import Foundation
import CryptoKit

struct Course: Identifiable, Codable, Hashable {
    let id: String        // ref_id
    let type: String      // crs | grp
    var title: String
}

struct SyncStats {
    var new = 0, updated = 0, unchanged = 0
    var errors: [String] = []
}

enum ILIASError: LocalizedError {
    case badURL, loginFailed(String), noCourses
    var errorDescription: String? {
        switch self {
        case .badURL: return "Die Server-Adresse ist ungültig."
        case .loginFailed(let m): return "Anmeldung fehlgeschlagen" + (m.isEmpty ? "." : ": \(m)")
        case .noCourses: return "Keine Kurse gefunden (Seitenstruktur nicht erkannt oder keine Mitgliedschaften)."
        }
    }
}

struct SyncEntry: Codable { var sig: String; var path: String; var hash: String? }

final class SyncState {
    private(set) var entries: [String: SyncEntry]
    private let url: URL

    init() {
        let dir = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("ILIASSync", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        url = dir.appendingPathComponent("state.json")
        entries = (try? JSONDecoder().decode([String: SyncEntry].self, from: Data(contentsOf: url))) ?? [:]
    }
    subscript(key: String) -> SyncEntry? {
        get { entries[key] }
        set { entries[key] = newValue }
    }
    func save() { try? JSONEncoder().encode(entries).write(to: url) }
}

final class ILIASClient {
    let base: URL
    private let session: URLSession

    init(server: String) throws {
        var s = server.trimmingCharacters(in: .whitespacesAndNewlines)
        if !s.contains("://") { s = "https://" + s }
        for tail in ["/login.php", "/ilias.php", "/index.php"] where s.hasSuffix(tail) { s.removeLast(tail.count) }
        while s.hasSuffix("/") { s.removeLast() }
        guard let u = URL(string: s), u.host != nil else { throw ILIASError.badURL }
        base = u
        let cfg = URLSessionConfiguration.ephemeral
        cfg.httpCookieAcceptPolicy = .always
        cfg.timeoutIntervalForRequest = 60
        session = URLSession(configuration: cfg)
    }

    // MARK: URLs

    private func url(_ path: String, _ query: String = "") -> URL {
        var q = query
        return URL(string: base.absoluteString + "/" + path + (q.isEmpty ? "" : "?" + q))!
    }
    func gotoURL(_ type: String, _ ref: String, suffix: String = "") -> URL {
        url("goto.php", "target=\(type)_\(ref)\(suffix)")
    }
    func courseWebURL(_ c: Course) -> URL { gotoURL(c.type, c.id) }

    private func fetch(_ u: URL) async throws -> (html: String, url: URL) {
        let (data, resp) = try await session.data(from: u)
        let html = String(data: data, encoding: .utf8) ?? String(decoding: data, as: UTF8.self)
        return (html, resp.url ?? u)
    }

    // MARK: Login

    func login(username: String, password: String) async throws {
        let (html, pageURL) = try await fetch(url("login.php"))
        guard let form = HTML.form(containing: "type=\"password\"", in: html) else {
            throw ILIASError.loginFailed("Anmeldeformular nicht gefunden – stimmt die Server-Adresse?")
        }
        var fields: [(String, String)] = []
        var userDone = false, passDone = false
        for f in form.fields {
            switch f.type {
            case "hidden": fields.append((f.name, f.value))
            case "password" where !passDone: fields.append((f.name, password)); passDone = true
            case "text" where !userDone: fields.append((f.name, username)); userDone = true
            case "submit" where f.name.hasPrefix("cmd"): fields.append((f.name, f.value))
            default: break
            }
        }
        guard userDone, passDone else { throw ILIASError.loginFailed("Anmeldeformular nicht erkannt.") }

        let target = form.action.flatMap { URL(string: $0, relativeTo: pageURL)?.absoluteURL } ?? pageURL
        var req = URLRequest(url: target)
        req.httpMethod = "POST"
        req.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        let allowed = CharacterSet.alphanumerics.union(CharacterSet(charactersIn: "-._~"))
        req.httpBody = fields.map {
            "\($0.0.addingPercentEncoding(withAllowedCharacters: allowed)!)=\($0.1.addingPercentEncoding(withAllowedCharacters: allowed)!)"
        }.joined(separator: "&").data(using: .utf8)

        let (data, _) = try await session.data(for: req)
        let result = String(decoding: data, as: UTF8.self)
        if result.contains("type=\"password\"") {
            let msg = HTML.matches("alert-danger[^>]*>(.{0,400})", in: result).first
                .map { HTML.stripTags($0[1]).replacingOccurrences(of: "Fehlermeldung", with: "").components(separatedBy: "<")[0].trimmingCharacters(in: .whitespaces) } ?? ""
            throw ILIASError.loginFailed(msg.isEmpty ? "Anmeldename oder Passwort ungültig." : msg)
        }
    }

