using System.Security.Cryptography;
using System.Text;

namespace SocketServerNetCore.TcpPlayground;

public sealed class LoginRequest
{
    public string? DeviceId { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; }
}

public static class LoginService
{
    public static bool TryAuthenticate(
        LoginRequest request,
        string authenticationSecret,
        string? adminUsername,
        string? adminPassword,
        out string deviceId,
        out string role,
        out string? error)
    {
        deviceId = request.DeviceId?.Trim() ?? string.Empty;
        role = string.IsNullOrWhiteSpace(request.Role) ? DeviceRoles.Client : request.Role.Trim();
        error = null;

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            error = "Login deviceId is required.";
            return false;
        }

        if (!CommandProtocol.IsKnownRole(role))
        {
            error = $"Unsupported login role '{role}'.";
            return false;
        }

        if (string.Equals(role, DeviceRoles.Admin, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword))
            {
                error = "Admin login is not configured on the server.";
                return false;
            }

            if (!FixedEquals(request.Username, adminUsername) || !FixedEquals(request.Password, adminPassword))
            {
                error = "Admin username or password is invalid.";
                return false;
            }

            return true;
        }

        if (!FixedEquals(request.Password, authenticationSecret))
        {
            error = "Client login password is invalid.";
            return false;
        }

        return true;
    }

    private static bool FixedEquals(string? left, string? right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
