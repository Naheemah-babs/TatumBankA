
namespace TatumWelcome2.DTOs;

public class WithdrawRequestDto
{
    public decimal Amount { get; set; }

    public string Pin { get; set; } = string.Empty;
}
