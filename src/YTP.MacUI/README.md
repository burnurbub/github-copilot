YTP.MacUI - Avalonia macOS-like UI (scaffold)

This project is a macOS-styled UI scaffold using Avalonia. It mirrors the Windows WPF UI roughly, with a similar MainWindow and controls.

How to run (Windows or mac):

- Ensure `dotnet` SDK is installed (recommended 8.0+ / 9.0 if available)
- From repo root run:

```powershell
cd src\YTP.MacUI
dotnet build
dotnet run --project .
```

Notes:
- This is a scaffold only. Core services and wiring to `YTP.Core` still need to be implemented.
- For native mac feel, we use Avalonia's default styling and will adjust fonts, toolbar, and menus to match macOS conventions next.
