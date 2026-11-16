using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class CreateAccountResponseDto
    {
        public string Message { get; set; }
        public string AccountNumber { get; set; }
        public string Name { get; set; }
        public string Branch { get; set; }
        public decimal Balance { get; set; }
    }
}
