using System.Text;
using System.Text.Json;
using CommonClassLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Payload;

namespace CommonLibTest;

[TestClass]
public sealed class UploadChecksumTests
{
    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void ComputeSha256Hex_IsStableLowercaseHex()
    {
        var bytes = Encoding.UTF8.GetBytes("hello-upgrade");
        var hex = BytesHelper.ComputeSha256Hex(bytes);
        Assert.AreEqual(64, hex.Length);
        Assert.AreEqual(hex, hex.ToLowerInvariant());
        Assert.AreEqual(hex, BytesHelper.ComputeSha256Hex(bytes));
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void TryValidateSha256_AcceptsMatchingIgnoringCaseAnd0xPrefix()
    {
        var bytes = Encoding.UTF8.GetBytes("payload");
        var hex = BytesHelper.ComputeSha256Hex(bytes);
        Assert.IsTrue(BytesHelper.TryValidateSha256(bytes, hex.ToUpperInvariant(), out _));
        Assert.IsTrue(BytesHelper.TryValidateSha256(bytes, "0x" + hex, out _));
        Assert.IsTrue(BytesHelper.TryValidateSha256(bytes, null, out var actual));
        Assert.AreEqual(hex, actual);
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void TryValidateSha256_RejectsMismatch()
    {
        var bytes = Encoding.UTF8.GetBytes("payload");
        Assert.IsFalse(BytesHelper.TryValidateSha256(bytes, new string('a', 64), out var actual));
        Assert.AreEqual(64, actual.Length);
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void HandleUpload_RejectsSha256Mismatch_WithoutWritingFile()
    {
        var bridge = new LegacyCommandBridge();
        var tempDir = Path.Combine(Path.GetTempPath(), "upload-sha-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var bytes = Encoding.UTF8.GetBytes("tampered-or-wrong");
            // Use action=upload + path=tempDir so a successful save WOULD write here;
            // mismatch must reject before SaveFile, leaving tempDir empty.
            var args = JsonSerializer.SerializeToElement(new
            {
                data = new
                {
                    name = "note.txt",
                    action = "upload",
                    path = tempDir,
                    fileBase64 = Convert.ToBase64String(bytes),
                    sha256 = new string('0', 64)
                }
            });

            var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
            Assert.AreEqual("rejected", result.GetProperty("status").GetString());
            StringAssert.Contains(result.GetProperty("reason").GetString(), "sha256 mismatch");
            Assert.AreEqual(0, Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Length);
            Assert.IsFalse(File.Exists(Path.Combine(tempDir, "note.txt")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void HandleUpload_AcceptsMatchingSha256_ForPlainUpload()
    {
        var bridge = new LegacyCommandBridge();
        var tempDir = Path.Combine(Path.GetTempPath(), "upload-sha-ok-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var bytes = Encoding.UTF8.GetBytes("verified-bytes");
            var hex = BytesHelper.ComputeSha256Hex(bytes);
            var args = JsonSerializer.SerializeToElement(new
            {
                data = new
                {
                    name = "note.txt",
                    action = "upload",
                    path = tempDir,
                    fileBase64 = Convert.ToBase64String(bytes),
                    sha256 = hex
                }
            });

            var result = JsonSerializer.SerializeToElement(bridge.HandleUpload(args));
            Assert.AreEqual("saved", result.GetProperty("status").GetString());
            Assert.AreEqual("verified-bytes", File.ReadAllText(Path.Combine(tempDir, "note.txt")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
