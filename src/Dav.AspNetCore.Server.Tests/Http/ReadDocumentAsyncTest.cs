using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Http;

public class ReadDocumentAsyncTest
{
    [Fact]
    public async Task ReadDocumentAsync_WithinLimit_ReturnsDocument()
    {
        var context = CreateContext("<root/>", maxBytes: 1024);

        var document = await context.ReadDocumentAsync();

        Assert.NotNull(document);
        Assert.Equal("root", document!.Root!.Name.LocalName);
    }

    [Fact]
    public async Task ReadDocumentAsync_OverContentLengthLimit_ReturnsNull()
    {
        var body = "<root>" + new string('a', 4096) + "</root>";
        var context = CreateContext(body, maxBytes: 1024);

        Assert.Null(await context.ReadDocumentAsync());
    }

    [Fact]
    public async Task ReadDocumentAsync_ChunkedOverLimit_ReturnsNull()
    {
        var body = "<root>" + new string('a', 4096) + "</root>";
        var context = CreateContext(body, maxBytes: 1024, contentLength: null);

        Assert.Null(await context.ReadDocumentAsync());
    }

    [Fact]
    public async Task ReadDocumentAsync_WithDtd_ReturnsNull()
    {
        var context = CreateContext("<!DOCTYPE root [<!ENTITY x \"y\">]><root>&x;</root>", maxBytes: 1024);

        Assert.Null(await context.ReadDocumentAsync());
    }

    private static HttpContext CreateContext(string xml, long maxBytes, long? contentLength = -1)
    {
        var body = Encoding.UTF8.GetBytes(xml);
        var services = new ServiceCollection();
        services.AddSingleton(new WebDavOptions { MaxXmlRequestBodyBytes = maxBytes });
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.ContentType = "application/xml";
        context.Request.ContentLength = contentLength == -1 ? body.Length : contentLength;
        context.Request.Body = new MemoryStream(body);
        return context;
    }
}
