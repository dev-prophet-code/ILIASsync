// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "ILIASSync",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(name: "ILIASSync", path: "Sources/ILIASSync")
    ]
)
