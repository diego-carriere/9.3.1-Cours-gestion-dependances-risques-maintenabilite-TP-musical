using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Tests;

public sealed class KeywordTests
{
    [Fact]
    public void Create_trims_and_collapses_inner_whitespace()
    {
        var keyword = Keyword.Create("  beau    temps ").Value;

        Assert.Equal("beau temps", keyword.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_keyword(string? raw)
    {
        var result = Keyword.Create(raw);

        Assert.Equal(ErrorKind.InvalidRequest, result.Error.Kind);
    }

    [Fact]
    public void Create_rejects_a_keyword_longer_than_the_limit()
    {
        var result = Keyword.Create(new string('a', Keyword.MaxLength + 1));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Normalized_form_ignores_case_so_two_spellings_share_a_cache_entry()
    {
        Assert.Equal(Keyword.Create("Beau Temps").Value.Normalized, Keyword.Create("beau temps").Value.Normalized);
    }
}

public sealed class KeywordSetTests
{
    [Fact]
    public void Create_keeps_the_order_and_drops_case_insensitive_duplicates()
    {
        var set = KeywordSet.Create(["soleil", "Sunshine", "SOLEIL"]).Value;

        Assert.Equal(["soleil", "Sunshine"], set.Keywords.Select(k => k.Value));
    }

    [Fact]
    public void Create_rejects_an_empty_list()
    {
        Assert.Equal(ErrorKind.InvalidRequest, KeywordSet.Create([]).Error.Kind);
    }

    [Fact]
    public void Create_rejects_a_list_holding_an_invalid_keyword()
    {
        Assert.True(KeywordSet.Create(["soleil", " "]).IsFailure);
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
