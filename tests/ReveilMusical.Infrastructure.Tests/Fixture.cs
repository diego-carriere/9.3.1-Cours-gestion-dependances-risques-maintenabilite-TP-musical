using System.Net;
using System.Text;

namespace ReveilMusical.Infrastructure.Tests;

internal static class Fixture
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    public static HttpResponseMessage Json(string body, string mediaType = "application/json") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, mediaType),
    };
}
