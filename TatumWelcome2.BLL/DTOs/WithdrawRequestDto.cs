
namespace TatumWelcome2.BLL.DTOs;

public class WithdrawRequestDto
{
    public decimal Amount { get; set; }

    public string Pin { get; set; } = string.Empty;
}
