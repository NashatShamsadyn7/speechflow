using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechFlow.Core;

/// <summary>Rewrites the last dictation with an LLM on Groq (OpenAI-compatible API).</summary>
public static class GroqPolisher
{
    const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
    const string Model = "llama-3.3-70b-versatile";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static readonly IReadOnlyDictionary<string, string> Modes = new Dictionary<string, string>
    {
        ["grammar"] = "Fix grammar, spelling and punctuation in the user's dictated text. Keep the same language, meaning and wording wherever possible. Reply with the corrected text only, no quotes or comments.",
        ["professional"] = "Rewrite the user's dictated text in a clear, polite, professional tone. Keep the same language. Reply with the rewritten text only, no quotes or comments.",
        ["translate"] = "If the user's text is Arabic, translate it into natural English; otherwise translate it into natural Arabic. Reply with the translation only, no quotes or comments.",
    };

    public static async Task<string> PolishAsync(string apiKey, string text, string mode, CancellationToken ct = default)
    {
        var prompt = Modes.TryGetValue(mode, out var p) ? p : Modes["grammar"];
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = Model,
                temperature = 0.2,
                messages = new object[]
                {
                    new { role = "system", content = prompt },
                    new { role = "user", content = text },
                },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            Log.Error($"Groq returned {(int)response.StatusCode}");
            throw new InvalidOperationException(response.StatusCode == HttpStatusCode.Unauthorized
                ? "Groq rejected the API key."
                : $"Groq error ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content")
            .GetString()?.Trim() ?? "";
    }
}
