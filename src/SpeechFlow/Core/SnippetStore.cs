using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpeechFlow.Core;

public sealed class Snippet
{
    public string Trigger { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>Spoken phrase → text expansions. Starts empty; the user adds their own.</summary>
public static class SnippetStore
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static List<Snippet> Load()
    {
        try
        {
            if (File.Exists(AppPaths.SnippetsFile))
                return JsonSerializer.Deserialize<List<Snippet>>(File.ReadAllText(AppPaths.SnippetsFile)) ?? new();
        }
        catch (Exception ex)
        {
            Log.Error("Snippets file unreadable", ex);
        }
        return new();
    }

    public static void Save(IEnumerable<Snippet> snippets)
    {
        var clean = snippets
            .Where(s => !string.IsNullOrWhiteSpace(s.Trigger) && !string.IsNullOrEmpty(s.Text))
            .Select(s => new Snippet { Trigger = s.Trigger.Trim(), Text = s.Text })
            .ToList();
        AppPaths.WriteAllTextAtomic(AppPaths.SnippetsFile, JsonSerializer.Serialize(clean, Json));
    }
}
