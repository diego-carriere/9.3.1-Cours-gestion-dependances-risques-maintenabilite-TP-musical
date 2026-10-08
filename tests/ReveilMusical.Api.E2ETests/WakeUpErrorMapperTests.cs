using Microsoft.AspNetCore.Http;
using ReveilMusical.Api.Errors;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Api.E2ETests;

public sealed class WakeUpErrorMapperTests
{
    [Fact]
    public void Every_error_kind_has_a_deliberate_non_500_status()
    {
        // Un switch C# sur une enum n'est jamais exhaustif pour le compilateur : ce test l'est.
        foreach (var kind in Enum.GetValues<ErrorKind>())
        {
            var status = WakeUpErrorMapper.StatusCodeFor(kind);

            Assert.InRange(status, 400, 504);
            Assert.NotEqual(StatusCodes.Status500InternalServerError, status);
        }
    }

    [Theory]
    [InlineData(ErrorKind.InvalidRequest, 400)]
    [InlineData(ErrorKind.UserNotFound, 404)]
    [InlineData(ErrorKind.UserServiceUnavailable, 503)]
    [InlineData(ErrorKind.Timeout, 504)]
    public void The_mapping_of_reachable_errors_is_the_documented_one(ErrorKind kind, int expected)
    {
        Assert.Equal(expected, WakeUpErrorMapper.StatusCodeFor(kind));
    }
}
