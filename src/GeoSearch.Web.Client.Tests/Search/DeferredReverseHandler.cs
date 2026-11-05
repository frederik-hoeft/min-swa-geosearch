using System.Net;
using System.Text;

namespace GeoSearch.Web.Client.Tests.Search;

/// <summary>Allows reverse HTTP completions to be delivered in any order.</summary>
internal sealed class DeferredReverseHandler : HttpMessageHandler
{
    private readonly List<TaskCompletionSource<HttpResponseMessage>> _responses = [];

    public int RequestCount => _responses.Count;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _responses.Add(response);
        // Deliberately ignore cancellation: exercise the state-generation guard
        // even when the transport delivers an obsolete response after cancellation.
        return response.Task;
    }

    public void Complete(int index, string json)
    {
#pragma warning disable CA2000
        _responses[index].TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
#pragma warning restore CA2000
    }
}
