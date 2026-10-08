using Dav.AspNetCore.Server.Http;
using Dav.AspNetCore.Server.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

    [Fact]
    public async Task InvokeAsync_AddsSecurityHeaders()
    {
        // arrange
        var options = new WebDavOptions { DisableServerName = true };
        var headers = new HeaderDictionary();
        var httpContext = CreateHttpContext(headers);

        var middleware = new WebDavMiddleware(_ => Task.CompletedTask, options, NullLogger<WebDavMiddleware>.Instance);

        // act
        await middleware.InvokeAsync(httpContext.Object);

        // assert
        Assert.Equal("nosniff", headers["X-Content-Type-Options"].ToString());
        Assert.Equal("sandbox", headers["Content-Security-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_NullContentSecurityPolicy_DoesNotAddTheHeader()
    {
        // arrange
        var options = new WebDavOptions { DisableServerName = true, ContentSecurityPolicy = null };
        var headers = new HeaderDictionary();
        var httpContext = CreateHttpContext(headers);

        var middleware = new WebDavMiddleware(_ => Task.CompletedTask, options, NullLogger<WebDavMiddleware>.Instance);

        // act
        await middleware.InvokeAsync(httpContext.Object);

        // assert
        Assert.True(headers.ContainsKey("X-Content-Type-Options"));
        Assert.False(headers.ContainsKey("Content-Security-Policy"));
    }

    private static Mock<HttpContext> CreateHttpContext(HeaderDictionary headers)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IStore>().Object);

        var httpContext = new Mock<HttpContext>(MockBehavior.Strict);
        httpContext.Setup(s => s.Request.Method).Returns(WebDavMethods.Options);
        httpContext.Setup(s => s.Request.Path).Returns(new PathString("/"));
        httpContext.Setup(s => s.RequestServices).Returns(services.BuildServiceProvider());
        httpContext.Setup(s => s.RequestAborted).Returns(CancellationToken.None);
        httpContext.Setup(s => s.Response.Headers).Returns(headers);
        httpContext.SetupProperty(s => s.Response.StatusCode);

        return httpContext;
    }
}
