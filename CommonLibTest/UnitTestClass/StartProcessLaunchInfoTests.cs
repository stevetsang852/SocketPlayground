using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Payload.Core.Command;

namespace CommonLibTest;

[TestClass]
public sealed class StartProcessLaunchInfoTests
{
    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void BuildStartInfo_SetsWorkingDirectoryAndFullExePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "patch-dir");
        var psi = StartProcessCommand.BuildStartInfo(dir, "foo.exe", useRunAs: true);

        Assert.AreEqual("cmd.exe", psi.FileName);
        Assert.AreEqual(dir, psi.WorkingDirectory);
        Assert.AreEqual("runas", psi.Verb);
        Assert.IsTrue(psi.UseShellExecute);
        StringAssert.Contains(psi.Arguments, Path.Combine(dir, "foo.exe"));
        StringAssert.StartsWith(psi.Arguments, "/C \"");
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void BuildStartInfo_RespectsUseRunAsFalse()
    {
        var psi = StartProcessCommand.BuildStartInfo(@"C:\patch", "a.exe", useRunAs: false);
        Assert.IsTrue(string.IsNullOrEmpty(psi.Verb));
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void BuildStartInfo_KeepsRootedExePath()
    {
        var full = Path.Combine(Path.GetTempPath(), "rooted", "tool.exe");
        var psi = StartProcessCommand.BuildStartInfo(@"C:\other", full, useRunAs: true);
        StringAssert.Contains(psi.Arguments, full);
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void StartProcessWaitTimeoutMs_DefaultsToUnbounded()
    {
        // Review follow-up: default 0 restores legacy infinite WaitForExit (no kill).
        Assert.AreEqual(0, CommonClassLibrary.Config.Instance.StartProcessWaitTimeoutMs);
    }
}
