# YTP Uno Port — Notes & Build Steps

This directory contains the Uno UI port for the YTP app. The platform targets include macCatalyst and WebAssembly (WASM). On macOS you may encounter SDK/workload issues; follow these steps.

Quick checklist to get WASM building (zsh):

1. Ensure .NET 9 SDK is installed (dotnet --info).
2. Install the wasm workload (you already have this):

```bash
dotnet workload install wasm-tools
dotnet workload install wasi-experimental
```

3. (Uno tool) Uno.Wasm.Bootstrap is optional for local hosting; if `dotnet tool install -g Uno.Wasm.Bootstrap` fails, you can still build the Wasm target and manually serve the output. The tool is mostly for convenience.

4. Restore and build the temporary WASM-only project in this repo:

```bash
cd src/YTP.Uno
dotnet restore YTP.Uno.UI.wasm.csproj
dotnet build YTP.Uno.UI.wasm.csproj -f net9.0-wasm
```

If you get `NETSDK1139: The target platform identifier wasm was not recognized` even after installing `wasm-tools`:
- Run `dotnet --info` and `dotnet workload list` and confirm `wasm-tools` appears.
- If it does, try restarting your shell/terminal or rebooting (manifest updates sometimes need a new process).
- If it still errors, your SDK installation could be inconsistent (multiple SDK installs or Homebrew vs. Microsoft installer). Try reinstalling the .NET SDK from https://dotnet.microsoft.com.

macCatalyst builds require Xcode and the corresponding .NET macCatalyst workload. If you want to target macCatalyst later, install Xcode and run:

```bash
xcode-select --install
# then
# dotnet workload install microsoft-net-sdk-maccatalyst
```

Progress: I continued porting UI into `src/YTP.Uno/YTP.Uno.UI` (MainPage, QueuePage, SettingsPage, QueueItemControl). The repo currently cannot be validated locally here due to the SDK/workload mismatch; follow steps above to enable local builds.

If you want me to continue porting more pages now, say "port more" — I'll add them without building, or say "wait" and I'll pause until you confirm builds work.
