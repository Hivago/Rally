using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Application.Waitlist.Queries.ListWaitlist;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Marketing.Endpoints.Waitlist;

public sealed class ListWaitlist : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/waitlist", HandleAsync)
            .WithName("AdminListWaitlist")
            .WithTags("Marketing")
            .WithSummary("Admin: paginated list of customer waitlist signups.")
            .RequireAuthorization("Admin");

        app.MapGet("/api/admin/waitlist/export", ExportAsync)
            .WithName("AdminExportWaitlist")
            .WithTags("Marketing")
            .WithSummary("Admin: CSV export of the full customer waitlist.")
            .RequireAuthorization("Admin")
            .RequireRateLimiting("admin-export");
    }

    private static async Task<IResult> HandleAsync(
        ISender sender,
        CancellationToken cancellationToken,
        string? search = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ListWaitlistQuery(search, page, pageSize);
        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> ExportAsync(
        ICustomerWaitlistRepository repository,
        CancellationToken cancellationToken)
    {
        var entries = await repository.GetAllAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Id,Name,Email,Phone,Source,CreatedAt");

        foreach (var e in entries)
        {
            sb.Append(e.Id).Append(',')
              .Append(Csv(e.Name)).Append(',')
              .Append(Csv(e.Email)).Append(',')
              .Append(Csv(e.Phone)).Append(',')
              .Append(Csv(e.Source ?? string.Empty)).Append(',')
              .Append(e.CreatedAt.ToString("o", CultureInfo.InvariantCulture))
              .AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var filename = $"waitlist-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return Results.File(bytes, "text/csv", filename);
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuoting ? $"\"{escaped}\"" : escaped;
    }
}
