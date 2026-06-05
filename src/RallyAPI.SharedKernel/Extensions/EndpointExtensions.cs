using Microsoft.AspNetCore.Http;
using RallyAPI.SharedKernel.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace RallyAPI.SharedKernel.Extensions;

/// <summary>
/// Extension methods for converting <see cref="Error"/> and <see cref="Result{T}"/> to
/// <see cref="IResult"/> responses using the canonical <see cref="ApiErrorResponse"/> shape.
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// Converts an <see cref="Error"/> to an <see cref="IResult"/> with the correct HTTP status code.
    /// Response body is always <see cref="ApiErrorResponse"/>.
    /// </summary>
    public static IResult ToErrorResult(this Error error)
    {
        var response = new ApiErrorResponse(
            error.Code,
            error.Message,
            error.FieldErrors);

        return HttpResults.Json(response, statusCode: GetStatusCode(error.Code));
    }

    /// <summary>
    /// Converts a failed <see cref="Result{T}"/> to an error <see cref="IResult"/>.
    /// </summary>
    public static IResult ToErrorResult<T>(this Result<T> result)
        => result.Error.ToErrorResult();

    /// <summary>
    /// Converts a failed <see cref="Result"/> to an error <see cref="IResult"/>.
    /// </summary>
    public static IResult ToErrorResult(this Result result)
        => result.Error.ToErrorResult();

    private static int GetStatusCode(string errorCode) => errorCode switch
    {
        var c when c.Contains("NotFound")     => StatusCodes.Status404NotFound,
        var c when c.Contains("Unauthorized") => StatusCodes.Status401Unauthorized,
        var c when c.Contains("Forbidden")    => StatusCodes.Status403Forbidden,
        var c when c.Contains("Conflict")     => StatusCodes.Status409Conflict,
        var c when c.Contains("TooManyRequests") => StatusCodes.Status429TooManyRequests,
        var c when c.Contains("Validation")   => StatusCodes.Status400BadRequest,
        _                                     => StatusCodes.Status400BadRequest
    };
}
