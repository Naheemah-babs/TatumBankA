namespace TatumWelcome2.BLL.DTOs;

public class BalanceResponseDto
{
    public string AccountNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public bool IsFrozen { get; set; }
}
