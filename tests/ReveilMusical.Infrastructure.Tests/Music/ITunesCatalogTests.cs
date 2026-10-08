using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Music;

public sealed class ITunesCatalogTests : MusicCatalogContractTests, IDisposable
{
    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task Results_are_mapped_to_title_and_artist_only()
    {
        // iTunes répond en text/javascript, pas en application/json : l'adaptateur doit l'accepter.
        _handler.Enqueue(Fixture.Json(Fixture.Read("itunes-search-soleil.json"), "text/javascript"));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal(
            [new Track("Soleil", "GIMS"), new Track("SOLEIL", "Naïka"), new Track("Soleil", "Françoise Hardy")],
            result.Value);
    }

    [Fact]
    public async Task The_query_follows_the_documented_search_contract()
    {
        _handler.Enqueue(Fixture.Json("""{"resultCount":0,"results":[]}"""));

        await CreateSut().SearchAsync(Keyword.Create("beau temps").Value, TestContext.Current.CancellationToken);

        var uri = _handler.Requests.Single().RequestUri!;
        Assert.Equal("/search", uri.AbsolutePath);
        Assert.Equal("?term=beau%20temps&media=music&entity=song&limit=5&country=FR", uri.Query);
    }

    [Fact]
    public async Task Results_without_title_or_artist_are_skipped()
    {
        _handler.Enqueue(Fixture.Json("""
            {"resultCount":3,"results":[
              {"trackName":"Soleil","artistName":"GIMS"},
              {"trackName":"","artistName":"X"},
              {"artistName":"Sans titre"}]}
            """));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal([new Track("Soleil", "GIMS")], result.Value);
    }

    [Theory]
    [InlineData("{ pas du json")]
    [InlineData("null")]
    [InlineData("""{"resultCount":0}""")]
    public async Task An_unreadable_body_is_an_unavailable_provider(string body)
    {
        _handler.Enqueue(Fixture.Json(body));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ProviderUnavailable, result.Error.Kind);
    }

    [Fact]
    public async Task A_network_failure_is_an_unavailable_provider()
    {
        _handler.Enqueue(_ => throw new HttpRequestException("DNS"));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ProviderUnavailable, result.Error.Kind);
    }

    [Fact]
    public async Task A_timeout_that_the_caller_did_not_request_is_a_timeout()
    {
        _handler.Enqueue(_ => throw new TaskCanceledException("délai HttpClient"));

        var result = await CreateSut().SearchAsync(SampleKeyword, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Timeout, result.Error.Kind);
    }

    public void Dispose()
    {
        _handler.Dispose();
    }

    protected override IMusicCatalog CreateSutWhoseProviderIsDown()
    {
        _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        return CreateSut();
    }

    protected override IMusicCatalog CreateSutWithNoMatch()
    {
        _handler.Enqueue(Fixture.Json("""{"resultCount":0,"results":[]}"""));
        return CreateSut();
    }

    private ITunesCatalog CreateSut() => new(
        new HttpClient(_handler) { BaseAddress = new Uri("https://itunes.test/") },
        Microsoft.Extensions.Options.Options.Create(new ITunesOptions()),
        NullLogger<ITunesCatalog>.Instance);
}
