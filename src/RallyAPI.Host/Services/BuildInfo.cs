using System.Reflection;

namespace RallyAPI.Host.Services;

/// <summary>
/// Runtime build/release identity for this deployment. Read once at startup.
///
/// - <see cref="Version"/> comes from &lt;Version&gt; in the root Directory.Build.props.
/// - <see cref="Commit"/> / <see cref="Branch"/> come from Railway's git env vars at runtime
///   (RAILWAY_GIT_COMMIT_SHA / RAILWAY_GIT_BRANCH), so there is nothing to hand-edit per deploy.
/// - <see cref="BuildTimestampUtc"/> is stamped into assembly metadata at build time.
/// </summary>
public static class BuildInfo
{
    private static readonly Assembly EntryAssembly =
        Assembly.GetEntryAssembly() ?? typeof(BuildInfo).Assembly;

    /// <summary>SemVer release version, e.g. "1.0.0" (build metadata after '+' is stripped).</summary>
    public static string Version { get; } =
        EntryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? EntryAssembly.GetName().Version?.ToString()
        ?? "unknown";

    /// <summary>Short git commit SHA of the running build (Railway-provided), or "unknown" locally.</summary>
    public static string Commit { get; } = ResolveCommit();

    /// <summary>Git branch of the running build (Railway-provided), or "unknown" locally.</summary>
    public static string Branch { get; } =
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("RAILWAY_GIT_BRANCH"),
            Environment.GetEnvironmentVariable("GIT_BRANCH"))
        ?? "unknown";

    /// <summary>UTC time this assembly was built (ISO-8601), or "unknown".</summary>
    public static string BuildTimestampUtc { get; } =
        EntryAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTimestampUtc")?.Value
        ?? "unknown";

    private static string ResolveCommit()
    {
        // Prefer Railway's runtime env var; fall back to the SHA the .NET SDK embeds into
        // InformationalVersion at build time ("1.0.0+<sha>"), so this works locally too.
        var sha = FirstNonEmpty(
            Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA"),
            Environment.GetEnvironmentVariable("GIT_COMMIT"),
            CommitFromInformationalVersion());

        if (string.IsNullOrEmpty(sha))
            return "unknown";

        return sha.Length > 7 ? sha[..7] : sha;
    }

    private static string? CommitFromInformationalVersion()
    {
        var informational = EntryAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var plus = informational?.IndexOf('+') ?? -1;
        return plus >= 0 ? informational![(plus + 1)..] : null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
