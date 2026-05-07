using MediatR;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Commands.OcrMenu;

/// <summary>
/// Runs Google Vision OCR over a menu image and returns the raw text plus best-effort
/// item-line suggestions. Used by ops during onboarding when a partner only has a
/// paper/PDF menu — the suggestions go into the Excel template.
/// </summary>
public sealed record OcrMenuCommand(Stream Image) : IRequest<Result<OcrMenuResponse>>;

public sealed record OcrMenuResponse(
    string RawText,
    IReadOnlyList<MenuOcrSuggestedItem> SuggestedItems);
