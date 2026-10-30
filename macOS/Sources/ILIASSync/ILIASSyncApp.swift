import SwiftUI
import AppKit
import UserNotifications

final class AppDelegate: NSObject, NSApplicationDelegate, UNUserNotificationCenterDelegate {
    static var window: NSWindow?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        if Bundle.main.bundleURL.pathExtension == "app" {
            let c = UNUserNotificationCenter.current()
            c.delegate = self
            c.requestAuthorization(options: [.alert, .sound]) { _, _ in }
        }
        Task { @MainActor in
            SyncEngine.shared.start()
            if !Settings.shared.isConfigured { Self.showSettings() }
        }
    }

    // Hinweis "Neue Daten synchronisiert" auch anzeigen, wenn die App im Vordergrund ist.
    func userNotificationCenter(_ center: UNUserNotificationCenter, willPresent notification: UNNotification,
                                withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
        completionHandler([.banner, .list])
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        Task { @MainActor in Self.showSettings() }
        return true
    }

    @MainActor static func showSettings() {
        if window == nil {
            let w = NSWindow(contentViewController: NSHostingController(rootView: SettingsView()))
            w.title = "ILIAS Sync – Einstellungen"
            w.styleMask = [.titled, .closable]
            w.isReleasedWhenClosed = false
            w.center()
            window = w
        }
        NSApp.activate(ignoringOtherApps: true)
        window?.makeKeyAndOrderFront(nil)
    }
}

@main
struct ILIASSyncApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject private var engine = SyncEngine.shared

    var body: some Scene {
        MenuBarExtra {
            MenuView(engine: engine)
        } label: {
            Image(systemName: engine.isSyncing ? "arrow.triangle.2.circlepath" : "books.vertical")
        }
        .menuBarExtraStyle(.menu)
    }
}

struct MenuView: View {
    @ObservedObject var engine: SyncEngine

    var body: some View {
        Text(engine.status)
        if let d = engine.lastSync {
            Text("Zuletzt: \(d.formatted(date: .omitted, time: .shortened))")
        }
        Divider()
        Menu("Meine Kurse") {
            if engine.courses.isEmpty { Text("Noch keine Kurse geladen") }
            ForEach(engine.courses) { c in
                Button(c.title) {
                    if let u = try? engine.makeClient().courseWebURL(c) { NSWorkspace.shared.open(u) }
                }
            }
        }
        Button("Jetzt synchronisieren") { Task { await engine.syncNow() } }
            .disabled(engine.isSyncing || !engine.settings.isConfigured)
        Button("Sync-Ordner öffnen") {
            NSWorkspace.shared.open(URL(fileURLWithPath: engine.settings.syncFolder))
        }.disabled(engine.settings.syncFolder.isEmpty)
        Divider()
        Button("Einstellungen …") { AppDelegate.showSettings() }.keyboardShortcut(",")
        Button("Beenden") { NSApp.terminate(nil) }.keyboardShortcut("q")
    }
}
