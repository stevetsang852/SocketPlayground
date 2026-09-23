namespace SocketServerNetCore.Tests;

[TestClass]
public sealed class RoadmapPlaceholderTests
{
    [TestMethod]
    public void PhaseBNatTraversalScenariosStayDocumentedOnlyInCi()
    {
        Assert.Inconclusive("Phase B UDP hole punching needs real NAT boundaries, so GitHub-hosted CI keeps this as a documented placeholder only.");
    }

    [TestMethod]
    public void PhaseDTunVpnScenariosRequireExplicitLinuxOptIn()
    {
        if (!OperatingSystem.IsLinux() || !string.Equals(Environment.GetEnvironmentVariable("SOCKETPLAYGROUND_ENABLE_TUN_TESTS"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Phase D Linux TUN/VPN tests are placeholders and stay disabled in CI unless explicitly enabled on a prepared Linux machine.");
        }

        Assert.Inconclusive("Enable Phase D only for local prototypes that avoid root, /dev/net/tun, and routing mutations in CI.");
    }
}
