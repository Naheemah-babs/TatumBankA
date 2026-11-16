using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class LoginResponseDto
    {
        public string Message { get; set; } = string.Empty;

        public string AccountNumber { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool IsFrozen { get; set; }

        public string AccessToken { get; set; } = string.Empty;

        public string TokenType { get; set; } = "Bearer";

        public DateTime ExpiresAt { get; set; }
    }
}
