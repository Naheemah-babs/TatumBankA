using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TatumWelcome2.API.Security
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AdminApiKeyAttribute : Attribute, IAuthorizationFilter
    {
        public const string HeaderName = "X-Admin-Key";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            IConfiguration configuration =
                context.HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>();

            string expectedKey =
                configuration["Admin:ApiKey"]
                ?? throw new InvalidOperationException(
                    "Admin API key is missing.");

            string suppliedKey =
                context.HttpContext.Request.Headers[HeaderName].ToString();

            if (!ApiKeyComparer.Matches(expectedKey, suppliedKey))
            {
                context.Result = new UnauthorizedResult();
            }
        }
    }
}
