using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SocketServerNetCore.TcpPlayground;

public sealed class CommandAuthTokenDescriptor
{
    public string DeviceId { get; init; } = string.Empty;
    public string Role { get; init; } = DeviceRoles.Client;
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public string ProtocolVersion { get; init; } = CommandProtocol.ProtocolVersion;
}

public static class CommandAuthTokenService
{
    public static string CreateToken(string secret, string deviceId, string role, DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (!CommandProtocol.IsKnownRole(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), $"Unsupported role '{role}'.");
        }

        var payload = new CommandAuthTokenDescriptor
        {
            DeviceId = deviceId,
            Role = role,
            ExpiresAtUtc = expiresAtUtc.ToUniversalTime(),
            ProtocolVersion = CommandProtocol.ProtocolVersion
        };

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonLineSocketProtocol.SerializerOptions);
        var signatureBytes = Sign(secret, payloadBytes);
        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signatureBytes)}";
    }

    public static bool TryValidateToken(string token, string secret, out CommandAuthTokenDescriptor? descriptor, out string? error)
    {
        descriptor = null;
        error = null;

        if (string.IsNullOrWhiteSpace(secret))
        {
            error = "Authentication secret is not configured.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            error = "Access token is required.";
            return false;
        }

        var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            error = "Access token format is invalid.";
            return false;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(parts[0]);
            var signatureBytes = Base64UrlDecode(parts[1]);
            var expectedSignatureBytes = Sign(secret, payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignatureBytes))
            {
                error = "Access token signature is invalid.";
                return false;
            }

            descriptor = JsonSerializer.Deserialize<CommandAuthTokenDescriptor>(payloadBytes, JsonLineSocketProtocol.SerializerOptions);
            if (descriptor is null)
            {
                error = "Access token payload is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptor.DeviceId))
            {
                error = "Access token deviceId is required.";
                return false;
            }

            if (!CommandProtocol.IsKnownRole(descriptor.Role))
            {
                error = $"Access token role '{descriptor.Role}' is not supported.";
                return false;
            }

            if (!string.Equals(descriptor.ProtocolVersion, CommandProtocol.ProtocolVersion, StringComparison.Ordinal))
            {
                error = $"Unsupported protocol version '{descriptor.ProtocolVersion}'.";
                return false;
            }

            if (descriptor.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                error = "Access token has expired.";
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or CryptographicException)
        {
            error = $"Access token could not be parsed: {exception.Message}";
            descriptor = null;
            return false;
        }
    }

    private static byte[] Sign(string secret, byte[] payloadBytes)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(payloadBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => $"{padded}==",
            3 => $"{padded}=",
            _ => padded
        };

        return Convert.FromBase64String(padded);
    }
}
