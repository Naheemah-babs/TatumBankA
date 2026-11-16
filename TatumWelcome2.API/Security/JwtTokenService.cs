using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace TatumWelcome2.API.Security
{
    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions _options;

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        public AccessToken CreateToken(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(_options.Key))
                throw new InvalidOperationException("JWT signing key is missing.");

            if (string.IsNullOrWhiteSpace(_options.Issuer))
                throw new InvalidOperationException("JWT issuer is missing.");

            if (string.IsNullOrWhiteSpace(_options.Audience))
                throw new InvalidOperationException("JWT audience is missing.");

            Claim[] claims =
            {
            new(JwtRegisteredClaimNames.Sub, accountNumber),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            SigningCredentials credentials = new(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
                SecurityAlgorithms.HmacSha256);

            DateTime expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

            JwtSecurityToken token = new(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new AccessToken(
                new JwtSecurityTokenHandler().WriteToken(token),
                expiresAt);
        }
    }
}
