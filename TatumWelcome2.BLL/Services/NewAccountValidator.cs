using TatumWelcome2.BLL.DTOs;

namespace TatumWelcome2.BLL.Services;

public sealed record NewAccountValidation(
    IReadOnlyList<FieldError> Errors,
    string Name,
    string Phone,
    string City)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Checks every account-creation field and reports all problems at once.</summary>
public static class NewAccountValidator
{
    public static NewAccountValidation Validate(CreateAccountRequestDto request)
    {
        List<FieldError> errors = new();

        string? nameError = InputValidator.ValidateName(request.Name, out string name);
        if (nameError != null)
            errors.Add(new FieldError("name", nameError));

        string? phoneError = InputValidator.ValidatePhone(request.Phone, out string phone);
        if (phoneError != null)
            errors.Add(new FieldError("phone", phoneError));

        string? cityError = InputValidator.ValidateCity(request.City, out string city);
        if (cityError != null)
            errors.Add(new FieldError("city", cityError));

        string? pinError = InputValidator.ValidatePin(request.Pin);
        if (pinError != null)
            errors.Add(new FieldError("pin", pinError));

        string? depositError = InputValidator.ValidateOpeningDeposit(request.OpeningDeposit);
        if (depositError != null)
            errors.Add(new FieldError("openingDeposit", depositError));

        return new NewAccountValidation(errors, name, phone, city);
    }
}