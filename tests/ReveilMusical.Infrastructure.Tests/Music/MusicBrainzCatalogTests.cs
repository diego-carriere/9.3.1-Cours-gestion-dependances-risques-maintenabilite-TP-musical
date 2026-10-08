using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Music;

public sealed class MusicBrainzCatalogTests : MusicCatalogContractTests, IDisposable
{
    private const string UserAgent = "ReveilMusical/1.0 ( https://example.test/contact )";

    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task Recordings_are_mapped_to_title_and_artist()
    {
        _handler.Enqueue(Fixture.Json(Fixture.Read("musicbrainz-recording-soleil.json")));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal(
            [new Track("soleil soleil soleil", "memo montañez"), new Track("Soleil Soleil", "Ilya"), new Track("Soleil, Soleil", "Ilya")],
            result.Value);
    }

    [Fact]
    public async Task A_multi_artist_credit_is_rebuilt_with_its_join_phrases()
    {
        _handler.Enqueue(Fixture.Json("""
            {"recordings":[{"title":"The Boxer","artist-credit":[
              {"name":"Paul Simon","joinphrase":" & "},
              {"name":"Art Garfunkel","joinphrase":""}]}]}
            """));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal([new Track("The Boxer", "Paul Simon & Art Garfunkel")], result.Value);
    }

    [Fact]
    public async Task Every_request_carries_the_identifiable_user_agent_from_configuration()
    {
        _handler.Enqueue(Fixture.Json("""{"recordings":[]}"""));

        await CreateSut().SearchAsync(Keyword.Create("beau temps").Value, TestContext.Current.CancellationToken);

        var request = _handler.Requests.Single();
        Assert.Equal(UserAgent, string.Join(' ', request.Headers.GetValues("User-Agent")));
        Assert.Equal("/ws/2/recording", request.RequestUri!.AbsolutePath);
        Assert.Equal("?query=beau%20temps&fmt=json&limit=5", request.RequestUri.Query);
    }

    [Fact]
    public async Task Recordings_without_artist_credit_are_skipped()
    {
        _handler.Enqueue(Fixture.Json("""{"recordings":[{"title":"Orphelin"},{"title":"Ok","artist-credit":[{"name":"A"}]}]}"""));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal([new Track("Ok", "A")], result.Value);
    }

    [Fact]
    public async Task A_rejected_request_is_an_unavailable_provider()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ProviderUnavailable, result.Error.Kind);
        Assert.Contains("403", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_malformed_body_is_an_unavailable_provider()
    {
        _handler.Enqueue(Fixture.Json("<html>"));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ProviderUnavailable, result.Error.Kind);
    }

    public void Dispose()
    {
        _handler.Dispose();
    }

    protected override IMusicCatalog CreateSutWhoseProviderIsDown()
    {
        _handler.Enqueue(_ => throw new HttpRequestException("connexion refusée"));
        return CreateSut();
    }

    protected override IMusicCatalog CreateSutWithNoMatch()
    {
        _handler.Enqueue(Fixture.Json("""{"count":0,"recordings":[]}"""));
        return CreateSut();
    }

    private MusicBrainzCatalog CreateSut() => new(
        new HttpClient(_handler) { BaseAddress = new Uri("https://musicbrainz.test/") },
        Microsoft.Extensions.Options.Options.Create(new MusicBrainzOptions { UserAgent = UserAgent }),
        NullLogger<MusicBrainzCatalog>.Instance);
}
