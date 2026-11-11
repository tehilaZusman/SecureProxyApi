namespace SecureProxy.Api.Middleware;

public sealed class RequestBodySizeTrackingMiddleware
{
    private const string ItemKey = "IncomingRequestBodyBytes";
    private readonly RequestDelegate _next;

    public RequestBodySizeTrackingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var original = context.Request.Body;
        var counting = new CountingReadStream(original);
        context.Request.Body = counting;
        context.Items[ItemKey] = counting;
        try
        {
            await _next(context);
        }
        finally
        {
            context.Items[ItemKey] = counting.BytesRead;
            context.Request.Body = original;
        }
    }

    public static long GetBytesRead(HttpContext context)
    {
        if (!context.Items.TryGetValue(ItemKey, out var value))
            return -1;

        return value switch
        {
            CountingReadStream counter => counter.BytesRead,
            long count => count,
            _ => -1
        };
    }

    private sealed class CountingReadStream : Stream
    {
        private readonly Stream _inner;
        public long BytesRead { get; private set; }

        public CountingReadStream(Stream inner) => _inner = inner;
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }
        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            BytesRead += read;
            return read;
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
            BytesRead += read;
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }
        public override int ReadByte()
        {
            var value = _inner.ReadByte();
            if (value >= 0) BytesRead++;
            return value;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { /* The server owns the underlying request stream. */ }
    }
}
