using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class TransferResponseDto
    {
        public string Message { get; set; }
        public string RecipientAccountNumber { get; set; }
        public decimal Amount { get; set; }
    }
}
