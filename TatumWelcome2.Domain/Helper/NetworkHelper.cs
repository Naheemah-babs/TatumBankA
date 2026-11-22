using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.Domain.Helper
{
    public class NetworkHelper
    {
        /// <summary>
        /// Returns the canonical spelling ("mtn" -> "MTN", " GLO " -> "Glo").
        /// An unknown network comes back trimmed, so the validator still rejects it.
        /// </summary>
        public static string Normalize(string? network)
        {
            string trimmed = network?.Trim() ?? string.Empty;

            return DataPlans.Networks.FirstOrDefault(name =>
                       string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                   ?? trimmed;
        }
    }
}
