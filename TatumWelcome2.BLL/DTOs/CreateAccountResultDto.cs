using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.DTOs
{
    public enum CreateAccountStatus
    {
        InvalidDetails,
        Created,
        DuplicatePhone,
        NumberUnavailable,
    }

    public sealed record CreateAccountResultDto(
        CreateAccountStatus Status,
        Account? Account = null,
        IReadOnlyList<FieldError>? Errors = null);
}