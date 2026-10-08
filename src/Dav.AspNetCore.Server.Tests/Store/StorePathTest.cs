using Dav.AspNetCore.Server.Store;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Store;

public class StorePathTest
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "dav-store-path-tests");

    [Fact]
    public void Resolve_RelativePath_StaysInsideRoot()
    {
        var path = StorePath.Resolve(root, "/folder/file.txt");

        Assert.Equal(Path.Combine(Path.GetFullPath(root), "folder", "file.txt"), path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void Resolve_EmptyPath_ReturnsRoot(string relativePath)
    {
        var path = StorePath.Resolve(root, relativePath);

        Assert.Equal(Path.GetFullPath(root), path);
    }

    [Fact]
    public void Resolve_DotDot_Throws()
    {
        Assert.Throws<UnauthorizedAccessException>(() => StorePath.Resolve(root, "a/../../b"));
    }

    [Theory]
    [InlineData("//server/share/file.txt")]
    [InlineData(@"\\server\share\file.txt")]
    public void Resolve_UncPath_Throws(string relativePath)
    {
        Assert.Throws<UnauthorizedAccessException>(() => StorePath.Resolve(root, relativePath));
    }

    [Theory]
    [InlineData("C:/Windows/win.ini")]
    [InlineData(@"C:\Windows\win.ini")]
    public void Resolve_DriveLetterPath_ThrowsOnWindows(string relativePath)
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Throws<UnauthorizedAccessException>(() => StorePath.Resolve(root, relativePath));
    }

    [Fact]
    public void Resolve_AlternateDataStream_ThrowsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Throws<UnauthorizedAccessException>(() => StorePath.Resolve(root, "/file.txt:stream"));
    }

    [Theory]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a\"b")]
    [InlineData("/dir/a*b.txt")]
    public void Resolve_InvalidWindowsPathCharacters_ThrowUnauthorizedAccess(string relativePath)
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Throws<UnauthorizedAccessException>(() => StorePath.Resolve(root, relativePath));
    }

    [Fact]
    public void Resolve_SymlinkPointingOutsideRoot_Throws()
    {
        var linkRoot = Path.Combine(Path.GetTempPath(), $"dav-link-root-{Guid.NewGuid():N}");
        var outside = Path.Combine(Path.GetTempPath(), $"dav-link-outside-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(linkRoot);
        System.IO.Directory.CreateDirectory(outside);

        try
        {
            if (!TryCreateDirectoryLink(Path.Combine(linkRoot, "link"), outside))
                return;

            Assert.Throws<UnauthorizedAccessException>(() => StorePath.Resolve(linkRoot, "/link/file.txt"));
        }
        finally
        {
            TryDeleteDirectory(linkRoot);
            TryDeleteDirectory(outside);
        }
    }

    [Fact]
    public void Resolve_SymlinkPointingInsideRoot_IsAllowed()
    {
        var linkRoot = Path.Combine(Path.GetTempPath(), $"dav-link-root-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(Path.Combine(linkRoot, "target"));

        try
        {
            if (!TryCreateDirectoryLink(Path.Combine(linkRoot, "link"), Path.Combine(linkRoot, "target")))
                return;

            var path = StorePath.Resolve(linkRoot, "/link/file.txt");

            Assert.Equal(Path.Combine(linkRoot, "link", "file.txt"), path);
        }
        finally
        {
            TryDeleteDirectory(linkRoot);
        }
    }

    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
        => TestDirectoryLink.TryCreate(linkPath, targetPath);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (System.IO.Directory.Exists(path))
                System.IO.Directory.Delete(path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void Resolve_EmptyRoot_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => StorePath.Resolve(string.Empty, "/file.txt"));
    }
}
