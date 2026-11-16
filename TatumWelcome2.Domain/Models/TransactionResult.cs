namespace TatumWelcome2.Domain.Models;

public enum TransactionResult
{
    Success,
    InvalidAmount,
    InsufficientFunds,
    AccountNotFound,
    SameAccount,
    InvalidRecipient,

    InvalidPin,

    InvalidNetwork,

    InvalidPhone,

    InvalidPlan,
    AccountFrozen,
    PaymentFailed,
    BalanceLimitExceeded,
    DuplicateReference,
    PinRequired,
    RecipientRequired,
}