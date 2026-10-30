using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using ILIASSync.Core;

namespace ILIASSync;

public partial class App : System.Windows.Application
{
    public static SyncEngine Engine { get; } = new();

    private Mutex? _mutex;
    private NotifyIcon? _tray;
    private ToolStripMenuItem? _statusItem;
    private SettingsWindow? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "ILIASSync.SingleInstance", out var created);
        if (!created) { Shutdown(); return; }

        BuildTray();
        Engine.Changed += UpdateStatus;
        Engine.Notify += msg => _tray?.ShowBalloonTip(5000, "ILIAS Sync", msg, ToolTipIcon.Info);
        Engine.Start();

        if (!Engine.Settings.IsConfigured) ShowSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }

    private void BuildTray()
    {
        var iconStream = GetResourceStream(new Uri("pack://application:,,,/Resources/AppIcon.ico")).Stream;
        var menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem(Engine.Status) { Enabled = false };
        var courses = new ToolStripMenuItem("Meine Kurse");
        var syncNow = new ToolStripMenuItem("Jetzt synchronisieren", null, async (_, _) => await Engine.SyncNow());
        var openFolder = new ToolStripMenuItem("Sync-Ordner öffnen", null, (_, _) =>
        {
            var f = Engine.Settings.SyncFolder;
            if (f.Length > 0 && System.IO.Directory.Exists(f)) Process.Start(new ProcessStartInfo(f) { UseShellExecute = true });
        });
        var settings = new ToolStripMenuItem("Einstellungen …", null, (_, _) => ShowSettings());
        var quit = new ToolStripMenuItem("Beenden", null, (_, _) => Shutdown());

        menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem, new ToolStripSeparator(), courses, syncNow, openFolder,
            new ToolStripSeparator(), settings, quit,
        });
        menu.Opening += (_, _) =>
        {
            courses.DropDownItems.Clear();
            if (Engine.Courses.Count == 0)
                courses.DropDownItems.Add(new ToolStripMenuItem("Noch keine Kurse geladen") { Enabled = false });
            foreach (var c in Engine.Courses)
            {
                var course = c;
                courses.DropDownItems.Add(new ToolStripMenuItem(course.Title.Replace("&", "&&"), null, (_, _) =>
                {
                    try
                    {
                        using var client = Engine.CreateClient();
                        Process.Start(new ProcessStartInfo(client.CourseWebUrl(course).AbsoluteUri) { UseShellExecute = true });
                    }
                    catch { /* ungültige Server-Adresse */ }
                }));
            }
            syncNow.Enabled = !Engine.IsSyncing && Engine.Settings.IsConfigured;
        };

        _tray = new NotifyIcon
        {
            Icon = new Icon(iconStream),
            Visible = true,
            Text = "ILIAS Sync",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowSettings();
    }

    private void UpdateStatus()
    {
        if (_statusItem == null || _tray == null) return;
        var text = Engine.IsSyncing ? "Synchronisiere …" : Engine.Status;
        _statusItem.Text = text.Length > 80 ? text[..80] + "…" : text;
        _tray.Text = ("ILIAS Sync – " + text).Length > 63 ? ("ILIAS Sync – " + text)[..62] + "…" : "ILIAS Sync – " + text;
    }

    public void ShowSettings()
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        _settings.Activate();
    }
}
