namespace TatumWelcome2.Domain.Models;

public enum PinAttemptOutcome
{
    Verified,
    WrongPin,
    Locked,
    AccountNotFound
}