using CommonClassLibrary;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Payload.Core.Command
{
    public class StartProcessCommandProps
    {
        public string ExeName { get; set; }
        public string TargetWorkSpaceDir { get; set; }
    }

    public class StartProcessCommand : Payload.Command.Interface.ICommand
    {
        public StartProcessCommandProps Props { get; set; }

        /// <summary>
        /// Builds launch parameters without starting a process (unit-testable on any OS).
        /// </summary>
        public static ProcessStartInfo BuildStartInfo(string? workingDir, string? exeName, bool useRunAs)
        {
            workingDir ??= string.Empty;
            exeName ??= string.Empty;

            // Prefer a full path so cmd.exe resolves the extracted patch exe reliably.
            var exePath = Path.IsPathRooted(exeName)
                ? exeName
                : (string.IsNullOrEmpty(workingDir) ? exeName : Path.Combine(workingDir, exeName));

            var psi = new ProcessStartInfo
            {
                // Note: CreateNoWindow has no effect when UseShellExecute is true (pre-existing).
                CreateNoWindow = true,
                FileName = "cmd.exe",
                UseShellExecute = true,
                // Quote the full path so spaces/special chars are safe under /C.
                Arguments = "/C \"" + exePath + "\""
            };

            if (!string.IsNullOrEmpty(workingDir))
            {
                psi.WorkingDirectory = workingDir;
            }

            if (useRunAs)
            {
                psi.Verb = "runas";
            }

            return psi;
        }

        public override Task Execute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException(
                    "StartProcessCommand (cmd.exe / runas patch launch) is Windows-only.");
            }

            string workingDir = Props != null ? Props.TargetWorkSpaceDir : Config.Instance.TargetWorkSpaceDir;
            string exeName = Props != null ? Props.ExeName : Config.Instance.ExeName;

            var psi = BuildStartInfo(workingDir, exeName, Config.Instance.StartProcessUseRunAs);

            try
            {
                var process = new Process { StartInfo = psi };
                process.Start();

                var timeoutMs = Config.Instance.StartProcessWaitTimeoutMs;
                if (timeoutMs <= 0)
                {
                    // Default / legacy: block until the process exits; never kill.
                    process.WaitForExit();
                }
                else if (!process.WaitForExit(timeoutMs))
                {
                    // Bounded wait only: do NOT kill a long-running installer.
                    // Leave the process alive and return after logging.
                    Console.WriteLine(
                        $"[StartProcessCommand] timed out waiting after {timeoutMs}ms for '{exeName}' " +
                        $"(pid={SafePid(process)}); process left running.");
                }
            }
            catch (PlatformNotSupportedException)
            {
                throw;
            }
            catch (Exception e)
            {
                // User declined UAC / not administrator / process start failure.
                Console.WriteLine(e.ToString());
                throw;
            }

            return null;
        }

        private static string SafePid(Process process)
        {
            try { return process.Id.ToString(); }
            catch { return "?"; }
        }
    }
}
