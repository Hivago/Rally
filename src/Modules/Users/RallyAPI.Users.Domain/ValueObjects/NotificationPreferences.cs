namespace RallyAPI.Users.Domain.ValueObjects;

public sealed record NotificationPreferences
{
    public bool EmailAlerts { get; init; }
    public bool BrowserNotifications { get; init; }
    public bool OrderSound { get; init; }

    public static NotificationPreferences Default() =>
        new() { EmailAlerts = true, BrowserNotifications = true, OrderSound = true };

    public static NotificationPreferences Create(bool emailAlerts, bool browserNotifications, bool orderSound) =>
        new() { EmailAlerts = emailAlerts, BrowserNotifications = browserNotifications, OrderSound = orderSound };
}
