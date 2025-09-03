#!/usr/bin/env bash
set -euo pipefail

# package-macos.sh
# Create a macOS .app bundle for a .NET single-file publish of the Avalonia app.
# Usage:
#  ./scripts/package-macos.sh -p src/YTP.MacUI -i path/to/AppIcon.icns -r osx-x64 -c Release -n YTP.MacUI
# Defaults: project=src/YTP.MacUI, rid=osx-x64, config=Release

usage() {
  cat <<EOF
Usage: $0 [-p project-path] [-i icon.icns] [-r rid] [-c config] [-n app-name]

-p project-path   Relative path to the csproj folder (default: src/YTP.MacUI)
-i icon.icns      Path to the .icns file to embed (recommended)
-r rid            Runtime identifier (osx-x64 or osx-arm64) (default: osx-x64)
-c config         Build configuration (Release/Debug) (default: Release)
-n app-name       Application name (default: derived from project folder)

Example:
  $0 -p src/YTP.MacUI -i src/YTP.MacUI/Assets/ytp.icns -r osx-x64

This script performs a single-file self-contained publish and packages the resulting
executable into a standard macOS .app bundle with an Info.plist and the provided icon.
EOF
}

PROJECT_DIR="src/YTP.MacUI"
ICON_PATH=""
RID="osx-x64"
CONFIG="Release"
APP_NAME=""

while getopts "p:i:r:c:n:h" opt; do
  case "$opt" in
    p) PROJECT_DIR="$OPTARG" ;;
    i) ICON_PATH="$OPTARG" ;;
    r) RID="$OPTARG" ;;
    c) CONFIG="$OPTARG" ;;
    n) APP_NAME="$OPTARG" ;;
    h|*) usage; exit 1 ;;
  esac
done

# Resolve paths
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_DIR_ABS="${ROOT_DIR}/${PROJECT_DIR}"

if [[ ! -d "$PROJECT_DIR_ABS" ]]; then
  echo "Project directory not found: $PROJECT_DIR_ABS"
  exit 2
fi

if [[ -z "$APP_NAME" ]]; then
  APP_NAME="$(basename "$PROJECT_DIR_ABS")"
fi

if [[ -n "$ICON_PATH" && ! -f "$ICON_PATH" ]]; then
  # try relative to project dir
  if [[ -f "$PROJECT_DIR_ABS/$ICON_PATH" ]]; then
    ICON_PATH="$PROJECT_DIR_ABS/$ICON_PATH"
  else
    echo "Icon file not found: $ICON_PATH"
    exit 3
  fi
fi

PUBLISH_BASE="$ROOT_DIR/artifacts/publish"
PUBLISH_DIR="$PUBLISH_BASE/${APP_NAME}-${RID}"
PUBLISH_OUT="$PUBLISH_DIR/publish"
APP_BUNDLE="$PUBLISH_DIR/${APP_NAME}.app"

echo "Project: $PROJECT_DIR_ABS"
echo "App name: $APP_NAME"
echo "RID: $RID"
echo "Configuration: $CONFIG"
echo "Publish output: $PUBLISH_OUT"

mkdir -p "$PUBLISH_OUT"

echo "Running dotnet publish (single-file, self-contained)..."
# Produce a single-file self-contained executable
dotnet publish "$PROJECT_DIR_ABS" -c "$CONFIG" -r "$RID" -p:PublishSingleFile=true -p:SelfContained=true -p:PublishTrimmed=false -o "$PUBLISH_OUT"

# Create .app bundle skeleton
MACOS_DIR="$APP_BUNDLE/Contents/MacOS"
RESOURCES_DIR="$APP_BUNDLE/Contents/Resources"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"

# Try to find the produced executable
EXECUTABLE="$PUBLISH_OUT/$APP_NAME"
if [[ -f "$EXECUTABLE" ]]; then
  echo "Found single-file executable: $EXECUTABLE"
  cp "$EXECUTABLE" "$MACOS_DIR/$APP_NAME"
  chmod +x "$MACOS_DIR/$APP_NAME"
else
  # Fallback: pick any executable-like file in publish folder
  candidate="$(ls "$PUBLISH_OUT" | grep -v '\.dll$' | head -n1 || true)"
  if [[ -n "$candidate" && -f "$PUBLISH_OUT/$candidate" ]]; then
    echo "Using candidate executable: $candidate"
    cp "$PUBLISH_OUT/$candidate" "$MACOS_DIR/$APP_NAME"
    chmod +x "$MACOS_DIR/$APP_NAME"
  else
    # Last resort: copy all published files into Resources and create a small launcher
    echo "No single-file executable found; copying all publish artifacts into Resources and creating launcher"
    cp -R "$PUBLISH_OUT"/* "$RESOURCES_DIR/"
    cat > "$MACOS_DIR/$APP_NAME" <<'SH'
#!/bin/bash
DIR="$(cd "$(dirname "$0")/../Resources" && pwd)"
# Try to run a same-named executable first, otherwise try the dll with dotnet
if [[ -x "$DIR/%APP%" ]]; then
  exec "$DIR/%APP%" "$@"
fi
if [[ -f "$DIR/%APP%.dll" ]]; then
  exec dotnet "$DIR/%APP%.dll" "$@"
fi
# fallback: try to run any file in Resources
exec "$DIR/$(ls "$DIR" | head -n1)" "$@"
SH
    # replace %APP% with actual app name
    sed -i '' "s/%APP%/$APP_NAME/g" "$MACOS_DIR/$APP_NAME" || sed -i "s/%APP%/$APP_NAME/g" "$MACOS_DIR/$APP_NAME"
    chmod +x "$MACOS_DIR/$APP_NAME"
  fi
fi

# Install icon
if [[ -n "$ICON_PATH" ]]; then
  ICON_BASENAME="$(basename "$ICON_PATH")"
  cp "$ICON_PATH" "$RESOURCES_DIR/$ICON_BASENAME"
  ICON_FILE_NAME="${ICON_BASENAME%.*}"
else
  ICON_FILE_NAME="AppIcon"
fi

# Write Info.plist
cat > "$APP_BUNDLE/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>$APP_NAME</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>com.ytp.${APP_NAME}</string>
  <key>CFBundleVersion</key>
  <string>1.0</string>
  <key>CFBundleExecutable</key>
  <string>$APP_NAME</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleIconFile</key>
  <string>$ICON_FILE_NAME</string>
  <key>LSMinimumSystemVersion</key>
  <string>10.14</string>
</dict>
</plist>
PLIST

echo "Created $APP_BUNDLE"

echo "Done. You can now copy the .app bundle to /Applications or double-click it to run."

echo "Artifact location: $APP_BUNDLE"
