using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class SendInterbankTransferRequestDto
    {
        public string Reference { get; set; }
        public string RecipientAccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string Pin { get; set; }
    }
}
