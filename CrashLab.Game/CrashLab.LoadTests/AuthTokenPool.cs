using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CrashLab.LoadTests;

public static class AuthTokenPool
{
    private const string TokenEndpoint = "http://identity.crashlab.local:8090/connect/token";
    private const int DefaultPoolSize = 50;

    private const int MaxConcurrentRequests = 50;

    public static async Task<List<string>> FetchAsync(HttpClient httpClient, int poolSize = DefaultPoolSize)
    {
        using var throttle = new SemaphoreSlim(MaxConcurrentRequests);

        var fetchTasks = Enumerable.Range(0, poolSize).Select(async i =>
        {
            await throttle.WaitAsync();
            try
            {
                var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["username"] = $"loadtest_{i:D3}",
                    ["password"] = "LoadTest123!",
                    ["client_id"] = "crashlab-loadtest",
                    ["scope"] = "profile"
                }));
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
                return payload!.AccessToken;
            }
            finally
            {
                throttle.Release();
            }
        });
        return (await Task.WhenAll(fetchTasks)).ToList();
    }

    private record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
}
