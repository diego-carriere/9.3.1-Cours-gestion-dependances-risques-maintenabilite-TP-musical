using System.Net;
using Microsoft.Extensions.Logging;

namespace ReveilMusical.Api.E2ETests;

public sealed class WakeUpEndpointTests
{
    [Fact]
    public async Task Nominal_wake_up_finds_a_track_on_itunes_and_pushes_it()
    {
        await using var factory = new ReveilApiFactory();
        factory.Upstream.FixtureFor(FakeUpstream.ITunesHost, "itunes-search-soleil.json", "text/javascript");

        var response = await factory.CreateClient().WakeUpAsync("42", "MARDI", "SOLEIL");
        var json = await response.JsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Soleil", json.Str("track.title"));
        Assert.Equal("GIMS", json.Str("track.artist"));
        Assert.Equal("catalog", json.Str("track.source"));
        Assert.Equal("weather", json.Str("track.preference"));
        Assert.Equal("soleil", json.Str("track.keyword"));
        Assert.Equal("push", json.Str("notification.deliveredOn"));
        Assert.Equal("delivered", json.Str("notification.attempts.0.status"));
        Assert.Equal("true", json.Str("delivered"));
        Assert.Equal("false", json.Str("degraded"));
        Assert.False(json.GetRawText().Contains("trackViewUrl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Ce mardi s'annonce ensoleillé : réveil en musique avec « Soleil » de GIMS.", factory.Outbox("push.log"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_day_override_is_used_on_that_day()
    {
        await using var factory = new ReveilApiFactory();
        factory.Upstream.FixtureFor(FakeUpstream.ITunesHost, "itunes-search-soleil.json");

        var json = await (await factory.CreateClient().WakeUpAsync("42", "lundi", "pluie")).JsonAsync();

        Assert.Equal("day-and-weather", json.Str("track.preference"));
        Assert.Equal("monday", json.Str("track.keyword"));
        Assert.Contains("term=monday", factory.Upstream.Requests.Single().RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_itunes_outage_fails_over_to_musicbrainz()
    {
        await using var factory = new ReveilApiFactory();
        factory.Upstream
            .FailFor(FakeUpstream.ITunesHost, times: 2)
            .FixtureFor(FakeUpstream.MusicBrainzHost, "musicbrainz-recording-soleil.json");

        var json = await (await factory.CreateClient().WakeUpAsync("42", "MARDI", "SOLEIL")).JsonAsync();

        Assert.Equal("soleil soleil soleil", json.Str("track.title"));
        Assert.Equal("catalog", json.Str("track.source"));
        Assert.Equal("false", json.Str("degraded"));
        Assert.StartsWith("ReveilMusical-E2E/1.0", factory.Upstream.Requests.Last().Headers.UserAgent.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_every_provider_down_the_local_playlist_still_wakes_the_user()
    {
        await using var factory = new ReveilApiFactory();
        factory.Upstream.FailFor(FakeUpstream.ITunesHost, times: 2).FailFor(FakeUpstream.MusicBrainzHost, times: 2);

        var response = await factory.CreateClient().WakeUpAsync("42", "MARDI", "SOLEIL");
        var json = await response.JsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("local-playlist", json.Str("track.source"));
        Assert.Equal("Here Comes the Sun", json.Str("track.title"));
        Assert.Equal("null", json.Str("track.keyword"));
        Assert.Equal("true", json.Str("degraded"));
        Assert.Equal("true", json.Str("delivered"));
    }

    [Fact]
    public async Task A_preferred_channel_outage_cascades_to_the_next_channel()
    {
        await using var factory = new ReveilApiFactory(new Dictionary<string, string?> { ["Vendors:Push:SimulateOutage"] = "true" });
        factory.Upstream.FixtureFor(FakeUpstream.ITunesHost, "itunes-search-soleil.json");

        var json = await (await factory.CreateClient().WakeUpAsync("42", "MARDI", "SOLEIL")).JsonAsync();

        Assert.Equal("sms", json.Str("notification.deliveredOn"));
        Assert.Equal("failed", json.Str("notification.attempts.0.status"));
        Assert.Equal("delivered", json.Str("notification.attempts.1.status"));
        Assert.Equal("true", json.Str("degraded"));
        Assert.Contains("+33612345678", factory.Outbox("sms.log"), StringComparison.Ordinal);
        Assert.Empty(factory.Outbox("push.log"));
    }

    [Fact]
    public async Task When_every_channel_is_down_the_operator_is_alerted_and_the_scheduler_told_to_retry()
    {
        await using var factory = new ReveilApiFactory(new Dictionary<string, string?>
        {
            ["Vendors:Push:SimulateOutage"] = "true",
            ["Vendors:Sms:SimulateOutage"] = "true",
            ["Vendors:Mail:SimulateOutage"] = "true",
        });
        factory.Upstream.FixtureFor(FakeUpstream.ITunesHost, "itunes-search-soleil.json");

        var response = await factory.CreateClient().WakeUpAsync("42", "MARDI", "SOLEIL");
        var json = await response.JsonAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
        Assert.Equal("false", json.Str("delivered"));
        Assert.Equal("true", json.Str("notification.operatorAlerted"));
        Assert.Contains(factory.Logs.Entries, e => e.Level == LogLevel.Critical && e.Message.Contains("user 42", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"day":"MARDI","weather":"SOLEIL"}""", "userId")]
    [InlineData("""{"userId":"42","day":"MARDI","weather":"BROUILLARD"}""", "weather")]
    [InlineData("""{"userId":"42","day":"MONDAY","weather":"SOLEIL"}""", "day")]
    [InlineData("""{"userId":"42"}""", "weather")]
    public async Task An_invalid_request_is_a_400_naming_the_faulty_field(string body, string field)
    {
        await using var factory = new ReveilApiFactory();

        var response = await factory.CreateClient().PostAsync(
            "/wake-ups", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        var json = await response.JsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(json.GetProperty("errors").TryGetProperty(field, out _), json.GetRawText());
        Assert.Empty(factory.Upstream.Requests);
    }

    [Fact]
    public async Task A_malformed_json_body_is_a_400()
    {
        await using var factory = new ReveilApiFactory();

        var response = await factory.CreateClient().PostAsync(
            "/wake-ups", new StringContent("{ pas du json", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_user_is_a_404()
    {
        await using var factory = new ReveilApiFactory();

        var response = await factory.CreateClient().WakeUpAsync("inconnu", "MARDI", "SOLEIL");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_user_service_outage_is_a_503_with_retry_after()
    {
        await using var factory = new ReveilApiFactory(new Dictionary<string, string?> { ["UserService:SimulateOutage"] = "true" });

        var response = await factory.CreateClient().WakeUpAsync("42", "MARDI", "SOLEIL");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task The_health_endpoint_answers()
    {
        await using var factory = new ReveilApiFactory();

        var response = await factory.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
