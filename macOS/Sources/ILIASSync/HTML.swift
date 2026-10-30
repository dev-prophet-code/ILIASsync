import Foundation

enum HTML {
    struct Anchor { let href: String; let text: String }

    static func matches(_ pattern: String, in s: String, options: NSRegularExpression.Options = [.caseInsensitive, .dotMatchesLineSeparators]) -> [[String]] {
        guard let re = try? NSRegularExpression(pattern: pattern, options: options) else { return [] }
        let ns = s as NSString
        return re.matches(in: s, range: NSRange(location: 0, length: ns.length)).map { m in
            (0..<m.numberOfRanges).map { i in
                let r = m.range(at: i)
                return r.location == NSNotFound ? "" : ns.substring(with: r)
            }
        }
    }

    static func decode(_ s: String) -> String {
        var r = s
        for (k, v) in ["&amp;": "&", "&quot;": "\"", "&#039;": "'", "&#39;": "'", "&lt;": "<", "&gt;": ">", "&nbsp;": " "] {
            r = r.replacingOccurrences(of: k, with: v)
        }
        return r
    }

    static func stripTags(_ s: String) -> String {
        let t = s.replacingOccurrences(of: "<[^>]+>", with: " ", options: .regularExpression)
        return decode(t).replacingOccurrences(of: "\\s+", with: " ", options: .regularExpression)
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    static func anchors(in html: String) -> [Anchor] {
        matches("<a\\b[^>]*?href\\s*=\\s*\"([^\"]*)\"[^>]*>(.*?)</a>", in: html).map {
            Anchor(href: decode($0[1]), text: stripTags($0[2]))
        }
    }

    static func baseHref(in html: String) -> String? {
        matches("<base\\b[^>]*?href\\s*=\\s*\"([^\"]*)\"", in: html).first.map { decode($0[1]) }
    }

    static func attr(_ name: String, in tag: String) -> String? {
        matches("(?<![\\w-])\(name)\\s*=\\s*\"([^\"]*)\"", in: tag).first.map { decode($0[1]) }
    }

    /// Returns the form (action, fields) that contains a field called `containing`.
    static func form(containing field: String, in html: String) -> (action: String?, fields: [(name: String, value: String, type: String)])? {
        for f in matches("<form\\b([^>]*)>(.*?)</form>", in: html) {
            guard f[2].contains(field) else { continue }
            let inputs = matches("<(?:input|button)\\b[^>]*>", in: f[2]).compactMap { m -> (String, String, String)? in
                guard let name = attr("name", in: m[0]) else { return nil }
                return (name, attr("value", in: m[0]) ?? "", (attr("type", in: m[0]) ?? "text").lowercased())
            }
            return (attr("action", in: "<form " + f[1] + ">"), inputs)
        }
        return nil
    }
}
