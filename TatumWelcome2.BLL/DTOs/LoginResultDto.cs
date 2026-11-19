using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.DTOs;

public enum LoginOutcome
{
    Success,
    MissingCredentials,
    InvalidCredentials,
    AccountLocked
}


public sealed record LoginResult(
    LoginOutcome Outcome,
    Account? Account = null,
    int RetryAfterSeconds = 0);

