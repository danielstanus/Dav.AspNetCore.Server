using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Dav.AspNetCore.Server.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Authentication;

public class DigestAuthenticationHandlerTest
{
    [Fact]
    public async Task AuthenticateAsync_MissingQop_Fails()
    {
        // arrange: a response without qop has no nonce-count and could be replayed.
        var options = new DigestAuthenticationSchemeOptions { Realm = "test" };
        var (nonce, opaque) = DigestNonceStore.Create();
        var context = CreateContext();
        var handler = await CreateHandlerAsync(options, context);

        context.Request.Headers.Authorization =
            $"Digest username=\"u\", realm=\"test\", nonce=\"{nonce}\", uri=\"/file.txt\", " +
            $"response=\"{new string('0', 32)}\", opaque=\"{opaque}\"";

        // act
        var result = await handler.AuthenticateAsync();

        // assert
        Assert.False(result.Succeeded);
        Assert.Equal("Unsupported quality of protection parameter.", result.Failure?.Message);
    }

    [Fact]
    public async Task AuthenticateAsync_MissingNonceCount_Fails()
    {
        // arrange
        var options = new DigestAuthenticationSchemeOptions { Realm = "test" };
        var (nonce, opaque) = DigestNonceStore.Create();
        var context = CreateContext();
        var handler = await CreateHandlerAsync(options, context);

        context.Request.Headers.Authorization =
            $"Digest username=\"u\", realm=\"test\", nonce=\"{nonce}\", uri=\"/file.txt\", " +
            $"response=\"{new string('0', 32)}\", opaque=\"{opaque}\", qop=\"auth\"";

        // act
        var result = await handler.AuthenticateAsync();

        // assert
        Assert.False(result.Succeeded);
        Assert.Equal("Missing nonce count or client nonce.", result.Failure?.Message);
    }

    [Fact]
    public async Task AuthenticateAsync_ValidQopAuthResponse_Succeeds()
    {
        // arrange
        var options = new DigestAuthenticationSchemeOptions { Realm = "test" };
        options.Events.OnPasswordRequested = (_, _) => Task.FromResult<string?>("pw");

        var (nonce, opaque) = DigestNonceStore.Create();
        var context = CreateContext();
        var handler = await CreateHandlerAsync(options, context);

        const string nonceCount = "00000001";
        const string clientNonce = "cnonce123";
        var ha1 = Md5("u:test:pw");
        var ha2 = Md5("GET:/file.txt");
        var response = Md5($"{ha1}:{nonce}:{nonceCount}:{clientNonce}:auth:{ha2}");

        context.Request.Headers.Authorization =
            $"Digest username=\"u\", realm=\"test\", nonce=\"{nonce}\", uri=\"/file.txt\", " +
            $"response=\"{response}\", opaque=\"{opaque}\", qop=\"auth\", nc={nonceCount}, cnonce=\"{clientNonce}\"";

        // act
        var result = await handler.AuthenticateAsync();

        // assert
        Assert.True(result.Succeeded);
    }

    private static async Task<DigestAuthenticationHandler> CreateHandlerAsync(
        DigestAuthenticationSchemeOptions options,
        HttpContext context)
    {
        var monitor = new Mock<IOptionsMonitor<DigestAuthenticationSchemeOptions>>();
        monitor.Setup(x => x.Get(It.IsAny<string>())).Returns(options);
        monitor.SetupGet(x => x.CurrentValue).Returns(options);

        var handler = new DigestAuthenticationHandler(monitor.Object, NullLoggerFactory.Instance, UrlEncoder.Default);
        await handler.InitializeAsync(
            new AuthenticationScheme(
                DigestAuthenticationDefaults.AuthenticationScheme,
                DigestAuthenticationDefaults.AuthenticationScheme,
                typeof(DigestAuthenticationHandler)),
            context);

        return handler;
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/file.txt";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        return context;
    }

    private static string Md5(string input)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}
