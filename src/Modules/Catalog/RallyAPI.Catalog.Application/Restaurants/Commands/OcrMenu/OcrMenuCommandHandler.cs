using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Catalog.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Catalog.Application.Restaurants.Commands.OcrMenu;

internal sealed class OcrMenuCommandHandler
    : IRequestHandler<OcrMenuCommand, Result<OcrMenuResponse>>
{
    private readonly IMenuOcrService _ocr;
    private readonly ILogger<OcrMenuCommandHandler> _logger;

    public OcrMenuCommandHandler(IMenuOcrService ocr, ILogger<OcrMenuCommandHandler> logger)
    {
        _ocr = ocr;
        _logger = logger;
    }

    public async Task<Result<OcrMenuResponse>> Handle(OcrMenuCommand request, CancellationToken ct)
    {
        try
        {
            var result = await _ocr.ExtractAsync(request.Image, ct);
            return Result.Success(new OcrMenuResponse(result.RawText, result.SuggestedItems));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "OCR endpoint called but Google Vision is not configured.");
            return Result.Failure<OcrMenuResponse>(
                Error.Create("Catalog.Ocr.NotConfigured",
                    "Google Vision credentials are not configured on the server. Set GoogleVision:CredentialsJson and restart."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR extraction failed.");
            return Result.Failure<OcrMenuResponse>(
                Error.Create("Catalog.Ocr.Failed", "OCR extraction failed. See server logs for details."));
        }
    }
}
