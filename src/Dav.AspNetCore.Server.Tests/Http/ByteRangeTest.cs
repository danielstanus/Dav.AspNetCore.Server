using Dav.AspNetCore.Server.Http;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Http;

public class ByteRangeTest
{
    [Theory]
    [InlineData(100L, 0L, 9L, 0L, 9L)]
    [InlineData(100L, 50L, null, 50L, 99L)]
    [InlineData(100L, null, 10L, 90L, 99L)]
    [InlineData(100L, 0L, 999L, 0L, 99L)]
    [InlineData(100L, null, 500L, 0L, 99L)]
    [InlineData(1L, 0L, 0L, 0L, 0L)]
    public void TryResolve_Valid(long length, long? from, long? to, long expectedStart, long expectedEnd)
    {
        Assert.True(ByteRange.TryResolve(length, from, to, out var start, out var end));
        Assert.Equal(expectedStart, start);
        Assert.Equal(expectedEnd, end);
    }

    [Theory]
    [InlineData(0L, 0L, 10L)]
    [InlineData(100L, 100L, null)]
    [InlineData(100L, 50L, 40L)]
    [InlineData(100L, null, 0L)]
    [InlineData(100L, null, null)]
    public void TryResolve_Invalid(long length, long? from, long? to)
    {
        Assert.False(ByteRange.TryResolve(length, from, to, out _, out _));
    }
}
