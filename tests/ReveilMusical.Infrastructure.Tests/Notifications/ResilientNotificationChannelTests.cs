using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Notifications;

public sealed class ResilientNotificationChannelTests
{
    private static readonly ContactAddress Contact = ContactAddress.Create("+33612345678").Value;
    private static readonly WakeUpMessage Message =
        WakeUpMessage.Compose("Alice", new Track("A", "B"), DayOfWeek.Monday, WeatherCondition.Sunny);

    private static readonly ChannelResilienceOptions FastOptions = new()
    {
        AttemptTimeout = TimeSpan.FromMilliseconds(100),
        RetryCount = 1,
        RetryDelay = TimeSpan.Zero,
        CircuitBreakerFailureRatio = 1.0,
        CircuitBreakerMinimumThroughput = 2,
        CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30),
        CircuitBreakerBreakDuration = TimeSpan.FromMinutes(1),
    };

    [Fact]
    public async Task A_transient_failure_is_retried_once()
    {
        var inner = new FlakyChannel(failuresBeforeSuccess: 1);

        var result = await Wrap(inner).SendAsync(Contact, Message, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task An_invalid_contact_is_not_retried()
    {
        var inner = new FakeNotificationChannel().FailsWith(ErrorKind.InvalidContact);

        var result = await Wrap(inner).SendAsync(Contact, Message, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.InvalidContact, result.Error.Kind);
        Assert.Equal(1, inner.Attempts);
    }

    [Fact]
    public async Task A_hanging_channel_becomes_a_timeout()
    {
        var result = await Wrap(new HangingChannel()).SendAsync(Contact, Message, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Timeout, result.Error.Kind);
    }

    [Fact]
    public async Task A_channel_that_ignores_the_token_is_still_bounded_by_the_timeout()
    {
        var sut = Wrap(new NonCooperativeChannel(), FastOptions with { RetryCount = 0 });

        var result = await sut.SendAsync(Contact, Message, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Timeout, result.Error.Kind);
    }

    [Fact]
    public async Task An_unexpected_exception_is_retried_then_becomes_an_unavailable_channel()
    {
        var inner = new FakeNotificationChannel().Throws(new IOException("disk full: +33612345678"));

        var result = await Wrap(inner).SendAsync(Contact, Message, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ChannelUnavailable, result.Error.Kind);
        Assert.Equal("Canal 'sms' : erreur inattendue (IOException).", result.Error.Message);
        Assert.Equal(2, inner.Attempts);
    }

    [Fact]
    public async Task Repeated_unexpected_exceptions_open_the_circuit()
    {
        var inner = new FakeNotificationChannel().Throws(new IOException("disk full"));
        var sut = Wrap(inner, FastOptions with { RetryCount = 0 });

        await sut.SendAsync(Contact, Message, TestContext.Current.CancellationToken);
        await sut.SendAsync(Contact, Message, TestContext.Current.CancellationToken);
        var third = await sut.SendAsync(Contact, Message, TestContext.Current.CancellationToken);

        Assert.Contains("circuit", third.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, inner.Attempts);
    }

    [Fact]
    public async Task Once_the_circuit_is_open_the_channel_fails_fast_without_calling_the_service()
    {
        var inner = new FakeNotificationChannel().FailsWith(ErrorKind.ChannelUnavailable);
        var sut = Wrap(inner, FastOptions with { RetryCount = 0 });

        await sut.SendAsync(Contact, Message, TestContext.Current.CancellationToken);
        await sut.SendAsync(Contact, Message, TestContext.Current.CancellationToken);
        var third = await sut.SendAsync(Contact, Message, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.ChannelUnavailable, third.Error.Kind);
        Assert.Contains("circuit", third.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, inner.Attempts);
    }

    [Fact]
    public async Task A_caller_cancellation_is_not_turned_into_a_timeout()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Wrap(new HangingChannel()).SendAsync(Contact, Message, cancellation.Token));
    }

    private static ResilientNotificationChannel Wrap(INotificationChannel inner, ChannelResilienceOptions? options = null)
    {
        var builder = new ResiliencePipelineBuilder<Result<DeliveryReceipt>>();
        ChannelResiliencePipelines.Configure(builder, options ?? FastOptions);

        return new ResilientNotificationChannel(
            inner, builder.Build(), ChannelId.Create("sms").Value, NullLogger<ResilientNotificationChannel>.Instance);
    }

    private sealed class FlakyChannel(int failuresBeforeSuccess) : INotificationChannel
    {
        public int Calls { get; private set; }

        public Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Calls <= failuresBeforeSuccess
                ? Result.Failure<DeliveryReceipt>(ErrorKind.ChannelUnavailable, "flaky")
                : Result.Success(new DeliveryReceipt("ok")));
        }
    }

    /// <summary>Un adaptateur qui bloque sans jamais regarder le jeton, comme un SDK synchrone figé.</summary>
    private sealed class NonCooperativeChannel : INotificationChannel
    {
        public async Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
            return Result.Success(new DeliveryReceipt("too late"));
        }
    }

    private sealed class HangingChannel : INotificationChannel
    {
        public async Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Result.Success(new DeliveryReceipt("never"));
        }
    }
}

/// <summary>Le décorateur respecte le même contrat que le canal qu'il enveloppe (Liskov).</summary>
public sealed class ResilientNotificationChannelContractTests : NotificationChannelContractTests
{
    protected override ContactAddress ValidContact => ContactAddress.Create("ok").Value;

    protected override ContactAddress InvalidContact => ContactAddress.Create("rejected").Value;

    protected override INotificationChannel CreateWorkingSut() => Wrap(new FakeNotificationChannel().Rejects("rejected"));

    protected override INotificationChannel CreateSutWhoseServiceIsDown() =>
        Wrap(new FakeNotificationChannel().FailsWith(ErrorKind.ChannelUnavailable));

    private static ResilientNotificationChannel Wrap(INotificationChannel inner)
    {
        var builder = new ResiliencePipelineBuilder<Result<DeliveryReceipt>>();
        ChannelResiliencePipelines.Configure(builder, new ChannelResilienceOptions { RetryDelay = TimeSpan.Zero });
        return new ResilientNotificationChannel(inner, builder.Build(), ChannelId.Create("x").Value, NullLogger<ResilientNotificationChannel>.Instance);
    }
}
