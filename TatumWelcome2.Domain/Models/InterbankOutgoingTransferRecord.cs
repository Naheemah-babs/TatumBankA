using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.Domain.Models
{
    public class InterbankOutgoingTransferRecord
    {
        public string Reference { get; set; }
        public string SenderAccountNumber { get; set; }
        public string RecipientAccountNumber { get; set; }
        public string BankUrl { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
    }
}
