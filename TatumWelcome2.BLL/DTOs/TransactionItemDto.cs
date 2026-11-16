namespace TatumWelcome2.BLL.DTOs;

public class TransactionItemDto
{
    /// <summary>TransactionType name, e.g. Deposit or TransferOut.</summary>
    public string Type { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Reference { get; set; }
}
