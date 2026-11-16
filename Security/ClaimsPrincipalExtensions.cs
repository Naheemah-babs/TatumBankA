using System.Security.Claims;

namespace TatumWelcome2.API.Security;

    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// The customer's account number from the JWT ("sub", mapped to
        /// NameIdentifier by the JWT handler). Null when absent or blank.
        /// </summary>
        public static string? GetAccountNumber(this ClaimsPrincipal user)
        {
            string? value =
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

