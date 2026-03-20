using FluentValidation;
using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Orders.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for automatic FluentValidation.
/// On failure returns a <see cref="Result{T}"/> with field-level <see cref="FieldError"/> details
/// instead of throwing, so endpoints can surface structured validation errors to clients.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        // Build structured field-level errors
        var fieldErrors = failures
            .Select(f => new FieldError(f.PropertyName, f.ErrorMessage))
            .ToList();

        var error = Error.ValidationFailed(fieldErrors);

        // Return failure result if TResponse is Result<T>
        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethods()
                .First(m => m.Name == "Failure" && m.IsGenericMethod)
                .MakeGenericMethod(resultType);

            return (TResponse)failureMethod.Invoke(null, new object[] { error })!;
        }

        // Return non-generic Result failure
        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        throw new ValidationException(failures);
    }
}
