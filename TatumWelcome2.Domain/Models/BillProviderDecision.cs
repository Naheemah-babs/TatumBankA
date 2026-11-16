namespace TatumWelcome2.Domain.Models;

/// <summary>The bill provider's answer for one payment.</summary>
public sealed record BillProviderDecision(bool Approved, string Message);