    // MARK: Courses

    func fetchCourses() async throws -> [Course] {
        let pages = [url("ilias.php", "baseClass=ilmembershipoverviewgui"),
                     url("ilias.php", "baseClass=ilDashboardGUI&cmd=jumpToSelectedItems")]
        for p in pages {
            guard let (html, _) = try? await fetch(p) else { continue }
            var order: [String] = []
            var found: [String: Course] = [:]
            for a in HTML.anchors(in: html) {
                guard let m = HTML.matches("(?:target=|/)(crs|grp)(?:_|/)(\\d+)", in: a.href).first else { continue }
                let id = m[2]
                if found[id] == nil { order.append(id); found[id] = Course(id: id, type: m[1], title: a.text) }
                else if found[id]!.title.isEmpty { found[id]!.title = a.text }
            }
            let list = order.compactMap { found[$0] }.filter { !$0.title.isEmpty }
            if !list.isEmpty { return list }
        }
        throw ILIASError.noCourses
    }

    // MARK: Sync

    func syncCourse(_ c: Course, into root: URL, state: SyncState, stats: inout SyncStats) async {
        let dir = root.appendingPathComponent(Self.sanitize(c.title), isDirectory: true)
        var visited = Set<String>()
        await crawl(type: c.type, ref: c.id, dir: dir, state: state, stats: &stats, visited: &visited, depth: 0)
    }

    private func crawl(type: String, ref: String, dir: URL, state: SyncState, stats: inout SyncStats,
                       visited: inout Set<String>, depth: Int) async {
        guard depth < 12, visited.insert(ref).inserted else { return }
        do {
            let (full, pageURL) = try await fetch(gotoURL(type, ref))
            let base = HTML.baseHref(in: full).flatMap { URL(string: $0) } ?? pageURL
            _ = base
            var html = full
            if let r = full.range(of: "id=\"il_center_col\"") ?? full.range(of: "<main") {
                html = String(full[r.lowerBound...])
            }
            var files: [(String, String)] = []
            var folders: [(String, String)] = []
            var seen = Set<String>()
            for a in HTML.anchors(in: html) where !a.text.isEmpty {
                if let m = HTML.matches("(?:target=file_|/file/)(\\d+)", in: a.href).first
                    ?? (a.href.contains("cmd=sendfile") ? HTML.matches("ref_id=(\\d+)", in: a.href).first : nil) {
                    if seen.insert("f" + m[1]).inserted { files.append((m[1], a.text)) }
                } else if let m = HTML.matches("(?:target=fold_|/fold/)(\\d+)", in: a.href).first, m[1] != ref {
                    if seen.insert("d" + m[1]).inserted { folders.append((m[1], a.text)) }
                }
            }
            for (fref, title) in files {
                do { try await download(ref: fref, title: title, dir: dir, state: state, stats: &stats) }
                catch { stats.errors.append("\(title): \(error.localizedDescription)") }
            }
            for (dref, title) in folders {
                await crawl(type: "fold", ref: dref, dir: dir.appendingPathComponent(Self.sanitize(title), isDirectory: true),
                            state: state, stats: &stats, visited: &visited, depth: depth + 1)
            }
        } catch {
            stats.errors.append("\(dir.lastPathComponent): \(error.localizedDescription)")
        }
    }

