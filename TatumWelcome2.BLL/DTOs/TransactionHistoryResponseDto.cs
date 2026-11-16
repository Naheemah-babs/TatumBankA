namespace TatumWelcome2.BLL.DTOs;

public class TransactionHistoryResponseDto
{
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Newest first.</summary>
    public List<TransactionItemDto> Transactions { get; set; } = new();
}
