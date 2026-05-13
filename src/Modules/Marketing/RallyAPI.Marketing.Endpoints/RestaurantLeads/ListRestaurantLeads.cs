using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Application.RestaurantLeads.Queries.ListLeads;
using RallyAPI.SharedKernel.Extensions;

namespace RallyAPI.Marketing.Endpoints.RestaurantLeads;

public sealed class ListRestaurantLeads : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/restaurant-leads", HandleAsync)
            .WithName("AdminListRestaurantLeads")
            .WithTags("Marketing")
            .WithSummary("Admin: paginated list of restaurant interest leads.")
            .RequireAuthorization("Admin");

        app.MapGet("/api/admin/restaurant-leads/export", ExportAsync)
            .WithName("AdminExportRestaurantLeads")
            .WithTags("Marketing")
            .WithSummary("Admin: CSV export of all restaurant leads.")
            .RequireAuthorization("Admin")
            .RequireRateLimiting("admin-export");
    }

    private static async Task<IResult> HandleAsync(
        ISender sender,
        CancellationToken cancellationToken,
        string? search = null,
        string? city = null,
        int? dailyOrders = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ListRestaurantLeadsQuery(search, city, dailyOrders, page, pageSize);
        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure
            ? result.Error.ToErrorResult()
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> ExportAsync(
        IRestaurantLeadRepository repository,
        CancellationToken cancellationToken)
    {
        var leads = await repository.GetAllAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Id,RestaurantName,OwnerName,Phone,City,DailyOrders,Source,CreatedAt");

        foreach (var l in leads)
        {
            sb.Append(l.Id).Append(',')
              .Append(Csv(l.RestaurantName)).Append(',')
              .Append(Csv(l.OwnerName)).Append(',')
              .Append(Csv(l.Phone)).Append(',')
              .Append(Csv(l.City)).Append(',')
              .Append(l.DailyOrders).Append(',')
              .Append(Csv(l.Source ?? string.Empty)).Append(',')
              .Append(l.CreatedAt.ToString("o", CultureInfo.InvariantCulture))
              .AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var filename = $"restaurant-leads-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
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
