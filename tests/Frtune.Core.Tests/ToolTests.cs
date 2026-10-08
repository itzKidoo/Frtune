using Frtune.Core.Tools;

namespace Frtune.Core.Tests;

public class ToolTests
{
    [Fact]
    public void Folder_target_measures_and_cleans_including_wildcard_profiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "frtune-clean-" + Guid.NewGuid().ToString("N"));
        try
        {
            var p1 = Directory.CreateDirectory(Path.Combine(root, "User Data", "Default", "Cache")).FullName;
            var p2 = Directory.CreateDirectory(Path.Combine(root, "User Data", "Profile 1", "Cache", "sub")).FullName;
            File.WriteAllBytes(Path.Combine(p1, "a"), new byte[100]);
            File.WriteAllBytes(Path.Combine(p2, "b"), new byte[50]);
            var keep = Path.Combine(root, "User Data", "Default", "Bookmarks");
            File.WriteAllBytes(keep, new byte[10]);

            var target = new FolderTarget(new[] { Path.Combine(root, "User Data", "*", "Cache") });
            Assert.Equal(150, target.Measure(default));
            var result = target.Clean(null, default);

            Assert.Equal(150, result.BytesFreed);
            Assert.Equal(2, result.FilesDeleted);
            Assert.True(File.Exists(keep));
            Assert.True(Directory.Exists(p1)); // root folder kept, only contents removed
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Folder_target_respects_minimum_age()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "frtune-age-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(root, "new"), new byte[10]);
            var old = Path.Combine(root, "old");
            File.WriteAllBytes(old, new byte[10]);
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-3));

            var r = new FolderTarget(new[] { root }, minAge: TimeSpan.FromDays(1)).Clean(null, default);
            Assert.Equal(1, r.FilesDeleted);
            Assert.True(File.Exists(Path.Combine(root, "new")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Duplicate_finder_groups_identical_files()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "frtune-dup-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "a.txt"), "same content");
            File.WriteAllText(Path.Combine(root, "b.txt"), "same content");
            File.WriteAllText(Path.Combine(root, "c.txt"), "diff content");
            var groups = FileFinder.Duplicates(root, 1, null, default);
            Assert.Single(groups);
            Assert.Equal(2, groups[0].Paths.Count);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Health_score_is_explainable()
    {
        var r = HealthScore.Compute(40, 50, 4, 10, 10, 0, false);
        Assert.Equal(100, r.Score);
        Assert.Equal(r.Score, r.Factors.Sum(f => f.Score));
        Assert.True(HealthScore.Compute(5, 95, 30, 10, 0, 20L << 30, true).Score < 20);
    }

    [Fact]
    public void Protected_apps_are_never_suggested()
    {
        Assert.True(AppxCatalog.IsProtected("Microsoft.WindowsStore"));
        Assert.True(AppxCatalog.IsProtected("Microsoft.WindowsCalculator"));
        Assert.True(AppxCatalog.IsProtected("Microsoft.Windows.Photos"));
        Assert.True(AppxCatalog.IsProtected("Microsoft.WindowsTerminal"));
        Assert.DoesNotContain(AppxCatalog.Suggested.Keys, AppxCatalog.IsProtected);
    }
}