    private func download(ref: String, title: String, dir: URL, state: SyncState, stats: inout SyncStats) async throws {
        let u = gotoURL("file", ref, suffix: "_download")
        let (bytes, resp) = try await session.bytes(from: u)
        guard let http = resp as? HTTPURLResponse, http.statusCode == 200 else { bytes.task.cancel(); return }
        if (http.value(forHTTPHeaderField: "Content-Type") ?? "").contains("text/html") { bytes.task.cancel(); return }

        let name = Self.sanitize(Self.filename(from: http.value(forHTTPHeaderField: "Content-Disposition")) ?? title)
        let sig = (http.value(forHTTPHeaderField: "Last-Modified") ?? "") + "|" + (http.value(forHTTPHeaderField: "Content-Length") ?? "")
        let dest = dir.appendingPathComponent(name)
        let key = "file_\(ref)"
        let fm = FileManager.default
        let local = fm.fileExists(atPath: dest.path)
        if let e = state[key], e.sig == sig, e.path == dest.path, local {
            bytes.task.cancel(); stats.unchanged += 1; return
        }
        bytes.task.cancel()

        // Datei laden und per SHA-256 mit dem Vorhandenen abgleichen.
        let (tmp, _) = try await session.download(from: u)
        defer { try? fm.removeItem(at: tmp) }   // nur die temporäre Download-Datei
        let remoteHash = try Self.sha256(tmp)

        if local, try Self.sha256(dest) == remoteHash {
            state[key] = SyncEntry(sig: sig, path: dest.path, hash: remoteHash)
            stats.unchanged += 1; return
        }
        if let e = state[key], e.hash == remoteHash, e.path == dest.path || fm.fileExists(atPath: e.path) {
            state[key] = SyncEntry(sig: sig, path: e.path, hash: remoteHash)
            stats.unchanged += 1; return
        }

        try fm.createDirectory(at: dir, withIntermediateDirectories: true)
        var target = dest
        if local {
            // Nie überschreiben: neue Version daneben ablegen.
            let f = DateFormatter(); f.dateFormat = "yyyy-MM-dd HH-mm"
            let ext = dest.pathExtension
            var n = dest.deletingPathExtension().lastPathComponent + " (neue Version \(f.string(from: Date())))"
            if !ext.isEmpty { n += "." + ext }
            target = dir.appendingPathComponent(n)
            var i = 2
            while fm.fileExists(atPath: target.path) {
                target = dir.appendingPathComponent(n.replacingOccurrences(of: ")", with: " \(i))", options: .backwards, range: nil)); i += 1
            }
        }
        try fm.moveItem(at: tmp, to: target)
        state[key] = SyncEntry(sig: sig, path: target.path, hash: remoteHash)
        if local { stats.updated += 1 } else { stats.new += 1 }
    }

    static func sha256(_ url: URL) throws -> String {
        let h = try FileHandle(forReadingFrom: url)
        defer { try? h.close() }
        var hasher = SHA256()
        while let chunk = try h.read(upToCount: 1 << 20), !chunk.isEmpty { hasher.update(data: chunk) }
        return hasher.finalize().map { String(format: "%02x", $0) }.joined()
    }

    static func filename(from header: String?) -> String? {
        guard let h = header else { return nil }
        if let m = HTML.matches("filename\\*\\s*=\\s*(?:UTF-8|utf-8)''([^;]+)", in: h).first,
           let d = m[1].removingPercentEncoding { return d }
        guard var n = HTML.matches("filename\\s*=\\s*\"?([^\";]+)\"?", in: h).first?[1] else { return nil }
        if n.contains("Ã"), let d = n.data(using: .isoLatin1), let fixed = String(data: d, encoding: .utf8) { n = fixed }
        return n
    }

    static func sanitize(_ s: String) -> String {
        var r = s.replacingOccurrences(of: "[/:\\\\]", with: "-", options: .regularExpression)
            .trimmingCharacters(in: .whitespacesAndNewlines)
        if r.hasPrefix(".") { r = "_" + r }
        return r.isEmpty ? "Unbenannt" : r
    }
}
