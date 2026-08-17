using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CrashLab.LoadTests;

public static class AuthTokenPool
{
    private const string TokenEndpoint = "http://identity.crashlab.local:8090/connect/token";
    private const int PoolSize = 50;

    public static async Task<List<string>> FetchAsync(HttpClient httpClient)
    {
        var tokens = new List<string>();
        for (var i = 0; i < PoolSize; i++)
        {
            var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = $"loadtest_{i:D2}",
                ["password"] = "LoadTest123!",
                ["client_id"] = "crashlab-loadtest",
                ["scope"] = "profile"
            }));
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
            tokens.Add(payload!.AccessToken);
        }
        return tokens;
    }

    private record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
}
