using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Helper;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

public sealed class CustomerBillPaymentService : ICustomerBillPaymentService
{
    /// <summary>BankId recorded on BillPayments for this bank's own customers.</summary>
    public const string OwnBankId = "BankA";

    private readonly IBillPaymentRepository _repository;
    private readonly IBillPaymentProvider _provider;
    private readonly IAccountRepository _accounts;

    public CustomerBillPaymentService(
        IBillPaymentRepository repository,
        IBillPaymentProvider provider,
        IAccountRepository accounts)
    {
        _repository = repository;
        _provider = provider;
        _accounts = accounts;
    }

    public BillPurchaseResult BuyAirtime(
        string accountNumber,
        AirtimePurchaseRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return Invalid(request.Reference, "PIN is required.", request.Amount);

        if (!TryResolveReference(request.Reference, out string reference))
            return InvalidReference(request.Reference, request.Amount);

        // Clean the number once, here, so the stored value is always 11 digits.
        string? phoneError = InputValidator.ValidatePhone(request.PhoneNumber, out string phone);

        if (phoneError != null)
            return Invalid(reference, phoneError, request.Amount);

        BillPaymentRequest bill = new()
        {
            Reference = reference,
            BankId = OwnBankId,
            AccountNumber = accountNumber,
            PaymentType = BillPaymentTypes.Airtime,
            Network = NetworkHelper.Normalize(request.Network),
            PhoneNumber = phone,
            Amount = request.Amount
        };

        return Purchase(
            bill,
            request.Pin,
            TransactionType.AirtimePurchase,
            TransactionDescriptions.Airtime(bill.Network, phone, reference));
    }

    public BillPurchaseResult BuyData(
        string accountNumber,
        DataPurchaseRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return Invalid(request.Reference, "PIN is required.", 0m);

        if (!TryResolveReference(request.Reference, out string reference))
            return InvalidReference(request.Reference, 0m);

        string? phoneError = InputValidator.ValidatePhone(request.PhoneNumber, out string phone);

        if (phoneError != null)
            return Invalid(reference, phoneError, 0m);

        DataPlan? plan = DataPlans.DataPlanItems.FirstOrDefault(
            item => string.Equals(
                item.Name,
                request.PlanName?.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (plan == null)
            return Invalid(reference, "Invalid data plan.", 0m);

        BillPaymentRequest bill = new()
        {
            Reference = reference,
            BankId = OwnBankId,
            AccountNumber = accountNumber,
            PaymentType = BillPaymentTypes.Data,
            Network = NetworkHelper.Normalize(request.Network),
            PhoneNumber = phone,
            Amount = plan.Price,
            DataPlanName = plan.Name
        };

        return Purchase(
            bill,
            request.Pin,
            TransactionType.DataPurchase,
            TransactionDescriptions.Data(bill.Network, plan.Name, phone, reference));
    }

    private BillPurchaseResult Purchase(
        BillPaymentRequest bill,
        string pin,
        TransactionType transactionType,
        string description)
    {
        string? error = BillPaymentValidator.Validate(bill);

        if (error != null)
            return Invalid(bill.Reference, error, bill.Amount);

        // Input mistakes are reported first, so they never count as wrong PINs.
        PinAttemptOutcome pinAttempt =
            _accounts.VerifyPinWithLockout(bill.AccountNumber, pin);

        if (pinAttempt == PinAttemptOutcome.Locked)
        {
            int seconds = _accounts.GetLockSecondsRemaining(bill.AccountNumber);

            return new BillPurchaseResult(
                BillPurchaseOutcome.AccountLocked,
                bill.Reference,
                $"Too many requests. Please try again in {seconds} seconds.",
                bill.Amount,
                TransactionResult.AccountLocked,
                RetryAfterSeconds: seconds);
        }

        if (pinAttempt == PinAttemptOutcome.WrongPin)
            return Rejected(bill, TransactionResult.InvalidPin);

        if (pinAttempt == PinAttemptOutcome.AccountNotFound)
            return Rejected(bill, TransactionResult.AccountNotFound);

        // Only a verified PIN reaches the provider.
        BillProviderDecision decision = _provider.Authorize(bill);

        // The repository checks the PIN again against the locked row.
        CustomerPurchaseWrite write = _repository.PurchaseForCustomer(
            bill,
            pin,
            transactionType,
            description,
            decision);

        return write.Status switch
        {
            CustomerPurchaseWriteStatus.Completed =>
                new BillPurchaseResult(
                    decision.Approved
                        ? BillPurchaseOutcome.Successful
                        : BillPurchaseOutcome.Declined,
                    bill.Reference,
                    decision.Message,
                    bill.Amount),

            CustomerPurchaseWriteStatus.Rejected =>
                Rejected(bill, write.RejectionReason ?? TransactionResult.PaymentFailed),

            _ => Replay(bill, write.Existing)
        };
    }

    private static BillPurchaseResult Rejected(
        BillPaymentRequest bill,
        TransactionResult reason)
    {
        string message = reason switch
        {
            TransactionResult.AccountNotFound => "Account not found.",
            TransactionResult.InvalidPin => "Incorrect PIN.",
            TransactionResult.AccountLocked => "Account is temporarily locked. Please try again later.",
            TransactionResult.AccountFrozen => "Account is frozen.",
            TransactionResult.InsufficientFunds => "Insufficient funds.",
            _ => "Purchase failed."
        };

        return new BillPurchaseResult(
            BillPurchaseOutcome.Rejected,
            bill.Reference,
            message,
            bill.Amount,
            reason);
    }

    /// <summary>Known reference: same details replay the stored outcome (no new debit).</summary>
    private static BillPurchaseResult Replay(
        BillPaymentRequest bill,
        BillPaymentRecord? existing)
    {
        if (existing == null)
        {
            return new BillPurchaseResult(
                BillPurchaseOutcome.InvalidRequest,
                bill.Reference,
                "Unable to retrieve previous payment.",
                bill.Amount);
        }

        if (!BillPaymentValidator.IsSameRequest(existing, bill))
        {
            return new BillPurchaseResult(
                BillPurchaseOutcome.ReferenceConflict,
                bill.Reference,
                "Payment reference already used for another request.",
                bill.Amount);
        }

        return new BillPurchaseResult(
            existing.Success
                ? BillPurchaseOutcome.Successful
                : BillPurchaseOutcome.Declined,
            bill.Reference,
            existing.Message,
            existing.Amount,
            Replayed: true);
    }

    /// <summary>Uses the caller's reference (GUID "N" format) or generates one.</summary>
    private static bool TryResolveReference(string? reference, out string resolved)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            resolved = Guid.NewGuid().ToString("N");
            return true;
        }

        resolved = reference.Trim();
        return Guid.TryParseExact(resolved, "N", out _);
    }

    private static BillPurchaseResult InvalidReference(string? reference, decimal amount)
    {
        return Invalid(
            reference,
            "Reference must be a 32-character GUID (\"N\" format) or omitted.",
            amount);
    }

    private static BillPurchaseResult Invalid(string? reference, string message, decimal amount)
    {
        return new BillPurchaseResult(
            BillPurchaseOutcome.InvalidRequest,
            reference ?? string.Empty,
            message,
            amount);
    }
}