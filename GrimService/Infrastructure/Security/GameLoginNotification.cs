using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GrimService.Infrastructure.Security;

public static class GameLoginNotification
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(2) };

    public static Task SendAsync(string endpoint, string json, CancellationToken budget, ILogger logger)
        => SendAsync(Client, endpoint, json, budget, logger);

    public static async Task SendAsync(HttpClient client, string endpoint, string json, CancellationToken budget, ILogger logger)
    {
        if (budget.IsCancellationRequested || string.IsNullOrWhiteSpace(endpoint)) return;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Game login notification failed: http={Status}; authentication continues", (int)response.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning("Game login notification unavailable: reason={Reason}; authentication continues", exception.GetType().Name);
        }
    }
}
