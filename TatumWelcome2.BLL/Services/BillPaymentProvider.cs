using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

/// <summary>
/// Test biller: outcome follows the admin payment mode
/// (BillPaymentSettings.PaymentMode). Random approves 80% of payments.
/// </summary>
public sealed class BillPaymentProvider : IBillPaymentProvider
{
    private const int RandomApprovalPercent = 80;

    private readonly IBillPaymentRepository _repository;

    public BillPaymentProvider(IBillPaymentRepository repository)
    {
        _repository = repository;
    }

    public BillProviderDecision Authorize(BillPaymentRequest request)
    {
        bool approved = _repository.GetPaymentMode() switch
        {
            BillPaymentModes.Success => true,
            BillPaymentModes.Failure => false,
            _ => Random.Shared.Next(100) < RandomApprovalPercent
        };

        return approved
            ? new BillProviderDecision(true, "Bill payment successful.")
            : new BillProviderDecision(false, "Bill payment failed. Please try again.");
    }
}
