using ReveilMusical.Api.Endpoints;
using ReveilMusical.Application;
using ReveilMusical.Application.Options;
using ReveilMusical.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Détecte au démarrage toute dépendance captive (un singleton qui capture un service scoped),
// sans attendre qu'elle se manifeste : Support J1, « le piège de la dépendance captive ».
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Le composition root : les seules lignes du projet qui relient des implémentations à des
// abstractions. Aucune autre classe n'appelle `new` sur une dépendance (Support J1, « le new
// partout ») ; les adaptateurs sont internal à l'Infrastructure, ce fichier ne peut même pas les nommer.
builder.Services.AddApplication(options => builder.Configuration.GetSection(WakeUpOptions.SectionName).Bind(options));
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapWakeUpEndpoint();
app.MapHealthChecks("/health");

app.Run();

// Permet à WebApplicationFactory<Program> (ReveilMusical.Api.E2ETests) de trouver le point d'entrée.
public partial class Program;
