using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class DepositResponseDto
    {
        public string Message { get; set; }
        public decimal Amount { get; set; }
        public string AccountNumber { get; set; }
    }
}
