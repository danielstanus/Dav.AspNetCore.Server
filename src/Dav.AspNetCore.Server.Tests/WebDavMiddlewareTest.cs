using Xunit;

namespace Dav.AspNetCore.Server.Tests;

public class WebDavMiddlewareTest
{
    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("a\rb", "a?b")]
    [InlineData("a\nb", "a?b")]
    [InlineData("a\tb", "a?b")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitizeForLog_ReplacesControlCharacters(string? input, string expected)
    {
        Assert.Equal(expected, WebDavMiddleware.SanitizeForLog(input));
    }
}
