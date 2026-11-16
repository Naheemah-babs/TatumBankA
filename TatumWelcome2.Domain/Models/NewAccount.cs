using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.Domain.Models
{
    public sealed class NewAccount
    {
        public string AccountNumber { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Branch { get; init; } = string.Empty;

        public string Phone { get; init; } = string.Empty;

        public string PinHash { get; init; } = string.Empty;

        public decimal OpeningDeposit { get; init; }

        public string OpeningDepositReference { get; init; } = string.Empty;

        public string OpeningDepositDescription { get; init; } = string.Empty;
    }

}
