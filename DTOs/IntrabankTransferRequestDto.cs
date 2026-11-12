
namespace TatumWelcome2.DTOs;

public class IntrabankTransferRequestDto
{
    public string RecipientAccountNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Pin { get; set; } = string.Empty;
}
