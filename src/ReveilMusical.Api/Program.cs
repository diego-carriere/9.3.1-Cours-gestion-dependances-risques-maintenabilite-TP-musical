var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();

// Permet à WebApplicationFactory<Program> (ReveilMusical.Api.E2ETests) de trouver le point d'entrée.
public partial class Program;
