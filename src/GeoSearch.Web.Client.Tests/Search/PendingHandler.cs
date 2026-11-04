using System.Net;
using System.Text;

namespace GeoSearch.Web.Client.Tests.Search;

internal sealed class PendingHandler : HttpMessageHandler
{
    private TaskCompletionSource<HttpResponseMessage> _response = CreateResponseSource();

    public int RequestCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequest = request;
        return _response.Task.WaitAsync(cancellationToken);
    }

    public void Complete(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        // The HttpClient caller owns and disposes the response once delivered.
#pragma warning disable CA2000
        _response.TrySetResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
#pragma warning restore CA2000
        _response = CreateResponseSource();
    }

    private static TaskCompletionSource<HttpResponseMessage> CreateResponseSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
