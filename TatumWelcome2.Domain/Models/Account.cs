using TatumWelcome2.Domain.Models;

public sealed class Account
{
    public string AccountNumber { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    /// <summary>PBKDF2 hash of the PIN. The raw PIN is never stored here.</summary>
    public string PinHash { get; init; } = string.Empty;

    public decimal Balance { get; init; }

    public bool IsFrozen { get; init; }

    public IReadOnlyList<Transaction> Transactions { get; init; } =
        Array.Empty<Transaction>();
}