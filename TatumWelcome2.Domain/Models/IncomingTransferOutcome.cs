using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.Domain.Models
{
    public class IncomingTransferOutcome
    {
        public string Reference { get; set; }
        public string SenderAccountNumber { get; set; }
        public string RecipientAccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
    }
}
