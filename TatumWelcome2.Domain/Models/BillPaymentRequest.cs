namespace TatumWelcome2.Domain.Models;

public class BillPaymentRequest
{
    public string Reference { get; set; } = string.Empty;

    public string BankId { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string PaymentType { get; set; } = string.Empty;

    public string Network { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? DataPlanName { get; set; }
}
