using Dav.AspNetCore.Server.Locks;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Locks;

public class LockTimeoutTest
{
    private static readonly TimeSpan Max = TimeSpan.FromHours(1);

    [Fact]
    public void Resolve_NoMax_InfiniteRequested_ReturnsZero()
    {
        var result = LockTimeout.Resolve(new[] { TimeSpan.Zero, TimeSpan.FromMinutes(30) }, null);

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void Resolve_NoMax_ReturnsLargestRequested()
    {
        var result = LockTimeout.Resolve(new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30) }, null);

        Assert.Equal(TimeSpan.FromMinutes(30), result);
    }

    [Fact]
    public void Resolve_WithMax_InfiniteRequested_ReturnsMax()
    {
        var result = LockTimeout.Resolve(new[] { TimeSpan.Zero }, Max);

        Assert.Equal(Max, result);
    }

    [Fact]
    public void Resolve_WithMax_LargerRequested_ReturnsMax()
    {
        var result = LockTimeout.Resolve(new[] { TimeSpan.FromHours(5) }, Max);

        Assert.Equal(Max, result);
    }

    [Fact]
    public void Resolve_WithMax_SmallerRequested_ReturnsRequested()
    {
        var result = LockTimeout.Resolve(new[] { TimeSpan.FromMinutes(15) }, Max);

        Assert.Equal(TimeSpan.FromMinutes(15), result);
    }

    [Fact]
    public void Resolve_WithMax_MixedInfiniteAndFinite_ReturnsLargestFinite()
    {
        var result = LockTimeout.Resolve(new[] { TimeSpan.Zero, TimeSpan.FromMinutes(10) }, Max);

        Assert.Equal(TimeSpan.FromMinutes(10), result);
    }

    [Fact]
    public void Resolve_Empty_ReturnsMax()
    {
        var result = LockTimeout.Resolve(Array.Empty<TimeSpan>(), Max);

        Assert.Equal(Max, result);
    }
}
