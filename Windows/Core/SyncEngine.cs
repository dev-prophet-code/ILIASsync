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
    public event Action<string>? Notify;   // Text des Hinweises "Neue Daten synchronisiert"

    private DispatcherTimer? _timer;
    private DateTime? _lastAttempt;
    private bool _loginBlocked;   // Zugangsdaten wurden abgelehnt: nicht automatisch weiter versuchen

    /// <summary>Nach geänderten Zugangsdaten wieder automatische Syncs erlauben.</summary>
    public void ResetLoginBlock() => _loginBlocked = false;

    public IliasClient CreateClient() => new(Settings.ServerUrl);

    public void Start()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += async (_, _) =>
        {
            var due = _lastAttempt == null || (DateTime.Now - _lastAttempt.Value).TotalMinutes >= Settings.IntervalMinutes;
            if (due && !_loginBlocked && Settings.IsConfigured) await SyncNow();
        };
        _timer.Start();
        if (Settings.IsConfigured) _ = SyncNow();
    }

    public async Task SyncNow()
    {
        if (IsSyncing || !Settings.IsConfigured) return;
        IsSyncing = true;
        _lastAttempt = DateTime.Now;
        Status = "Synchronisiere …";
        Changed?.Invoke();
        try
        {
            using var client = CreateClient();
            await client.Login(Settings.Username, Settings.Password);
            _loginBlocked = false;
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
            if (changed > 0) Notify?.Invoke($"{stats.New} neue, {stats.Updated} aktualisierte Datei(en)");
        }
        catch (LoginFailedException ex)
        {
            _loginBlocked = true;
            Status = ex.Message + " (automatische Syncs pausiert)";
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
