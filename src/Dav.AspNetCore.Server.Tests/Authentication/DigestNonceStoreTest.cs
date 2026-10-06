using Dav.AspNetCore.Server.Authentication;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Authentication;

public class DigestNonceStoreTest
{
    [Fact]
    public void TryValidate_ValidNonce_ReturnsTrue()
    {
        var (nonce, opaque) = DigestNonceStore.Create();

        Assert.True(DigestNonceStore.TryValidate(nonce, opaque, true, 1, out _));
    }

    [Fact]
    public void TryValidate_ReplayedNonceCount_ReturnsFalse()
    {
        var (nonce, opaque) = DigestNonceStore.Create();
        Assert.True(DigestNonceStore.TryValidate(nonce, opaque, true, 1, out _));

        Assert.False(DigestNonceStore.TryValidate(nonce, opaque, true, 1, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_IncreasingNonceCount_ReturnsTrue()
    {
        var (nonce, opaque) = DigestNonceStore.Create();

        Assert.True(DigestNonceStore.TryValidate(nonce, opaque, true, 1, out _));
        Assert.True(DigestNonceStore.TryValidate(nonce, opaque, true, 2, out _));
    }

    [Fact]
    public void TryValidate_UnknownNonce_ReturnsFalse()
    {
        Assert.False(DigestNonceStore.TryValidate("00000000000000000000000000000000", "opaque", false, 0, out _));
    }

    [Fact]
    public void TryValidate_WrongOpaque_ReturnsFalse()
    {
        var (nonce, _) = DigestNonceStore.Create();

        Assert.False(DigestNonceStore.TryValidate(nonce, "wrong", false, 0, out _));
    }

    [Fact]
    public void Create_ReturnsDistinctNonces()
    {
        var first = DigestNonceStore.Create();
        var second = DigestNonceStore.Create();

        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.NotEqual(first.Opaque, second.Opaque);
    }
}
