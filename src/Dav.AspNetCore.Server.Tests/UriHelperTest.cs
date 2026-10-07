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
}