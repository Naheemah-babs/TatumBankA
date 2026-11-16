using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class BillPaymentModes
    {
        public const string Random = "Random";
        public const string Success = "Success";
        public const string Failure = "Failure";
        
        public static bool IsValid(string? mode) => mode == Random || mode == Success || mode == Failure; 

    }
}
