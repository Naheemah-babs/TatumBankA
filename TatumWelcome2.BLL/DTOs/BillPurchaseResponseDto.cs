using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class BillPurchaseResponseDto
    {
        public string Message { get; set; } = string.Empty;

        public string Reference { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        ///<summary>BillPurchaseOutcome name, e.g. Successful or Declined.</summary>
        public string Status { get; set; } = string.Empty;

        ///<summary>True when the reference was already processed (no new debit).</summary>
        public bool Replayed { get; set; }
    }
}
