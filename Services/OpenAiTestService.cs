using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace UaeEInvoice.Services;

public class OpenAiTestService
{
    private readonly ChatClient _client;

    public OpenAiTestService(IConfiguration config)
    {
        var apiKey = config["OpenAI:ApiKey"]
                     ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("OpenAI API key not found. Set OpenAI:ApiKey in appsettings.Development.json OR set OPENAI_API_KEY env var.");

        _client = new ChatClient(model: "gpt-5-mini", apiKey: apiKey);
    }

    public async Task<string> PingAsync()
    {
        var completion = await _client.CompleteChatAsync("Reply only with: OK");
        return completion.Value.Content.FirstOrDefault()?.Text?.Trim() ?? "";
    }
}
