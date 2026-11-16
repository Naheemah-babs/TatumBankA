using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

public sealed class CustomerBillPaymentService : ICustomerBillPaymentService
{
    /// <summary>BankId recorded on BillPayments for this bank's own customers.</summary>
    public const string OwnBankId = "BankA";

    private readonly IBillPaymentRepository _repository;
    private readonly IBillPaymentProvider _provider;

    public CustomerBillPaymentService(
        IBillPaymentRepository repository,
        IBillPaymentProvider provider)
    {
        _repository = repository;
        _provider = provider;
    }

    public BillPurchaseResult BuyAirtime(
        string accountNumber,
        AirtimePurchaseRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return Invalid(request.Reference, "PIN is required.", request.Amount);

        if (!TryResolveReference(request.Reference, out string reference))
            return InvalidReference(request.Reference, request.Amount);

        BillPaymentRequest bill = new()
        {
            Reference = reference,
            BankId = OwnBankId,
            AccountNumber = accountNumber,
            PaymentType = BillPaymentTypes.Airtime,
            Network = request.Network,
            PhoneNumber = request.PhoneNumber,
            Amount = request.Amount
        };

        return Purchase(
            bill,
            request.Pin,
            TransactionType.AirtimePurchase,
            TransactionDescriptions.Airtime(request.Network, request.PhoneNumber, reference));
    }

    public BillPurchaseResult BuyData(
        string accountNumber,
        DataPurchaseRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return Invalid(request.Reference, "PIN is required.", 0m);

        if (!TryResolveReference(request.Reference, out string reference))
            return InvalidReference(request.Reference, 0m);

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
            Network = request.Network,
            PhoneNumber = request.PhoneNumber,
            Amount = plan.Price,
            DataPlanName = plan.Name
        };

        return Purchase(
            bill,
            request.Pin,
            TransactionType.DataPurchase,
            TransactionDescriptions.Data(request.Network, plan.Name, request.PhoneNumber, reference));
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

        BillProviderDecision decision = _provider.Authorize(bill);

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
