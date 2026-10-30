import SwiftUI
import AppKit
import ServiceManagement

struct SettingsView: View {
    @ObservedObject var settings = Settings.shared
    @ObservedObject var engine = SyncEngine.shared

    @State private var server = Settings.shared.serverURL
    @State private var username = Settings.shared.username
    @State private var password = Settings.shared.password
    @State private var folder = Settings.shared.syncFolder
    @State private var message = ""
    @State private var busy = false
    @State private var loginItem = SMAppService.mainApp.status == .enabled

    var body: some View {
        Form {
            Section("Server") {
                TextField("ILIAS-Adresse", text: $server, prompt: Text("https://ilias.uni-beispiel.de"))
            }
            Section("Zugangsdaten") {
                TextField("Benutzername", text: $username)
                SecureField("Passwort", text: $password)
            }
            Section("Synchronisierung") {
                HStack {
                    Text(folder.isEmpty ? "Kein Ordner gewählt" : folder)
                        .lineLimit(1).truncationMode(.middle).foregroundStyle(.secondary)
                    Spacer()
                    Button("Wählen …") { chooseFolder() }
                }
                Picker("Intervall", selection: $settings.intervalMinutes) {
                    ForEach([5, 15, 30, 60, 180], id: \.self) { Text("alle \($0) Min.").tag($0) }
                }
                Toggle("Beim Anmelden starten", isOn: $loginItem)
                    .onChange(of: loginItem) { on in
                        try? on ? SMAppService.mainApp.register() : SMAppService.mainApp.unregister()
                    }
            }
            if !engine.courses.isEmpty {
                Section("Kurse synchronisieren") {
                    ForEach(engine.courses) { c in
                        Toggle(c.title, isOn: Binding(
                            get: { !settings.disabledCourses.contains(c.id) },
                            set: { on in
                                settings.disabledCourses.removeAll { $0 == c.id }
                                if !on { settings.disabledCourses.append(c.id) }
                            }))
                    }
                }
            }
            Section {
                HStack {
                    Button("Speichern & Verbinden") { Task { await save() } }
                        .disabled(busy || server.isEmpty || username.isEmpty || password.isEmpty || folder.isEmpty)
                    if busy { ProgressView().controlSize(.small) }
                }
                if !message.isEmpty { Text(message).font(.callout).foregroundStyle(.secondary) }
            }
        }
        .formStyle(.grouped)
        .frame(width: 480, height: 560)
    }

    private func chooseFolder() {
        let p = NSOpenPanel()
        p.canChooseDirectories = true; p.canChooseFiles = false; p.canCreateDirectories = true
        if p.runModal() == .OK, let u = p.url { folder = u.path }
    }

    private func save() async {
        busy = true; defer { busy = false }
        message = "Verbinde …"
        do {
            let client = try ILIASClient(server: server)
            try await client.login(username: username, password: password)
            let courses = try await client.fetchCourses()
            settings.serverURL = server
            settings.username = username; settings.password = password
            settings.syncFolder = folder
            engine.courses = courses
            message = "Verbunden – \(courses.count) Kurse gefunden. Synchronisierung läuft."
            Task { await engine.syncNow() }
        } catch {
            message = error.localizedDescription
        }
    }
}
