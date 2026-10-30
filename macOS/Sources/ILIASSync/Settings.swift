import Foundation
import Security

enum Keychain {
    static let service = "de.iliassync.app"

    static func set(_ value: String, account: String) {
        let base: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
        SecItemDelete(base as CFDictionary)
        var add = base
        add[kSecValueData as String] = Data(value.utf8)
        SecItemAdd(add as CFDictionary, nil)
    }

    static func get(account: String) -> String? {
        let q: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        var out: AnyObject?
        guard SecItemCopyMatching(q as CFDictionary, &out) == errSecSuccess,
              let data = out as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }
}

final class Settings: ObservableObject {
    static let shared = Settings()
    private let d = UserDefaults.standard

    @Published var serverURL: String { didSet { d.set(serverURL, forKey: "serverURL") } }
    @Published var username: String { didSet { d.set(username, forKey: "username") } }
    @Published var syncFolder: String { didSet { d.set(syncFolder, forKey: "syncFolder") } }
    @Published var intervalMinutes: Int { didSet { d.set(intervalMinutes, forKey: "intervalMinutes") } }
    @Published var disabledCourses: [String] { didSet { d.set(disabledCourses, forKey: "disabledCourses") } }

    init() {
        serverURL = d.string(forKey: "serverURL") ?? ""
        username = d.string(forKey: "username") ?? ""
        syncFolder = d.string(forKey: "syncFolder") ?? ""
        let i = d.integer(forKey: "intervalMinutes")
        intervalMinutes = i == 0 ? 30 : i
        disabledCourses = d.stringArray(forKey: "disabledCourses") ?? []
    }

    var password: String {
        get { Keychain.get(account: username) ?? "" }
        set { Keychain.set(newValue, account: username) }
    }

    var isConfigured: Bool {
        !serverURL.isEmpty && !username.isEmpty && !syncFolder.isEmpty && !password.isEmpty
    }
}
