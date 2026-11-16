using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class AirtimePurchaseRequestDto
    {
        public string Network { get; set; }
        public string PhoneNumber { get; set; }
        public decimal Amount { get; set; }
        public string Pin { get; set; }
        public string? Reference { get; set; }
    }
}
