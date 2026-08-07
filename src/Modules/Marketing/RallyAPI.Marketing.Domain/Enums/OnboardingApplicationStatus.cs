namespace RallyAPI.Marketing.Domain.Enums;

public enum OnboardingApplicationStatus
{
    /// <summary>Submitted, awaiting admin review. No live owner/restaurant account exists yet.</summary>
    Pending = 0,

    /// <summary>Admin has reviewed and approved — a follow-up manual step creates the real owner/restaurant account.</summary>
    Approved = 1,

    /// <summary>Admin has reviewed and rejected. Terminal state.</summary>
    Rejected = 2
}
