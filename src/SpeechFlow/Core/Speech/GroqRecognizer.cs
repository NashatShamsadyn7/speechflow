using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechFlow.Core.Speech;

/// <summary>Whisper large-v3-turbo on Groq's servers: faster than a laptop CPU, needs a free API key.</summary>
public sealed class GroqRecognizer(string apiKey) : ISpeechRecognizer
{
    const string TranscribeEndpoint = "https://api.groq.com/openai/v1/audio/transcriptions";
    // Groq only translates with the full large-v3 model, not turbo.
    const string TranslateEndpoint = "https://api.groq.com/openai/v1/audio/translations";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public Task PrepareAsync(IProgress<string> progress, CancellationToken ct) => Task.CompletedTask;

    public async Task<string> TranscribeAsync(byte[] pcm16, string? language, bool translate, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await SendAsync(pcm16, language, translate, ct);
            }
            catch (HttpRequestException ex) when (attempt < 2)
            {
                Log.Error("Groq request failed, retrying", ex);
                await Task.Delay(500, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new SpeechException("Can't reach Groq. Check your internet, or switch to the on-device engine.", ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new SpeechException("Groq took too long to answer.", ex);
            }
        }
    }

    async Task<string> SendAsync(byte[] pcm16, string? language, bool translate, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(Wav.FromPcm16(pcm16));
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "file", "speech.wav");
        form.Add(new StringContent(translate ? "whisper-large-v3" : "whisper-large-v3-turbo"), "model");
        form.Add(new StringContent("json"), "response_format");
        form.Add(new StringContent("0"), "temperature");
        if (!translate && language is not null) form.Add(new StringContent(language), "language");

        var endpoint = translate ? TranslateEndpoint : TranscribeEndpoint;
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error($"Groq transcription returned {(int)response.StatusCode}");
            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new SpeechException("Groq rejected the API key. Check it in Settings."),
                HttpStatusCode.TooManyRequests => new SpeechException("Groq's free limit is reached for now. Wait a minute or use the on-device engine."),
                _ => new SpeechException($"Groq error ({(int)response.StatusCode})."),
            };
        }

        using var doc = JsonDocument.Parse(body);
        return Hallucinations.Clean(doc.RootElement.GetProperty("text").GetString() ?? "");
    }
}

static class Wav
{
    public static byte[] FromPcm16(byte[] pcm)
    {
        using var stream = new MemoryStream(44 + pcm.Length);
        using var w = new BinaryWriter(stream);
        int rate = AudioCapture.SampleRate;
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + pcm.Length);
        w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(rate);
        w.Write(rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(pcm.Length);
        w.Write(pcm);
        w.Flush();
        return stream.ToArray();
    }
}
