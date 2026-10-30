using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ILIASSync.Core;

/// <summary>Steuert die Synchronisierung. Alle Zugriffe laufen auf dem UI-Thread.</summary>
public sealed class SyncEngine
{
    public Settings Settings { get; set; } = Settings.Load();
    public List<Course> Courses { get; set; } = new();
    public bool IsSyncing { get; private set; }
    public string Status { get; private set; } = "Noch nicht synchronisiert";
    public DateTime? LastSync { get; private set; }

    public event Action? Changed;
    public event Action<string>? Notify;

    private DispatcherTimer? _timer;

    public IliasClient CreateClient() => new(Settings.ServerUrl);

    public void Start()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += async (_, _) =>
        {
            var due = LastSync == null || (DateTime.Now - LastSync.Value).TotalMinutes >= Settings.IntervalMinutes;
            if (due && Settings.IsConfigured) await SyncNow();
        };
        _timer.Start();
        if (Settings.IsConfigured) _ = SyncNow();
    }

    public async Task SyncNow()
    {
        if (IsSyncing || !Settings.IsConfigured) return;
        IsSyncing = true;
        Status = "Synchronisiere …";
        Changed?.Invoke();
        try
        {
            using var client = CreateClient();
            await client.Login(Settings.Username, Settings.Password);
            var list = await client.FetchCourses();
            Courses = list;

            var state = new SyncState();
            var stats = new SyncStats();
            foreach (var c in list.Where(c => !Settings.DisabledCourses.Contains(c.Id)))
                await client.SyncCourse(c, Settings.SyncFolder, state, stats);
            state.Save();

            LastSync = DateTime.Now;
            var changed = stats.New + stats.Updated;
            Status = changed > 0 ? $"{stats.New} neu, {stats.Updated} aktualisiert" : "Alles aktuell";
            if (stats.Errors.Count > 0) Status += $" ({stats.Errors.Count} Fehler)";
            if (changed > 0) Notify?.Invoke($"{stats.New} neue und {stats.Updated} aktualisierte Dateien");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsSyncing = false;
            Changed?.Invoke();
        }
    }
}
