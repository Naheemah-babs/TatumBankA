using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class AdminAccountResponseDto
    {
        public string AccountNumber { get; set; }
        public string Name { get; set; }
        public bool IsFrozen { get; set; }
    }
}
