using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Commands.ImportMenu;

/// <summary>
/// Bulk-creates a restaurant's menus, items, option groups, and options from a parsed Excel workbook.
/// Images referenced by filename are looked up in <see cref="ImageBlobs"/>, uploaded to R2,
/// and the resulting public URLs are stored on each MenuItem.
///
/// Append-only for v1: re-running the same workbook creates duplicate menus/items.
/// </summary>
public sealed record ImportMenuCommand(
    Guid RestaurantId,
    Stream WorkbookStream,
    IReadOnlyDictionary<string, ImageBlob> ImageBlobs
) : IRequest<Result<ImportMenuResponse>>;

public sealed record ImageBlob(byte[] Content, string ContentType);

public sealed record ImportMenuResponse(
    int MenusCreated,
    int ItemsCreated,
    int OptionGroupsCreated,
    int OptionsCreated,
    int ImagesUploaded,
    IReadOnlyList<string> Warnings);
