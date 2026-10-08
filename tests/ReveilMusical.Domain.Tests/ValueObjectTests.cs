using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Tests;

public sealed class TrackRequestTests
{
    [Fact]
    public void Create_trims_and_collapses_inner_whitespace()
    {
        var request = TrackRequest.Create("  Here   Comes the Sun ", " The   Beatles ").Value;

        Assert.Equal("Here Comes the Sun", request.Title);
        Assert.Equal("The Beatles", request.Artist);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_title(string? title)
    {
        var result = TrackRequest.Create(title, "The Beatles");

        Assert.Equal(ErrorKind.InvalidRequest, result.Error.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void The_artist_is_optional(string? artist)
    {
        var request = TrackRequest.Create("Je veux", artist).Value;

        Assert.Null(request.Artist);
        Assert.Equal("Je veux", request.ToString());
    }

    [Fact]
    public void Create_rejects_a_title_or_an_artist_that_is_too_long()
    {
        var tooLong = new string('a', TrackRequest.MaxLength + 1);

        Assert.True(TrackRequest.Create(tooLong).IsFailure);
        Assert.True(TrackRequest.Create("Je veux", tooLong).IsFailure);
    }

    [Fact]
    public void Normalized_ignores_case_so_two_spellings_share_a_cache_entry()
    {
        Assert.Equal(
            TrackRequest.Create("Here Comes The Sun", "the beatles").Value.Normalized,
            TrackRequest.Create("here comes the sun", "The Beatles").Value.Normalized);
    }

    [Fact]
    public void Normalized_keeps_title_and_artist_apart()
    {
        Assert.NotEqual(
            TrackRequest.Create("Wake Me Up Avicii").Value.Normalized,
            TrackRequest.Create("Wake Me Up", "Avicii").Value.Normalized);
    }

    [Fact]
    public void ToString_shows_the_title_then_the_artist()
    {
        Assert.Equal("Wake Me Up — Avicii", TrackRequest.Create("Wake Me Up", "Avicii").Value.ToString());
    }
}

public sealed class ChannelIdTests
{
    [Fact]
    public void Create_normalizes_case_and_surrounding_spaces()
    {
        Assert.Equal(ChannelId.Create("sms").Value, ChannelId.Create("  SMS ").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("sms!")]
    [InlineData("push notification")]
    public void Create_rejects_an_invalid_identifier(string? raw)
    {
        Assert.Equal(ErrorKind.InvalidRequest, ChannelId.Create(raw).Error.Kind);
    }

    [Fact]
    public void A_new_channel_needs_no_change_to_the_domain()
    {
        // WhatsApp n'existe nulle part dans le code de production : c'est tout l'intérêt de
        // ChannelId plutôt qu'une enum.
        Assert.Equal("whatsapp", ChannelId.Create("WhatsApp").Value.Value);
    }

    [Fact]
    public void ToString_returns_the_identifier()
    {
        Assert.Equal("email", ChannelId.Create("email").Value.ToString());
    }
}

public sealed class ContactAddressTests
{
    [Fact]
    public void Create_trims_the_address()
    {
        Assert.Equal("+33612345678", ContactAddress.Create(" +33612345678 ").Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Create_rejects_a_blank_address(string? raw)
    {
        Assert.Equal(ErrorKind.InvalidContact, ContactAddress.Create(raw).Error.Kind);
    }
}

public sealed class UserIdTests
{
    [Fact]
    public void Create_trims_the_identifier()
    {
        Assert.Equal("42", UserId.Create(" 42 ").Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_identifier(string? raw)
    {
        Assert.Equal(ErrorKind.InvalidRequest, UserId.Create(raw).Error.Kind);
    }

    [Fact]
    public void ToString_returns_the_identifier()
    {
        Assert.Equal("42", UserId.Create("42").Value.ToString());
    }
}

public sealed class TrackTests
{
    [Fact]
    public void Create_trims_title_and_artist()
    {
        var track = Track.Create(" Here Comes the Sun ", " The Beatles ").Value;

        Assert.Equal(new Track("Here Comes the Sun", "The Beatles"), track);
    }

    [Theory]
    [InlineData(null, "The Beatles")]
    [InlineData(" ", "The Beatles")]
    [InlineData("Here Comes the Sun", null)]
    [InlineData("Here Comes the Sun", "")]
    public void Create_rejects_a_track_without_title_or_artist(string? title, string? artist)
    {
        Assert.True(Track.Create(title, artist).IsFailure);
    }
}
