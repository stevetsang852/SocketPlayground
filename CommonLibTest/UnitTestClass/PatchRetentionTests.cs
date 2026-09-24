using CommonClassLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest;

[TestClass]
public sealed class PatchRetentionTests
{
    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void IsPatchDirectoryName_MatchesLegacyAndCurrentSuffixes()
    {
        Assert.IsTrue(PatchRetention.IsPatchDirectoryName("2026_01_01_00_00_00_patch"));
        Assert.IsTrue(PatchRetention.IsPatchDirectoryName("foo_patch"));
        Assert.IsFalse(PatchRetention.IsPatchDirectoryName("somethingpatch")); // needs underscore
        Assert.IsFalse(PatchRetention.IsPatchDirectoryName("dispatch"));
        Assert.IsFalse(PatchRetention.IsPatchDirectoryName("2026_01_01_exe"));
        Assert.IsFalse(PatchRetention.IsPatchDirectoryName("Graphics"));
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void SelectPatchDirsToDelete_KeepsNewestN_NeverDeletesProtected()
    {
        var root = Path.Combine(Path.GetTempPath(), "patch-ret-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var dirs = new List<string>();
            for (var i = 0; i < 7; i++)
            {
                var d = Path.Combine(root, $"2026_01_0{i}_00_00_00_aaa_patch");
                Directory.CreateDirectory(d);
                Directory.SetLastWriteTimeUtc(d, new DateTime(2026, 1, 1, 0, 0, i, DateTimeKind.Utc));
                dirs.Add(d);
            }

            // Non-patch sibling must never be selected.
            Directory.CreateDirectory(Path.Combine(root, "2026_01_01_00_00_00_bbb_exe"));

            var protect = dirs[^1]; // newest
            var toDelete = PatchRetention.SelectPatchDirsToDelete(root, protect, keepCount: 5);

            // 7 patch dirs, keep newest 5 (includes protect) => delete 2 oldest.
            Assert.AreEqual(2, toDelete.Count);
            Assert.IsFalse(toDelete.Any(p => string.Equals(p, protect, StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(toDelete.Any(p => p.EndsWith("_exe", StringComparison.OrdinalIgnoreCase)));
            CollectionAssert.AreEquivalent(new[] { dirs[0], dirs[1] }, toDelete.ToList());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void SelectPatchDirsToDelete_AlwaysRetainsProtectedEvenIfOld()
    {
        var root = Path.Combine(Path.GetTempPath(), "patch-ret-old-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var oldActive = Path.Combine(root, "old_active_patch");
            Directory.CreateDirectory(oldActive);
            Directory.SetLastWriteTimeUtc(oldActive, DateTime.UtcNow.AddDays(-30));

            for (var i = 0; i < 5; i++)
            {
                var d = Path.Combine(root, $"new_{i}_patch");
                Directory.CreateDirectory(d);
                Directory.SetLastWriteTimeUtc(d, DateTime.UtcNow.AddMinutes(i));
            }

            // keepCount=5 among 6 dirs; newest 5 are the new_* ones, but protect=oldActive must remain.
            var toDelete = PatchRetention.SelectPatchDirsToDelete(root, oldActive, keepCount: 5);
            Assert.AreEqual(0, toDelete.Count, "protect forces 6 retained when keep=5");
            Assert.IsFalse(toDelete.Any(p => string.Equals(p, oldActive, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void Apply_DeletesOnlySelectedDirs()
    {
        var root = Path.Combine(Path.GetTempPath(), "patch-ret-apply-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var keep = new List<string>();
            for (var i = 0; i < 3; i++)
            {
                var d = Path.Combine(root, $"keep_{i}_patch");
                Directory.CreateDirectory(d);
                Directory.SetLastWriteTimeUtc(d, DateTime.UtcNow.AddMinutes(i));
                keep.Add(d);
            }

            var old = Path.Combine(root, "old_0_patch");
            Directory.CreateDirectory(old);
            Directory.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));

            var deleted = PatchRetention.Apply(root, protectFullPath: keep[^1], keepCount: 3);
            Assert.AreEqual(1, deleted.Count);
            Assert.IsFalse(Directory.Exists(old));
            foreach (var d in keep)
            {
                Assert.IsTrue(Directory.Exists(d));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("AutoUpgrade")]
    public void SelectPatchDirsToDelete_NoOpWhenRootMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N"));
        var result = PatchRetention.SelectPatchDirsToDelete(missing, null, 5);
        Assert.AreEqual(0, result.Count);
    }
}
