using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ILIASSync.Core;

public sealed class Course
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "crs";   // crs | grp
    public string Title { get; set; } = "";
}

public sealed class IliasException : Exception
{
    public IliasException(string message) : base(message) { }
}

/// <summary>
/// Spricht ILIAS wie ein Browser an (Login-Formular, "Meine Kurse", goto.php). Liest nur – lädt nie etwas hoch
/// und löscht nie lokale Dateien.
/// </summary>
public sealed class IliasClient : IDisposable
{
    private readonly Uri _base;
    private readonly HttpClient _http;

    public IliasClient(string server)
    {
        var s = server.Trim();
        if (!s.Contains("://")) s = "https://" + s;
        foreach (var tail in new[] { "/login.php", "/ilias.php", "/index.php" })
            if (s.EndsWith(tail, StringComparison.OrdinalIgnoreCase)) s = s[..^tail.Length];
        s = s.TrimEnd('/');
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || string.IsNullOrEmpty(u.Host))
            throw new IliasException("Die Server-Adresse ist ungültig.");
        _base = u;

        var handler = new SocketsHttpHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = true,
            ResponseHeaderEncodingSelector = (_, _) => Encoding.UTF8,
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
    }

    public void Dispose() => _http.Dispose();

    // ---- URLs ----

    private Uri Url(string path, string query = "") =>
        new(_base.AbsoluteUri.TrimEnd('/') + "/" + path + (query.Length > 0 ? "?" + query : ""));

    private Uri GotoUrl(string type, string id, string suffix = "") => Url("goto.php", $"target={type}_{id}{suffix}");

    public Uri CourseWebUrl(Course c) => GotoUrl(c.Type, c.Id);

    private async Task<(string Html, Uri Url)> Fetch(Uri u)
    {
        using var resp = await _http.GetAsync(u);
        var html = await resp.Content.ReadAsStringAsync();
        return (html, resp.RequestMessage?.RequestUri ?? u);
    }

    // ---- Login ----

    public async Task Login(string username, string password)
    {
        var (html, pageUrl) = await Fetch(Url("login.php"));
        var form = Html.Form("type=\"password\"", html)
            ?? throw new IliasException("Anmeldeformular nicht gefunden – stimmt die Server-Adresse?");

        var fields = new List<KeyValuePair<string, string>>();
        bool userDone = false, passDone = false;
        foreach (var f in form.Fields)
        {
            if (f.Type == "hidden") fields.Add(new(f.Name, f.Value));
            else if (f.Type == "password" && !passDone) { fields.Add(new(f.Name, password)); passDone = true; }
            else if (f.Type == "text" && !userDone) { fields.Add(new(f.Name, username)); userDone = true; }
            else if (f.Type == "submit" && f.Name.StartsWith("cmd")) fields.Add(new(f.Name, f.Value));
        }
        if (!userDone || !passDone) throw new IliasException("Anmeldeformular nicht erkannt.");

        var target = form.Action != null ? new Uri(pageUrl, form.Action) : pageUrl;
        using var resp = await _http.PostAsync(target, new FormUrlEncodedContent(fields));
        var result = await resp.Content.ReadAsStringAsync();
        if (result.Contains("type=\"password\""))
        {
            var m = Regex.Match(result, "alert-danger[^>]*>(.{0,400})", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var msg = m.Success ? Html.StripTags(m.Groups[1].Value).Replace("Fehlermeldung", "").Split('<')[0].Trim() : "";
            throw new IliasException("Anmeldung fehlgeschlagen: " + (msg.Length == 0 ? "Anmeldename oder Passwort ungültig." : msg));
        }
    }

    // ---- Kurse ----

    public async Task<List<Course>> FetchCourses()
    {
        var pages = new[]
        {
            Url("ilias.php", "baseClass=ilmembershipoverviewgui"),
            Url("ilias.php", "baseClass=ilDashboardGUI&cmd=jumpToSelectedItems"),
        };
        foreach (var p in pages)
        {
            string html;
            try { html = (await Fetch(p)).Html; } catch { continue; }
            var order = new List<string>();
            var found = new Dictionary<string, Course>();
            foreach (var a in Html.Anchors(html))
            {
                var m = Regex.Match(a.Href, @"(?:target=|/)(crs|grp)(?:_|/)(\d+)");
                if (!m.Success) continue;
                var id = m.Groups[2].Value;
                if (!found.TryGetValue(id, out var c))
                {
                    order.Add(id);
                    found[id] = new Course { Id = id, Type = m.Groups[1].Value, Title = a.Text };
                }
                else if (c.Title.Length == 0) c.Title = a.Text;
            }
            var list = order.Select(i => found[i]).Where(c => c.Title.Length > 0).ToList();
            if (list.Count > 0) return list;
        }
        throw new IliasException("Keine Kurse gefunden (Seitenstruktur nicht erkannt oder keine Mitgliedschaften).");
    }

    // ---- Synchronisieren ----

    public async Task SyncCourse(Course c, string root, SyncState state, SyncStats stats)
    {
        var dir = Path.Combine(root, Sanitize(c.Title));
        await Crawl(c.Type, c.Id, dir, state, stats, new HashSet<string>(), 0);
    }

    private async Task Crawl(string type, string id, string dir, SyncState state, SyncStats stats,
                             HashSet<string> visited, int depth)
    {
        if (depth >= 12 || !visited.Add(id)) return;
        try
        {
            var (full, _) = await Fetch(GotoUrl(type, id));
            var html = full;
            var i = full.IndexOf("id=\"il_center_col\"", StringComparison.Ordinal);
            if (i < 0) i = full.IndexOf("<main", StringComparison.Ordinal);
            if (i >= 0) html = full[i..];

            var files = new List<(string Id, string Title)>();
            var folders = new List<(string Id, string Title)>();
            var seen = new HashSet<string>();
            foreach (var a in Html.Anchors(html).Where(a => a.Text.Length > 0))
            {
                var m = Regex.Match(a.Href, @"(?:target=file_|/file/)(\d+)");
                if (!m.Success && a.Href.Contains("cmd=sendfile")) m = Regex.Match(a.Href, @"ref_id=(\d+)");
                if (m.Success)
                {
                    if (seen.Add("f" + m.Groups[1].Value)) files.Add((m.Groups[1].Value, a.Text));
                    continue;
                }
                var f = Regex.Match(a.Href, @"(?:target=fold_|/fold/)(\d+)");
                if (f.Success && f.Groups[1].Value != id && seen.Add("d" + f.Groups[1].Value))
                    folders.Add((f.Groups[1].Value, a.Text));
            }

            foreach (var (fid, title) in files)
            {
                try { await Download(fid, title, dir, state, stats); }
                catch (Exception ex) { stats.Errors.Add($"{title}: {ex.Message}"); }
            }
            foreach (var (did, title) in folders)
                await Crawl("fold", did, Path.Combine(dir, Sanitize(title)), state, stats, visited, depth + 1);
        }
        catch (Exception ex)
        {
            stats.Errors.Add($"{Path.GetFileName(dir)}: {ex.Message}");
        }
    }

    private async Task Download(string id, string title, string dir, SyncState state, SyncStats stats)
    {
        var u = GotoUrl("file", id, "_download");
        using var resp = await _http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead);
        if (resp.StatusCode != HttpStatusCode.OK) return;
        if (resp.Content.Headers.ContentType?.MediaType?.Contains("text/html") == true) return;

        var cd = resp.Content.Headers.ContentDisposition;
        var name = Sanitize(cd?.FileNameStar ?? cd?.FileName?.Trim('"') ?? title);
        var sig = (resp.Content.Headers.LastModified?.ToString("R") ?? "") + "|" +
                  (resp.Content.Headers.ContentLength?.ToString() ?? "");
        var dest = Path.Combine(dir, name);
        var key = "file_" + id;
        var local = File.Exists(dest);

        var entry = state.Get(key);
        if (entry != null && entry.Sig == sig && entry.Path == dest && local)
        {
            stats.Unchanged++;
            return;   // unveränderte Datei: Antwort wird verworfen, nichts geladen
        }

        // Datei in eine temporäre Datei laden und per SHA-256 mit dem Vorhandenen abgleichen.
        var tmp = Path.GetTempFileName();
        try
        {
            await using (var fs = File.Create(tmp))
                await resp.Content.CopyToAsync(fs);
            var remoteHash = Sha256(tmp);

            if (local && Sha256(dest) == remoteHash)
            {
                state.Set(key, new SyncEntry(sig, dest, remoteHash));
                stats.Unchanged++;
                return;
            }
            if (entry?.Hash == remoteHash && (entry.Path == dest || File.Exists(entry.Path)))
            {
                state.Set(key, entry with { Sig = sig });
                stats.Unchanged++;
                return;
            }

            Directory.CreateDirectory(dir);
            var target = dest;
            if (local)
            {
                // Nie überschreiben: neue Version daneben ablegen.
                var baseName = Path.GetFileNameWithoutExtension(dest);
                var ext = Path.GetExtension(dest);
                var stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm");
                target = Path.Combine(dir, $"{baseName} (neue Version {stamp}){ext}");
                for (var n = 2; File.Exists(target); n++)
                    target = Path.Combine(dir, $"{baseName} (neue Version {stamp} {n}){ext}");
            }
            File.Move(tmp, target);
            state.Set(key, new SyncEntry(sig, target, remoteHash));
            if (local) stats.Updated++; else stats.New++;
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);   // nur die temporäre Download-Datei
        }
    }

    private static string Sha256(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
    }

    public static string Sanitize(string s)
    {
        var invalid = new string(Path.GetInvalidFileNameChars()) + "/:\\";
        var r = new string(s.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim().TrimEnd('.', ' ');
        if (r.StartsWith('.')) r = "_" + r;
        return r.Length == 0 ? "Unbenannt" : r;
    }
}
