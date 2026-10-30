# Baut eine einzelne, eigenständige ILIASSync.exe (win-x64, .NET muss nicht installiert sein).
# Voraussetzung: .NET 8 SDK  (https://dotnet.microsoft.com/download)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none -o build
Write-Host "Fertig: $PSScriptRoot\build\ILIASSync.exe"
