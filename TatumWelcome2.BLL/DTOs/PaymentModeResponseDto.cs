using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TatumWelcome2.BLL.DTOs
{
    public class PaymentModeResponseDto
    {
        /// <summary>Present only after an update.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }

        public string PaymentMode { get; set; } = string.Empty;
    }
}
