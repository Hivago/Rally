using Microsoft.AspNetCore.Routing;

namespace RallyAPI.Marketing.Endpoints;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
