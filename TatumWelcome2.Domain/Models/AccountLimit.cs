using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.Domain.Models
{
    public static class AccountLimits
    {
        /// <summary>Largest balance an account may hold (DECIMAL(18, 2)).</summary>
        public const decimal MaximumBalance = 9999999999999999.99m;
    }
}
