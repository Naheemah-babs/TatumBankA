
using System.Text.Json.Serialization;

namespace TatumWelcome2.BLL.DTOs;

public class BillPaymentResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;
    [JsonIgnore]
    public bool ReferenceConflict { get; set; }
}
