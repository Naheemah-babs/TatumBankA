
namespace TatumWelcome2.DTOs;

public class CreateAccountRequestDto
{
    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Pin { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public decimal OpeningDeposit { get; set; }
}
