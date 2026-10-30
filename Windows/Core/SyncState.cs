using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ILIASSync.Core;

public sealed record SyncEntry(string Sig, string Path, string? Hash);

public sealed class SyncStats
{
    public int New, Updated, Unchanged;
    public List<string> Errors { get; } = new();
}

public sealed class SyncState
{
    private static string FilePath => System.IO.Path.Combine(Settings.DataDir, "state.json");
    private readonly Dictionary<string, SyncEntry> _entries;

    public SyncState()
    {
        try { _entries = JsonSerializer.Deserialize<Dictionary<string, SyncEntry>>(File.ReadAllText(FilePath)) ?? new(); }
        catch { _entries = new(); }
    }

    public SyncEntry? Get(string key) => _entries.TryGetValue(key, out var e) ? e : null;
    public void Set(string key, SyncEntry e) => _entries[key] = e;

    public void Save()
    {
        Directory.CreateDirectory(Settings.DataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(_entries));
    }
}
