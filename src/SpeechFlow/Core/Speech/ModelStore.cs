using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechFlow.Core.Speech;

public sealed record WhisperModel(string Id, string DisplayName, string FileName, long Bytes);

/// <summary>Downloads Whisper models once into %LOCALAPPDATA%\SpeechFlow\models (not roaming: they're big).</summary>
public static class ModelStore
{
    const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";
    static readonly HttpClient Http = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    public static readonly WhisperModel[] Models =
    {
        new("turbo", "Best quality — Whisper large-v3-turbo (574 MB)", "ggml-large-v3-turbo-q5_0.bin", 574_041_195),
        new("small", "Faster on older PCs — Whisper small (190 MB)", "ggml-small-q5_1.bin", 190_085_487),
    };

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpeechFlow", "models");

    public static WhisperModel Find(string id) => Models.FirstOrDefault(m => m.Id == id) ?? Models[0];

    public static string PathOf(WhisperModel model) => Path.Combine(Directory, model.FileName);

    public static bool IsDownloaded(WhisperModel model)
    {
        var file = new FileInfo(PathOf(model));
        return file.Exists && file.Length == model.Bytes;
    }

    /// <param name="progress">Fraction 0..1.</param>
    public static async Task DownloadAsync(WhisperModel model, IProgress<double> progress, CancellationToken ct)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var target = PathOf(model);
        var partial = target + ".part";

        try
        {
            using var response = await Http.GetAsync(BaseUrl + model.FileName, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(ct))
            await using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                var buffer = new byte[1 << 16];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                    done += read;
                    progress.Report((double)done / model.Bytes);
                }
            }

            if (new FileInfo(partial).Length != model.Bytes)
                throw new SpeechException("The speech model download was incomplete. Try again.");
            File.Move(partial, target, overwrite: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            TryDelete(partial);
            Log.Error("Model download failed", ex);
            throw new SpeechException(ex is IOException
                ? "Couldn't save the speech model. Check free disk space."
                : "Couldn't download the speech model. Check your internet connection.", ex);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
