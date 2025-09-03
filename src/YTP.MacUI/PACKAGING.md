Packaging the macOS app (.app) for YTP.MacUI

This document explains how to produce a macOS .app bundle from the Avalonia-based
`YTP.MacUI` project in this repository. The included script `scripts/package-macos.sh`
performs a self-contained single-file publish and wraps the result into a standard
macOS bundle with an Info.plist and your `.icns` icon.

Prerequisites
- .NET SDK installed (dotnet 6/7/8/9 as used by the project). Ensure `dotnet` is on your PATH.
- macOS machine (or runner) matching the target RID (`osx-x64` or `osx-arm64`).
- Optional: a valid `.icns` file (recommended) — place it somewhere in the repo, for example `src/YTP.MacUI/Assets/YTP.icns`.

Quick steps (example)

1. Make sure the icon is added (optional):

   cp /path/to/your/YTP.icns src/YTP.MacUI/Assets/YTP.icns

2. Run the packaging script from the repo root (example for Intel macs):

```bash
./scripts/package-macos.sh -p src/YTP.MacUI -i src/YTP.MacUI/Assets/YTP.icns -r osx-x64 -c Release
```

For Apple Silicon (arm64):

```bash
./scripts/package-macos.sh -p src/YTP.MacUI -i src/YTP.MacUI/Assets/YTP.icns -r osx-arm64 -c Release
```

The produced .app bundle will be located under `artifacts/publish/YTP.MacUI-<rid>/YTP.MacUI.app`.

Notes and troubleshooting
- The script attempts to publish a single-file, self-contained executable. If the publish produces
  a single executable it will be copied to `Contents/MacOS/<AppName>` inside the .app.
  If a single-file executable cannot be found, the script falls back to copying all publish
  artifacts into `Contents/Resources` and creates a small launcher script that executes the
  appropriate file.

- Code signing & notarization
  If you want to distribute the app outside your machine you should sign and notarize it with
  an Apple Developer account. The script does not perform signing or notarization. Example signing commands:

```bash
# sign
codesign --deep --force --verify --verbose --sign "Developer ID Application: <Your Name> (TEAMID)" \
  artifacts/publish/YTP.MacUI-osx-x64/YTP.MacUI.app

# create zip
ditto -c -k --sequesterRsrc --keepParent artifacts/publish/YTP.MacUI-osx-x64/YTP.MacUI.app YTP.MacUI.zip

# notarize (requires altool or notarytool configured)
# xcrun altool --notarize-app -u <apple-id> -p <app-specific-password> -f YTP.MacUI.zip --primary-bundle-id "com.ytp.YTP.MacUI"
```

- Universal builds
  To produce a universal app supporting both x64 and arm64 you can publish both RIDs and
  then create a universal binary with `lipo` for native executables. For single-file dotnet
  publishes this may be non-trivial; an easier option is to distribute separate builds per-arch.

Questions or edits
If you'd like I can:
- Add a script that automates codesigning and notarization (requires Apple credentials), or
- Create a GitHub Actions workflow that builds and uploads signed artifacts, or
- Adjust the script to produce a .pkg installer.

