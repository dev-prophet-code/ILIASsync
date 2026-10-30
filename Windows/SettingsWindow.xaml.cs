using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ILIASSync.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ILIASSync;

public partial class SettingsWindow : Window
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "ILIASSync";
    private string _folder;

    public SettingsWindow()
    {
        InitializeComponent();
        var s = App.Engine.Settings;
        ServerBox.Text = s.ServerUrl;
        UserBox.Text = s.Username;
        PasswordBox.Password = s.Password;
        _folder = s.SyncFolder;
        ShowFolder();

        foreach (var m in new[] { 5, 15, 30, 60, 180 })
            IntervalBox.Items.Add(new ComboBoxItem { Content = $"alle {m} Min.", Tag = m });
        IntervalBox.SelectedItem = IntervalBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == s.IntervalMinutes)
                                   ?? IntervalBox.Items[2];

        using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
            AutostartBox.IsChecked = key?.GetValue(RunName) != null;
        AutostartBox.Click += (_, _) => SetAutostart(AutostartBox.IsChecked == true);

        BuildCourseList();
    }

    private void ShowFolder()
    {
        FolderText.Text = _folder.Length == 0 ? "Kein Ordner gewählt" : _folder;
    }

    private static void SetAutostart(bool on)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key == null) return;
        if (on) key.SetValue(RunName, "\"" + Environment.ProcessPath + "\"");
        else key.DeleteValue(RunName, throwOnMissingValue: false);
    }

    private void BuildCourseList()
    {
        var s = App.Engine.Settings;
        CoursesList.Items.Clear();
        foreach (var c in App.Engine.Courses)
        {
            var course = c;
            var cb = new CheckBox { Content = course.Title, IsChecked = !s.DisabledCourses.Contains(course.Id), Margin = new Thickness(0, 2, 0, 2) };
            cb.Click += (_, _) =>
            {
                s.DisabledCourses.RemoveAll(x => x == course.Id);
                if (cb.IsChecked != true) s.DisabledCourses.Add(course.Id);
                s.Save();
            };
            CoursesList.Items.Add(cb);
        }
        CoursesPanel.Visibility = CoursesList.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new Forms.FolderBrowserDialog { ShowNewFolderButton = true, SelectedPath = _folder };
        if (dlg.ShowDialog() == Forms.DialogResult.OK)
        {
            _folder = dlg.SelectedPath;
            ShowFolder();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var server = ServerBox.Text.Trim();
        var user = UserBox.Text.Trim();
        var pass = PasswordBox.Password;
        if (server.Length == 0 || user.Length == 0 || pass.Length == 0 || _folder.Length == 0)
        {
            MessageText.Text = "Bitte alle Felder ausfüllen und einen Sync-Ordner wählen.";
            return;
        }

        SaveButton.IsEnabled = false;
        MessageText.Text = "Verbinde …";
        try
        {
            using var client = new IliasClient(server);
            await client.Login(user, pass);
            var courses = await client.FetchCourses();

            var s = App.Engine.Settings;
            s.ServerUrl = server;
            s.Username = user;
            s.Password = pass;
            s.SyncFolder = _folder;
            s.IntervalMinutes = (int)((ComboBoxItem)IntervalBox.SelectedItem).Tag;
            s.Save();

            App.Engine.Courses = courses;
            BuildCourseList();
            MessageText.Text = $"Verbunden – {courses.Count} Kurse gefunden. Synchronisierung läuft.";
            _ = App.Engine.SyncNow();
        }
        catch (Exception ex)
        {
            MessageText.Text = ex.Message;
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }
}
