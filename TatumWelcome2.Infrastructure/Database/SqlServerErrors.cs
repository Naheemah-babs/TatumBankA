using Microsoft.Data.SqlClient;

namespace TatumWelcome2.Infrastructure.Database;

internal static class SqlServerErrors
{
    /// <summary>Violation of a UNIQUE or PRIMARY KEY constraint.</summary>
    public const int UniqueConstraint = 2627;

    /// <summary>Duplicate key in a unique index.</summary>
    public const int UniqueIndex = 2601;

    public static bool IsUniqueViolation(SqlException ex) =>
        ex.Number is UniqueConstraint or UniqueIndex;

    /// <summary>True when the duplicate-key error names the given key.</summary>
    public static bool IsUniqueViolation(SqlException ex, string keyName) =>
        IsUniqueViolation(ex) &&
        ex.Message.Contains(keyName, StringComparison.OrdinalIgnoreCase);
}