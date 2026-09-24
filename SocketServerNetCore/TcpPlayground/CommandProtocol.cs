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

    public static readonly IReadOnlySet<string> AllowedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "health-check",
        "refresh-config",
        "collect-diagnostics",
        "list-status",
        "ping-time"
    };

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
