using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SpeechFlow.Core;

public enum StreamStatus { Connecting, Connected, Reconnecting, Closed }

/// <summary>
/// One dictation session over Deepgram's live WebSocket API.
/// Audio is buffered from the first millisecond, so words spoken while the socket is still
/// connecting (or reconnecting) are not lost. <see cref="Complete"/> sends CloseStream and keeps
/// reading until Deepgram has delivered the last final transcript. Events fire on a worker thread.
/// </summary>
public sealed class DeepgramStream
{
    const int MaxConnectAttempts = 5;
    static readonly byte[] CloseStreamMessage = Encoding.UTF8.GetBytes("{\"type\":\"CloseStream\"}");

    readonly string _apiKey;
    readonly Uri _uri;
    // 50 ms chunks → 600 chunks ≈ 30 s of audio kept while offline; beyond that the oldest is dropped.
    readonly Channel<byte[]> _audio = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(600) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    readonly CancellationTokenSource _cts = new();

    public event Action<string>? Interim;
    public event Action<string>? Final;
    public event Action<StreamStatus>? StatusChanged;
    public event Action<string>? Failed;
    public event Action? Completed;

    public DeepgramStream(string apiKey, string language, string model)
    {
        _apiKey = apiKey;
        var query = new List<string>
        {
            "model=" + Uri.EscapeDataString(model),
            "language=" + Uri.EscapeDataString(language),
            "encoding=linear16",
            $"sample_rate={AudioCapture.SampleRate}",
            "channels=1",
            "smart_format=true",
            "punctuate=true",
            "interim_results=true",
            "endpointing=300",
        };
        // Spoken "comma", "period", "new line"… Deepgram only supports this for English.
        if (language.StartsWith("en", StringComparison.Ordinal)) query.Add("dictation=true");
        _uri = new Uri("wss://api.deepgram.com/v1/listen?" + string.Join("&", query));
    }

    public void Start() => _ = Task.Run(RunAsync);

    public void Write(byte[] pcm16) => _audio.Writer.TryWrite(pcm16);

    /// <summary>No more audio; flush what's buffered and wait (max 8 s) for the final transcripts.</summary>
    public void Complete()
    {
        if (_audio.Writer.TryComplete()) _cts.CancelAfter(TimeSpan.FromSeconds(8));
    }

    async Task RunAsync()
    {
        try
        {
            for (int attempt = 0; ;)
            {
                using var ws = new ClientWebSocket();
                ws.Options.SetRequestHeader("Authorization", "Token " + _apiKey);
                ws.Options.CollectHttpResponseDetails = true;
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

                StatusChanged?.Invoke(attempt == 0 ? StreamStatus.Connecting : StreamStatus.Reconnecting);
                try
                {
                    await ws.ConnectAsync(_uri, _cts.Token);
                }
                catch (Exception ex) when (!_cts.IsCancellationRequested)
                {
                    switch (ws.HttpStatusCode)
                    {
                        case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                            Failed?.Invoke("Deepgram rejected the API key. Check it in Settings.");
                            return;
                        case HttpStatusCode.PaymentRequired:
                            Failed?.Invoke("Your Deepgram balance is used up.");
                            return;
                        case HttpStatusCode.BadRequest:
                            Failed?.Invoke("Deepgram doesn't accept this model/language. Check Settings → Advanced.");
                            return;
                    }
                    if (++attempt > MaxConnectAttempts)
                    {
                        Log.Error("Deepgram connect failed", ex);
                        Failed?.Invoke("Can't reach Deepgram. Check your internet connection.");
                        return;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(8, 1 << (attempt - 1))), _cts.Token);
                    continue;
                }

                attempt = 0;
                StatusChanged?.Invoke(StreamStatus.Connected);
                if (await RunConnectionAsync(ws)) return;
                if (_audio.Reader.Completion.IsCompleted) return;
                // Dropped mid-session: reconnect; audio kept buffering meanwhile.
                attempt = 1;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error("Deepgram stream crashed", ex);
            Failed?.Invoke("Speech service error. See the log for details.");
        }
        finally
        {
            StatusChanged?.Invoke(StreamStatus.Closed);
            Completed?.Invoke();
        }
    }

    /// <returns>True when the session ended normally (CloseStream sent and all results received).</returns>
    async Task<bool> RunConnectionAsync(ClientWebSocket ws)
    {
        using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var receive = ReceiveLoopAsync(ws, _cts.Token);
        var send = SendLoopAsync(ws, sendCts.Token);

        if (await Task.WhenAny(receive, send) == receive)
        {
            // The server closed (or the network dropped) while we still had audio to send.
            sendCts.Cancel();
            await send;
            return false;
        }

        bool closedCleanly = await send;
        await receive;
        return closedCleanly;
    }

    async Task<bool> SendLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        try
        {
            while (await _audio.Reader.WaitToReadAsync(ct))
            {
                // Peek first so a chunk isn't lost if the socket dies mid-send.
                while (_audio.Reader.TryPeek(out var chunk))
                {
                    await ws.SendAsync(chunk, WebSocketMessageType.Binary, true, ct);
                    _audio.Reader.TryRead(out _);
                }
            }
            await ws.SendAsync(CloseStreamMessage, WebSocketMessageType.Text, true, ct);
            return true;
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            return false;
        }
    }

    async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        try
        {
            while (ws.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                var result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (ws.CloseStatus != WebSocketCloseStatus.NormalClosure)
                        Log.Info($"Deepgram closed the stream: {ws.CloseStatus} {ws.CloseStatusDescription}");
                    if (ws.State == WebSocketState.CloseReceived)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                    return;
                }
                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage) continue;
                HandleMessage(message.ToArray());
                message.SetLength(0);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
        }
    }

    void HandleMessage(byte[] json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "Results") return;

            var alternatives = root.GetProperty("channel").GetProperty("alternatives");
            if (alternatives.GetArrayLength() == 0) return;
            var transcript = alternatives[0].GetProperty("transcript").GetString();
            if (string.IsNullOrWhiteSpace(transcript)) return;

            bool isFinal = root.TryGetProperty("is_final", out var f) && f.GetBoolean();
            if (isFinal) Final?.Invoke(transcript);
            else Interim?.Invoke(transcript);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Error("Unexpected message from Deepgram", ex);
        }
    }
}
