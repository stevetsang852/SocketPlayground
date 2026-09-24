namespace SocketServerNetCore.TcpPlayground;

public enum DuplicateSessionPolicy
{
    RejectNew,
    ReplaceExisting
}

public static class CommandProtocol
{
    public const string ProtocolVersion = "1.0";
    public const string ServerDeviceId = "server";
    public const string ServerRole = "server";

    /// <summary>Always-available playground commands (safe by design).</summary>
    public static readonly IReadOnlySet<string> SafeCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "health-check",
        "refresh-config",
        "collect-diagnostics",
        "list-status",
        "ping-time",
        "custom-cmd"
    };

    /// <summary>
    /// High-risk legacy Socket.IO capabilities. Only included when the server
    /// is started with <c>--allow-legacy-commands true</c> (default off).
    /// </summary>
    public static readonly IReadOnlySet<string> LegacyCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "csharp",
        "upload",
        "wallpapertaskpack"
    };

    /// <summary>
    /// All known command names (safe + legacy). Prefer <see cref="GetAllowedCommands"/> for runtime gating.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedCommands = new HashSet<string>(
        SafeCommands.Concat(LegacyCommands),
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> GetAllowedCommands(bool allowLegacyCommands)
    {
        if (!allowLegacyCommands)
        {
            return SafeCommands;
        }

        return AllowedCommands;
    }

    public static bool IsLegacyCommand(string? commandName)
        => !string.IsNullOrWhiteSpace(commandName) && LegacyCommands.Contains(commandName);

    public static bool IsKnownRole(string? role)
        => role is DeviceRoles.Admin or DeviceRoles.Client;

    public static DuplicateSessionPolicy ParseDuplicateSessionPolicy(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "replace-existing" => DuplicateSessionPolicy.ReplaceExisting,
            _ => DuplicateSessionPolicy.RejectNew
        };
}

public static class DeviceRoles
{
    public const string Admin = "admin";
    public const string Client = "client";
}
