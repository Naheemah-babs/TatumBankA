using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TatumWelcome2.BLL.DTOs
{
    public class AccountStatusResponseDto
    {
        public string Message { get; set; } = string.Empty;

        ///<summary>Omitted when the account was already in the requested state.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AccountNumber { get; set; }
    }
}
