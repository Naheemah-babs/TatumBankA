using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.Domain.Models
{
    public class InterbankTransferStatus
    {
        public const string Processing = "Processing";
        public const string Pending = "Pending";
        public const string Successful = "Successful";
        public const string Rejected = "Rejected";
        public const string Reversed = "Reversed";

    }
}
