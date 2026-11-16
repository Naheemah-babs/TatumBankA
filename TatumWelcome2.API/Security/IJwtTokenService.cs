namespace TatumWelcome2.API.Security
{
    public sealed record AccessToken(string Token, DateTime ExpiresAt);

    public interface IJwtTokenService
    {
        /// <summary>Issues a signed customer token whose subject is the account number.</summary>
        AccessToken CreateToken(string accountNumber);
    }

}
