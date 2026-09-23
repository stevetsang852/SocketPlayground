using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest.Interface;

internal static class TestEnvironmentRequirements
{
    public static void RequireWindows([CallerMemberName] string? testName = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive($"{testName} requires Windows-specific APIs and is skipped on this platform.");
        }
    }

    public static void RequireExternalNetworkOptIn([CallerMemberName] string? testName = null)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SOCKETPLAYGROUND_RUN_EXTERNAL_TESTS"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive($"{testName} requires external network access and is skipped unless SOCKETPLAYGROUND_RUN_EXTERNAL_TESTS=1.");
        }
    }
}
