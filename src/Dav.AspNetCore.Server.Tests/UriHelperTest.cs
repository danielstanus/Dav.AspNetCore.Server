using Xunit;

namespace Dav.AspNetCore.Server.Tests;

public class UriHelperTest
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/test", "/")]
    [InlineData("/test/", "/")]
    [InlineData("/test/file1.txt", "/test/")]
    [InlineData("/test/file1.txt/", "/test/")]
    public void GetParent(string uriString, string expectedParentUriString)
    {
        // arrange
        var uri = UriHelper.CreateUri(uriString);

        // act
        var parentUri = uri.GetParent();

        // assert
        Assert.Equal(UriHelper.CreateUri(expectedParentUriString).LocalPath, parentUri.LocalPath);
    }

    [Theory]
    [InlineData("/test/test.txt", "/test", "/test.txt")]
    [InlineData("/test/test.txt", "/abc", "/abc")]
    [InlineData("/test/test.txt", "/test/test/", "/test/test/")]
    public void GetRelativeUri(string relativeToUriString, string uriString, string expectedUriString)
    {
        // arrange
        var relativeTo = UriHelper.CreateUri(relativeToUriString);
        var uri = UriHelper.CreateUri(uriString);

        // act
        var result = relativeTo.GetRelativeUri(uri);

        // assert
        Assert.Equal(UriHelper.CreateUri(expectedUriString).LocalPath, result.LocalPath);
    }

    [Theory]
    [InlineData("/a:b", "/a:b")]
    [InlineData("/a|b", "/a|b")]
    [InlineData("a:b", "/a:b")]
    public void CreateUri_InvalidPathCharacters_DoesNotThrow(string input, string expectedLocalPath)
    {
        // act
        var uri = UriHelper.CreateUri(input);

        // assert
        Assert.Equal(expectedLocalPath, uri.LocalPath);
    }

    [Theory]
    [InlineData("/dav", "/dav/file.txt", true, "/file.txt")]
    [InlineData("/dav", "/dav/sub/file.txt", true, "/sub/file.txt")]
    [InlineData("/dav", "/dav", true, "/")]
    [InlineData("/dav/", "/dav/file.txt", true, "/file.txt")]
    [InlineData("", "/file.txt", true, "/file.txt")]
    [InlineData("/", "/file.txt", true, "/file.txt")]
    [InlineData("/dav", "/other/file.txt", false, "/other/file.txt")]
    [InlineData("/dav", "/", false, "/")]
    [InlineData("/dav", "/davx/file.txt", false, "/davx/file.txt")]
    public void TryRemovePathBase(
        string pathBase,
        string destinationString,
        bool expectedResult,
        string expectedLocalPath)
    {
        // arrange
        var destination = UriHelper.CreateUri(destinationString);

        // act
        var result = UriHelper.TryRemovePathBase(pathBase, destination, out var stripped);

        // assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(UriHelper.CreateUri(expectedLocalPath).LocalPath, stripped.LocalPath);
    }

    [Theory]
    [InlineData("/dir/", "/dir")]
    [InlineData("/dir", "/dir")]
    [InlineData("/a/b/", "/a/b")]
    [InlineData("/", "/")]
    public void NormalizeLockUri(string input, string expectedLocalPath)
    {
        // act
        var result = UriHelper.NormalizeLockUri(UriHelper.CreateUri(input));

        // assert
        Assert.Equal(UriHelper.CreateUri(expectedLocalPath).LocalPath, result.LocalPath);
    }
}