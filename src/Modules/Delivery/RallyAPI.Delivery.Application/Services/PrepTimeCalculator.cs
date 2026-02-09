using Microsoft.Extensions.Options;

namespace RallyAPI.Delivery.Application.Services;

public sealed class PrepTimeCalculator
{
    private readonly PrepTimeOptions _options;

    public PrepTimeCalculator(IOptions<PrepTimeOptions> options)
    {
        _options = options.Value;
    }

    public PrepTimeResult Calculate(int itemCount)
    {
        // Formula: 15 min base + 5 min per additional item
        var prepMinutes = _options.BaseMinutes +
                         (Math.Max(0, itemCount - 1) * _options.MinutesPerAdditionalItem);

        // Dispatch X minutes before food is ready
        var dispatchAfter = Math.Max(0, prepMinutes - _options.DispatchBufferMinutes);

        return new PrepTimeResult(prepMinutes, dispatchAfter);
    }
}

public sealed record PrepTimeResult(
    int TotalPrepMinutes,
    int DispatchAfterMinutes);

public sealed class PrepTimeOptions
{
    public const string SectionName = "Delivery:PrepTime";

    public int BaseMinutes { get; set; } = 15;
    public int MinutesPerAdditionalItem { get; set; } = 5;
    public int DispatchBufferMinutes { get; set; } = 5;
}