using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.Domain.Models
{
    public sealed class LegacyPin
    {
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>The value currently stored in Accounts.PinHash.</summary>
        public string StoredValue { get; set; } = string.Empty;
    }
}
