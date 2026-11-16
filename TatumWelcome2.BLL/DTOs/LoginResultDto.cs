using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.DTOs;

public enum LoginOutcome
{
    Success,
    MissingCredentials,
    InvalidCredentials
}


public sealed record LoginResult(LoginOutcome Outcome, Account? Account = null);

