using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ILIASSync.Core;

public sealed class Settings
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ILIASSync");

    private static string FilePath => Path.Combine(DataDir, "settings.json");

    public string ServerUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string SyncFolder { get; set; } = "";
    public int IntervalMinutes { get; set; } = 30;
    public List<string> DisabledCourses { get; set; } = new();

    /// <summary>Passwort, mit Windows-DPAPI (aktueller Benutzer) verschlüsselt.</summary>
    public string EncryptedPassword { get; set; } = "";

    [JsonIgnore]
    public string Password
    {
        get
        {
            if (EncryptedPassword.Length == 0) return "";
            try
            {
                var raw = ProtectedData.Unprotect(Convert.FromBase64String(EncryptedPassword), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(raw);
            }
            catch { return ""; }
        }
        set => EncryptedPassword = value.Length == 0 ? "" : Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    }

    [JsonIgnore]
    public bool IsConfigured =>
        ServerUrl.Length > 0 && Username.Length > 0 && SyncFolder.Length > 0 && EncryptedPassword.Length > 0;

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); }
        catch { return new Settings(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
