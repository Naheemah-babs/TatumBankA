using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.Infrastructure.Database
{
    internal static class MySqlErrors
    {
        /// <summary>ER_DUP_ENTRY: duplicate value for a PRIMARY or UNIQUE key.</summary>
        public const int DuplicateEntry = 1062;

        public static bool IsUniqueViolation(MySqlException ex) =>
            ex.Number == DuplicateEntry;

        /// <summary>True when the duplicate-key error names the given key.</summary>
        public static bool IsUniqueViolation(MySqlException ex, string keyName) =>
            IsUniqueViolation(ex) &&
            ex.Message.Contains(keyName, StringComparison.OrdinalIgnoreCase);
    }

}
