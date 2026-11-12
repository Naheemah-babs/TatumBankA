
namespace TatumWelcome2.DTOs;

public sealed class InterbankTransferOutcomeDto
{
    public string Reference { get; set; } = "";
    public string SenderAccountNumber { get; set; } = "";
    public string RecipientAccountNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
}
