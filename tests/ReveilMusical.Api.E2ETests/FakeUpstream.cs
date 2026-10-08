using System.Net;
using System.Text;

namespace ReveilMusical.Api.E2ETests;

/// <summary>
/// Route les requêtes HTTP sortantes par hôte vers des réponses scriptées et garde chaque requête.
/// Remplace le seul <see cref="HttpMessageHandler"/> primaire : client typé, désérialisation,
/// pipeline Polly, cache, failover, conteneur et routage restent réels au-dessus.
/// </summary>
public sealed class FakeUpstream
{
    public const string ITunesHost = "itunes.test";
    public const string MusicBrainzHost = "musicbrainz.test";

    private readonly Dictionary<string, Queue<Func<HttpRequestMessage, HttpResponseMessage>>> _responsesByHost =
        new(StringComparer.OrdinalIgnoreCase);

    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeUpstream EnqueueFor(string host, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        if (!_responsesByHost.TryGetValue(host, out var queue))
        {
            queue = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
            _responsesByHost[host] = queue;
        }

        queue.Enqueue(responder);
        return this;
    }

    public FakeUpstream FixtureFor(string host, string fixture, string mediaType = "application/json") =>
        EnqueueFor(host, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture)), Encoding.UTF8, mediaType),
        });

    public FakeUpstream FailFor(string host, int times, HttpStatusCode status = HttpStatusCode.InternalServerError)
    {
        for (var i = 0; i < times; i++)
        {
            EnqueueFor(host, _ => new HttpResponseMessage(status));
        }

        return this;
    }

    public int CallCountFor(string host) =>
        Requests.Count(r => string.Equals(r.RequestUri?.Host, host, StringComparison.OrdinalIgnoreCase));

    public HttpMessageHandler CreateHandler() => new RoutingHandler(this);

    private sealed class RoutingHandler(FakeUpstream upstream) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            upstream.Requests.Add(request);
            var host = request.RequestUri?.Host ?? throw new InvalidOperationException("Request has no URI.");

            if (!upstream._responsesByHost.TryGetValue(host, out var queue) || queue.Count == 0)
            {
                throw new InvalidOperationException($"{nameof(FakeUpstream)} has no scripted response left for host '{host}'.");
            }

            return Task.FromResult(queue.Dequeue()(request));
        }
    }
}
