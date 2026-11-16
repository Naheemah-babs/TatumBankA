namespace TatumWelcome2.TransactionReconciliation.Processor;

/// <summary>Settings for the reconciliation loop, bound from the "Reconciliation" config section.</summary>
public sealed class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    /// <summary>Seconds to wait between passes over the Pending transfers.</summary>
    public int IntervalSeconds { get; set; } = 15;

    /// <summary>Which slice of the Pending transfers THIS instance owns (0-based).</summary>
    public int PartitionIndex { get; set; } = 0;

    /// <summary>How many instances share the work in total. 1 = this instance does everything.</summary>
    public int PartitionCount { get; set; } = 1;
}