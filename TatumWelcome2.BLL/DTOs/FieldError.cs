namespace TatumWelcome2.BLL.DTOs;

/// <summary>One validation problem, tied to the field it belongs to.</summary>
public sealed record FieldError(string Field, string Message);