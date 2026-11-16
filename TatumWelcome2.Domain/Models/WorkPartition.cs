using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.Domain.Models
{
    public readonly record struct WorkPartition(int Index, int Count)
    {
        /// <summary>A single partition covering everything.</summary>
        public static WorkPartition All { get; } = new(0, 1);

        public bool IsValid => Count >= 1 && Index >= 0 && Index < Count;

        public void EnsureValid()
        {
            if (!IsValid)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Index),
                    $"Invalid partition {Index}/{Count}: " +
                    "Count must be at least 1 and 0 <= Index < Count.");
            }
        }

        public override string ToString() => $"{Index}/{Count}";
    }
}
