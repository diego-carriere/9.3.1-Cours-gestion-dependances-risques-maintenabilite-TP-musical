using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.Infrastructure;

namespace ReveilMusical.Api.E2ETests;

/// <summary>
/// Un ordonnanceur qui raccroche (délai d'attente trop court, redémarrage) ou un arrêt de l'hôte
/// (déploiement) ne doit pas annuler un réveil déjà en route : le réveil n'est lié ni à
/// <c>RequestAborted</c> ni à <c>ApplicationStopping</c>.
/// </summary>
public sealed class SchedulerDisconnectTests
{
    [Fact]
    public async Task A_scheduler_that_hangs_up_mid_flight_does_not_cancel_the_wake_up()
    {
        await using var factory = new ReveilApiFactory(
            new Dictionary<string, string?>
            {
                ["UserService:Users:0:PreferredChannel"] = "gated",
                ["UserService:Users:0:Contacts:gated"] = "gate-1",
                ["Resilience:Channels:AttemptTimeout"] = "00:00:30",
            },
            services => services.AddNotificationChannel<GatedChannel>("gated"));
        factory.Upstream.FixtureFor(FakeUpstream.ITunesHost, "itunes-search-here-comes-the-sun.json");
        var gate = factory.Services.GetRequiredService<GatedChannel>();
        var testToken = TestContext.Current.CancellationToken;

        using var hangUp = new CancellationTokenSource();
        var call = factory.CreateClient().PostAsJsonAsync("/wake-ups", new { userId = "42", day = "MARDI", weather = "SOLEIL" }, hangUp.Token);
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10), testToken);

        // L'ordonnanceur raccroche ; on laisse à la déconnexion le temps d'atteindre le serveur. Si le
        // réveil suivait RequestAborted, le canal serait annulé avant d'être libéré.
        await hangUp.CancelAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(200), testToken);
        gate.Release();

        Assert.True(await gate.Delivered.WaitAsync(TimeSpan.FromSeconds(10), testToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    [Fact]
    public async Task A_host_shutting_down_mid_flight_does_not_cancel_the_wake_up()
    {
        await using var factory = new ReveilApiFactory(
            new Dictionary<string, string?>
            {
                ["UserService:Users:0:PreferredChannel"] = "gated",
                ["UserService:Users:0:Contacts:gated"] = "gate-1",
                ["Resilience:Channels:AttemptTimeout"] = "00:00:30",
            },
            services => services.AddNotificationChannel<GatedChannel>("gated"));
        factory.Upstream.FixtureFor(FakeUpstream.ITunesHost, "itunes-search-here-comes-the-sun.json");
        var gate = factory.Services.GetRequiredService<GatedChannel>();
        var testToken = TestContext.Current.CancellationToken;

        var call = factory.CreateClient().PostAsJsonAsync("/wake-ups", new { userId = "42", day = "MARDI", weather = "SOLEIL" }, testToken);
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10), testToken);

        // Un déploiement démarre l'arrêt pendant l'envoi : ApplicationStopping se déclenche. Si le
        // réveil le suivait, le canal serait annulé avant d'être libéré.
        factory.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        await Task.Delay(TimeSpan.FromMilliseconds(200), testToken);
        gate.Release();

        Assert.True(await gate.Delivered.WaitAsync(TimeSpan.FromSeconds(10), testToken));

        // Kestrel attend les requêtes en vol (HostOptions.ShutdownTimeout) ; TestServer, lui, libère
        // le conteneur sans les attendre, et la réponse peut ne jamais revenir. Seule la remise compte ici.
        await Record.ExceptionAsync(() => call);
    }

    /// <summary>Un canal qui attend qu'on le libère, en respectant le jeton qu'il reçoit.</summary>
    internal sealed class GatedChannel : INotificationChannel
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public Task<bool> Delivered => _delivered.Task;

        public void Release() => _release.TrySetResult();

        public async Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                _delivered.TrySetResult(true);
                return Result.Success(new DeliveryReceipt("gated-1"));
            }
            catch (OperationCanceledException)
            {
                _delivered.TrySetResult(false);
                throw;
            }
        }
    }
}
