using System.Text;
using Dav.AspNetCore.Server.Http;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Http;

public class LimitedReadStreamTest
{
    [Fact]
    public async Task ReadAsync_WithinLimit_Succeeds()
    {
        var stream = new LimitedReadStream(new MemoryStream(Encoding.UTF8.GetBytes("123")), 3);
        using var output = new MemoryStream();

        await stream.CopyToAsync(output);

        Assert.Equal("123", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task ReadAsync_NullContentLengthOverLimit_Throws()
    {
        var stream = new LimitedReadStream(new MemoryStream(Encoding.UTF8.GetBytes("12345")), 3);
        using var output = new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(() => stream.CopyToAsync(output));
    }

    [Fact]
    public void Read_SyncOverLimit_Throws()
    {
        var stream = new LimitedReadStream(new MemoryStream(Encoding.UTF8.GetBytes("12345")), 3);
        var buffer = new byte[10];

        Assert.Equal(3, stream.Read(buffer, 0, buffer.Length));
        Assert.Throws<InvalidDataException>(() => stream.Read(buffer, 0, buffer.Length));
    }
}
