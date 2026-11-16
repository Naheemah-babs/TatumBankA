using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

/// <summary>
/// Rules for a bill payment request, shared by the provider endpoint
/// (POST /bills/pay) and customer airtime/data purchases.
/// </summary>
public static class BillPaymentValidator
{
    public const decimal MaximumAmount = AccountLimits.MaximumBalance;
    public const decimal MinimumAirtimeAmount = 50m;

    /// <summary>Returns an error message, or null when the request is valid.</summary>
    public static string? Validate(BillPaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reference) ||
            request.Reference.Length > 100)
        {
            return "Invalid payment reference.";
        }

        if (string.IsNullOrWhiteSpace(request.BankId) ||
            request.BankId.Length > 50)
        {
            return "Invalid bank identifier.";
        }

        if (string.IsNullOrWhiteSpace(request.AccountNumber) ||
            request.AccountNumber.Length > 10)
        {
            return "Invalid account number.";
        }

        if (request.PaymentType != BillPaymentTypes.Airtime &&
            request.PaymentType != BillPaymentTypes.Data)
        {
            return "Invalid payment type.";
        }

        if (!DataPlans.Networks.Contains(request.Network))
        {
            return "Invalid network provider.";
        }

        if (request.PhoneNumber == null ||
            request.PhoneNumber.Length != 11 ||
            !request.PhoneNumber.All(char.IsDigit))
        {
            return "Invalid phone number.";
        }

        if (request.Amount <= 0 ||
            request.Amount > MaximumAmount ||
            decimal.Round(request.Amount, 2) != request.Amount)
        {
            return "Invalid payment amount.";
        }

        if (request.PaymentType == BillPaymentTypes.Airtime &&
            request.Amount < MinimumAirtimeAmount)
        {
            return "Minimum airtime amount is 50.";
        }

        if (request.PaymentType == BillPaymentTypes.Data)
        {
            bool validPlan = DataPlans.DataPlanItems.Any(
                plan => plan.Name == request.DataPlanName &&
                        plan.Price == request.Amount);

            if (!validPlan)
            {
                return "Invalid data plan.";
            }
        }

        if (request.DataPlanName?.Length > 100)
        {
            return "Invalid data plan name.";
        }

        return null;
    }

    /// <summary>True when a stored payment matches the request field for field.</summary>
    public static bool IsSameRequest(
        BillPaymentRecord previous,
        BillPaymentRequest request)
    {
        return previous.BankId == request.BankId &&
               previous.AccountNumber == request.AccountNumber &&
               previous.PaymentType == request.PaymentType &&
               previous.Network == request.Network &&
               previous.PhoneNumber == request.PhoneNumber &&
               previous.Amount == request.Amount &&
               previous.DataPlanName == request.DataPlanName;
    }
}
