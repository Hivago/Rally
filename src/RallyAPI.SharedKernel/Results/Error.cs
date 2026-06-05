namespace RallyAPI.SharedKernel.Results;

public sealed class Error
{
    public string Code { get; }
    public string Message { get; }
    public string Details { get; }

    /// <summary>
    /// Field-level validation errors. Populated only by <see cref="ValidationFailed"/>.
    /// </summary>
    public IReadOnlyList<FieldError>? FieldErrors { get; }

    private Error(string code, string message, string details = "", IReadOnlyList<FieldError>? fieldErrors = null)
    {
        Code = code;
        Message = message;
        Details = details;
        FieldErrors = fieldErrors;
    }

    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NullValue => new("Error.NullValue", "A null value was provided.");

    public static Error Create(string code, string message) => new(code, message);

    // Common errors
    public static Error NotFound(string entity, Guid id) =>
        new($"{entity}.NotFound", $"{entity} with ID {id} was not found.");
    public static Error NotFound(string entity) =>
        new($"{entity}.NotFound", $"{entity} was not found.");

    public static Error Validation(string message) =>
        new("Validation.Error", message);

    public static Error Validation(string message, string detail) =>
        new("Validation.Error", message, detail);

    /// <summary>
    /// Creates a validation error with structured per-field details.
    /// Used by <see cref="ValidationBehavior"/> to surface FluentValidation failures.
    /// </summary>
    public static Error ValidationFailed(IEnumerable<FieldError> fieldErrors)
    {
        var list = fieldErrors.ToList();
        return new Error(
            "Validation.Error",
            "One or more validation errors occurred.",
            string.Empty,
            list);
    }

    public static Error Conflict(string message) =>
        new("Conflict.Error", message);

    /// <summary>
    /// Rate-limit / lockout errors. Maps to HTTP 429 in <c>EndpointExtensions.GetStatusCode</c>.
    /// </summary>
    public static Error TooManyRequests(string message) =>
        new("RateLimit.TooManyRequests", message);

    public static Error Unauthorized(string message = "Unauthorized access.") =>
        new("Unauthorized.Error", message);

    public static Error Forbidden(string message = "Access forbidden.") =>
        new("Forbidden.Error", message);

    public override string ToString() => $"{Code}: {Message}";
}
