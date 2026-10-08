using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Tests;

public sealed class ResultTests
{
    [Fact]
    public void A_success_carries_its_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void A_failure_carries_its_error()
    {
        var result = Result.Failure<int>(ErrorKind.UserNotFound, "inconnu");

        Assert.True(result.IsFailure);
        Assert.Equal(new DomainError(ErrorKind.UserNotFound, "inconnu"), result.Error);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_throws()
    {
        var result = Result.Failure<int>(ErrorKind.Timeout, "trop long");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Reading_the_error_of_a_success_throws()
    {
        var result = Result.Success("ok");

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void ToString_never_throws_so_a_failure_can_be_logged()
    {
        // Polly journalise le résultat de chaque tentative : le ToString synthétisé d'un record
        // lirait Value et lèverait sur un échec.
        Assert.Equal("Failure(Timeout: trop long)", Result.Failure<int>(ErrorKind.Timeout, "trop long").ToString());
        Assert.Equal("Success(42)", Result.Success(42).ToString());
    }
}
