using System.Net.Http.Json;
using System.Text.Json;

namespace ReveilMusical.Api.E2ETests;

internal static class Http
{
    public static Task<HttpResponseMessage> WakeUpAsync(this HttpClient client, string userId, string day, string weather) =>
        client.PostAsJsonAsync("/wake-ups", new { userId, day, weather }, TestContext.Current.CancellationToken);

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static string Str(this JsonElement element, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            element = int.TryParse(segment, out var index) ? element[index] : element.GetProperty(segment);
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();
    }
}
