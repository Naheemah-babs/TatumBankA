
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TatumWelcome2.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InterbankApiKeyAttribute : Attribute, IAuthorizationFilter
{
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
            context.HttpContext.Request.Headers[
                "X-Interbank-Key"].ToString();

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        byte[] suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);

        if (expectedBytes.Length != suppliedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(
                expectedBytes,
                suppliedBytes))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                Message = "Invalid or missing interbank API key."
            });
        }
    }
}
