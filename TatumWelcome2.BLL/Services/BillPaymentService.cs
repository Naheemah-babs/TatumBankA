using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

/// <summary>Provider side: payments requested by banks via POST /bills/pay.</summary>
public sealed class BillPaymentService : IBillPaymentService
{
    private readonly IBillPaymentRepository _repository;
    private readonly IBillPaymentProvider _provider;

    public BillPaymentService(
        IBillPaymentRepository repository,
        IBillPaymentProvider provider)
    {
        _repository = repository;
        _provider = provider;
    }

    public string PaymentMode => _repository.GetPaymentMode();

    public bool SetPaymentMode(string mode)
    {
        if (!BillPaymentModes.IsValid(mode))
            return false;

        _repository.SetPaymentMode(mode);
        return true;
    }

    public BillPaymentResponseDto ProcessPayment(
        BillPaymentRequest request)
    {
        if (request == null)
        {
            return new BillPaymentResponseDto
            {
                Success = false,
                Message = "Payment request is required."
            };
        }

        string? error = BillPaymentValidator.Validate(request);

        if (error != null)
            return Failed(request, error);

        BillProviderDecision decision = _provider.Authorize(request);
        BillPaymentWrite write = _repository.ProcessPayment(request, decision);

        if (write.Status == BillPaymentWriteStatus.Recorded)
        {
            return new BillPaymentResponseDto
            {
                Success = decision.Approved,
                Message = decision.Message,
                Reference = request.Reference
            };
        }

        // Known reference: replay the stored outcome or report a conflict.
        if (write.Existing == null)
            return Failed(request, "Unable to retrieve previous payment.");

        if (!BillPaymentValidator.IsSameRequest(write.Existing, request))
        {
            BillPaymentResponseDto conflict = Failed(
                request,
                "Payment reference already used for another request.");

            conflict.ReferenceConflict = true;
            return conflict;
        }

        return new BillPaymentResponseDto
        {
            Success = write.Existing.Success,
            Message = write.Existing.Message,
            Reference = write.Existing.Reference
        };
    }

    private static BillPaymentResponseDto Failed(
        BillPaymentRequest request,
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
