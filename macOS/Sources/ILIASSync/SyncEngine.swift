import Foundation
import AppKit
import UserNotifications

@MainActor
final class SyncEngine: ObservableObject {
    static let shared = SyncEngine()
    let settings = Settings.shared

    @Published var courses: [Course] = []
    @Published var isSyncing = false
    @Published var status = "Noch nicht synchronisiert"
    @Published var lastSync: Date?

    private var loop: Task<Void, Never>?
    private var lastAttempt: Date?
    private var loginBlocked = false   // Zugangsdaten abgelehnt: nicht automatisch weiter versuchen (Konto-Sperre)

    /// Nach geänderten Zugangsdaten wieder automatische Syncs erlauben.
    func resetLoginBlock() { loginBlocked = false }

    func start() {
        loop?.cancel()
        loop = Task { [weak self] in
            while !Task.isCancelled {
                guard let self else { return }
                let due = self.lastAttempt.map { Date().timeIntervalSince($0) >= Double(self.settings.intervalMinutes) * 60 } ?? true
                if due && !self.loginBlocked && self.settings.isConfigured { await self.syncNow() }
                try? await Task.sleep(nanoseconds: 60 * 1_000_000_000)
            }
        }
    }

    func makeClient() throws -> ILIASClient {
        try ILIASClient(server: settings.serverURL)
    }

    func syncNow() async {
        guard !isSyncing, settings.isConfigured else { return }
        isSyncing = true
        lastAttempt = Date()
        status = "Synchronisiere …"
        defer { isSyncing = false }
        do {
            let client = try makeClient()
            try await client.login(username: settings.username, password: settings.password)
            loginBlocked = false
            let list = try await client.fetchCourses()
            courses = list
            let root = URL(fileURLWithPath: settings.syncFolder, isDirectory: true)
            let state = SyncState()
            var stats = SyncStats()
            for c in list where !settings.disabledCourses.contains(c.id) {
                await client.syncCourse(c, into: root, state: state, stats: &stats)
            }
            state.save()
            lastSync = Date()
            let changed = stats.new + stats.updated
            status = changed > 0 ? "\(stats.new) neu, \(stats.updated) aktualisiert" : "Alles aktuell"
            if !stats.errors.isEmpty { status += " (\(stats.errors.count) Fehler)" }
            if changed > 0 { notify("Neue Daten synchronisiert", "\(stats.new) neue, \(stats.updated) aktualisierte Datei(en)") }
        } catch let e as ILIASError {
            if case .loginFailed = e {
                loginBlocked = true
                status = e.localizedDescription + " (automatische Syncs pausiert)"
            } else {
                status = e.localizedDescription
            }
        } catch {
            status = error.localizedDescription
        }
    }

    private func notify(_ title: String, _ body: String) {
        guard Bundle.main.bundleURL.pathExtension == "app" else { return }
        let n = UNMutableNotificationContent()
        n.title = title; n.body = body
        // Berechtigung wird beim Start angefragt (AppDelegate); ohne Erlaubnis zeigt macOS nichts an.
        UNUserNotificationCenter.current().add(UNNotificationRequest(identifier: UUID().uuidString, content: n, trigger: nil))
    }
}
