using System.Collections.Concurrent;
using System.Net;

namespace SecureProxy.Tests.Infrastructure;

public sealed class RecordingHttpHandler : HttpMessageHandler
{
    public ConcurrentQueue<HttpRequestMessage> ReceivedRequests { get; } = new();

    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Responder { get; set; }

    public bool ReceivedAny => !ReceivedRequests.IsEmpty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ReceivedRequests.Enqueue(request);

        if (Responder is not null)
            return await Responder(request, cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", System.Text.Encoding.UTF8, "application/json")
        };
    }
}
