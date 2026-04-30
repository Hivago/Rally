using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using RallyAPI.SharedKernel.Filters;

namespace RallyAPI.SharedKernel.Extensions;

public static class IdempotencyExtensions
{
    public static RouteHandlerBuilder RequireIdempotency(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<IdempotencyEndpointFilter>();
}
