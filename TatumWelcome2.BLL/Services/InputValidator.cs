using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

/// <summary>
/// One place for input rules, so a rule change is made once.
/// Each method returns an error message, or null when the input is valid.
/// </summary>
public static class InputValidator
{
    public const int PinLength = 4;

    // Keep these at or below the column sizes in the Accounts table.
    public const int NameMaxLength = 100;
    public const int CityMaxLength = 50;

    public const decimal MinimumOpeningDeposit = 1000m;

    public static string? ValidatePin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return "PIN is required.";

        if (!pin.All(char.IsAsciiDigit))
            return "PIN must contain digits only (0-9), with no letters, spaces or symbols.";

        if (pin.Length < PinLength)
            return $"PIN is too short. It must be exactly {PinLength} digits.";

        if (pin.Length > PinLength)
            return $"PIN is too long. It must be exactly {PinLength} digits.";

        return null;
    }

    /// <summary>
    /// Validates a Nigerian mobile number and returns it in the 11-digit form
    /// (08012345678). Accepts +2348012345678, 2348012345678, 8012345678, and
    /// spaces, dashes or brackets.
    /// </summary>
    public static string? ValidatePhone(string? input, out string phone)
    {
        phone = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
            return "Phone number is required.";

        string cleaned = new(input.Where(c => !" -()".Contains(c)).ToArray());

        if (cleaned.StartsWith("+234"))
            cleaned = "0" + cleaned[4..];
        else if (cleaned.StartsWith("234") && cleaned.Length == 13)
            cleaned = "0" + cleaned[3..];
        else if (cleaned.Length == 10 && (cleaned[0] is '7' or '8' or '9'))
            cleaned = "0" + cleaned;

        if (!cleaned.All(char.IsAsciiDigit))
            return "Phone number must contain digits only. A leading +234 is allowed.";

        if (cleaned.Length < 11)
            return "Phone number is too short. Use 11 digits, e.g. 08012345678.";

        if (cleaned.Length > 11)
            return "Phone number is too long. Use 11 digits, e.g. 08012345678.";

        if (cleaned[0] != '0' ||
            cleaned[1] is not ('7' or '8' or '9') ||
            cleaned[2] is not ('0' or '1'))
        {
            return "Enter a valid Nigerian mobile number, e.g. 08012345678. " +
                   "It must start with 070, 080, 081, 090 or 091.";
        }

        phone = cleaned;
        return null;
    }

    public static string? ValidateName(string? input, out string name) =>
        ValidateText("Name", input, 2, NameMaxLength, out name);

    public static string? ValidateCity(string? input, out string city) =>
        ValidateText("City", input, 2, CityMaxLength, out city);

    public static string? ValidateOpeningDeposit(decimal amount)
    {
        if (amount < MinimumOpeningDeposit)
            return $"Opening deposit must be at least {MinimumOpeningDeposit:N0}.";

        if (amount > AccountLimits.MaximumBalance)
            return $"Opening deposit cannot be more than {AccountLimits.MaximumBalance:N0}.";

        if (decimal.Round(amount, 2) != amount)
            return "Opening deposit can have at most 2 decimal places.";

        return null;
    }

    private static string? ValidateText(
        string label,
        string? input,
        int minLength,
        int maxLength,
        out string value)
    {
        value = string.Empty;
        string trimmed = input?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
            return $"{label} is required.";

        if (trimmed.Length < minLength)
            return $"{label} is too short. Use at least {minLength} characters.";

        if (trimmed.Length > maxLength)
            return $"{label} is too long. Use at most {maxLength} characters.";

        if (!trimmed.All(c => char.IsLetter(c) || c is ' ' or '-' or '\'' or '.'))
        {
            return $"{label} can contain only letters, spaces, hyphens, " +
                   "apostrophes and full stops.";
        }

        value = trimmed;
        return null;
    }
}