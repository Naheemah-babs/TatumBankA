
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;
using TatumWelcome2.API.Security;
using TatumWelcome2.BLL.DTOs;

namespace TatumWelcome2.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InterbankApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Interbank-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        IConfiguration configuration =
            context.HttpContext.RequestServices
                .GetRequiredService<IConfiguration>();

        string expectedKey =
            configuration["Interbank:ApiKey"]
            ?? throw new InvalidOperationException(
                "Interbank API key is not configured.");

        string suppliedKey =
            context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!ApiKeyComparer.Matches(expectedKey, suppliedKey))
        {
            context.Result = new UnauthorizedObjectResult(
                new MessageResponseDto
                {
                    Message = "Invalid or missing interbank API key."
                });
        }
    }
}
