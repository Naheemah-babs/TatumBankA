
using TatumWelcome2.DTOs;
using TatumWelcome2.Models;
using TatumWelcome2.Repositories;

namespace TatumWelcome2.BLL;

public class BillPaymentService
{
    private readonly BillPaymentRepository _repository;

    public BillPaymentService(BillPaymentRepository repository)
    {
        _repository = repository;
    }

    public string PaymentMode => _repository.GetPaymentMode();

    public bool SetPaymentMode(string mode)
    {
        if (mode != "Random" &&
            mode != "Success" &&
            mode != "Failure")
        {
            return false;
        }

        _repository.SetPaymentMode(mode);
        return true;
    }

    public BillPaymentResponseDto ProcessPayment(
        BillPaymentRequestDto request)
    {
        if (request == null)
        {
            return new BillPaymentResponseDto
            {
                Success = false,
                Message = "Payment request is required."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Reference) ||
            request.Reference.Length > 100)
        {
            return Failed(request, "Invalid payment reference.");
        }

        if (string.IsNullOrWhiteSpace(request.BankId) ||
            request.BankId.Length > 50)
        {
            return Failed(request, "Invalid bank identifier.");
        }

        if (string.IsNullOrWhiteSpace(request.AccountNumber) ||
            request.AccountNumber.Length > 10)
        {
            return Failed(request, "Invalid account number.");
        }

        if (request.PaymentType != "Airtime" &&
            request.PaymentType != "Data")
        {
            return Failed(request, "Invalid payment type.");
        }

        if (!DataPlans.Networks.Contains(request.Network))
        {
            return Failed(request, "Invalid network provider.");
        }

        if (request.PhoneNumber == null ||
            request.PhoneNumber.Length != 11 ||
            !request.PhoneNumber.All(char.IsDigit))
        {
            return Failed(request, "Invalid phone number.");
        }

        if (request.Amount <= 0 ||
            request.Amount > 9999999999999999.99m ||
            decimal.Round(request.Amount, 2) != request.Amount)
        {
            return Failed(request, "Invalid payment amount.");
        }

        if (request.PaymentType == "Airtime" &&
            request.Amount < 50)
        {
            return Failed(request, "Minimum airtime amount is 50.");
        }

        if (request.PaymentType == "Data")
        {
            bool validPlan = DataPlans.DataPlanItems.Any(
                plan => plan.Name == request.DataPlanName &&
                        plan.Price == request.Amount);

            if (!validPlan)
            {
                return Failed(request, "Invalid data plan.");
            }
        }

        if (request.DataPlanName?.Length > 100)
        {
            return Failed(request, "Invalid data plan name.");
        }

        return _repository.ProcessPayment(request);
    }

    private static BillPaymentResponseDto Failed(
        BillPaymentRequestDto request,
        string message)
    {
        return new BillPaymentResponseDto
        {
            Success = false,
            Message = message,
            Reference = request.Reference
        };
    }
}
