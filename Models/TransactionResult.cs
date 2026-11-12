namespace TatumWelcome2;

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
}