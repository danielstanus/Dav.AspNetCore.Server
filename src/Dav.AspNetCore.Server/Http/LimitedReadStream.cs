namespace Dav.AspNetCore.Server.Http;

/// <summary>
/// Read-only wrapper that fails once more than <paramref name="maxBytes"/> would be read.
/// This protects chunked uploads without a Content-Length from exceeding the configured limit.
/// </summary>
internal sealed class LimitedReadStream : Stream
{
    private readonly Stream inner;
    private readonly long maxBytes;
    private long read;

    /// <summary>
    /// Initializes a new <see cref="LimitedReadStream"/> class.
    /// </summary>
    /// <param name="inner">The inner stream.</param>
    /// <param name="maxBytes">The maximum number of bytes that may be read.</param>
    public LimitedReadStream(Stream inner, long maxBytes)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.maxBytes = maxBytes;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var allowed = GetAllowed(count);
        if (allowed <= 0)
        {
            EnsureEnd();
            return 0;
        }

        var bytesRead = inner.Read(buffer, offset, allowed);
        read += bytesRead;
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var allowed = GetAllowed(buffer.Length);
        if (allowed <= 0)
        {
            await EnsureEndAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        var bytesRead = await inner.ReadAsync(buffer.Slice(0, allowed), cancellationToken).ConfigureAwait(false);
        read += bytesRead;
        return bytesRead;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int ReadByte()
    {
        var buffer = new byte[1];
        return Read(buffer, 0, 1) == 1 ? buffer[0] : -1;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int GetAllowed(int requested)
    {
        var remaining = maxBytes - read;
        if (remaining <= 0)
            return 0;

        return (int)Math.Min(requested, remaining);
    }

    private void EnsureEnd()
    {
        if (inner.ReadByte() != -1)
            throw TooLarge();
    }

    private async ValueTask EnsureEndAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        var bytesRead = await inner.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        if (bytesRead > 0)
            throw TooLarge();
    }

    private static InvalidDataException TooLarge()
        => new("The request body exceeds the maximum allowed resource size.");
}
