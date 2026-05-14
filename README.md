# Monitor Brightness Control

A Windows app for adjusting brightness independently for each attached monitor using DDC/CI.

## Versions

- `MonitorBrightness\` - WinUI 3 / .NET 10 app.
- `pythonVersion\` - original Python/tkinter version kept for reference.

## License

This project is licensed under the MIT License. See `LICENSE`.

## Prerequisites

### Running the published app

- Windows 10/11.
- Windows App Runtime 2.x installed. The app uses Windows App SDK `2.0.1`.
- External monitors must support DDC/CI brightness control for the sliders/CLI brightness commands to work.

The published app is self-contained and Native AOT compiled, so it does not require installing the .NET runtime separately.

### Building or running from source

- .NET 10 SDK.
- Windows SDK `10.0.26100.0` or newer.
- Windows App SDK build support for WinUI 3 projects.

The project targets:

```text
net10.0-windows10.0.26100.0
Microsoft.WindowsAppSDK 2.0.1
```

### Publishing with Native AOT

- .NET 10 SDK with Native AOT toolchain support.
- Windows C++ build tools required by Native AOT publishing.
- A supported runtime identifier, for example `win-x64`.

## WinUI 3 app features

- Per-monitor brightness sliders.
- Monitor names resolved from EDID/Windows display data when available.
- Monitor resolution and display index shown in each tile.
- Compact monitor tiles with automatic window sizing.
- Configurable threshold for how many monitor tiles are shown before the list scrolls. The default is `4`.
- Identify button that shows a numbered overlay on each monitor.
- Settings panel opened from the gear button.
- Configurable global hotkey to bring the app to the foreground.
- Keyboard-first brightness control for power users.
- System tray support:
  - Closing the window minimizes to the tray.
  - Single-click or double-click the tray icon restores the window.
  - The first close shows a one-time explanation and gives an option to close the app entirely.
- First-launch prompt explaining that a global hotkey can be configured.
- Settings are stored next to the executable in `monitor_brightness_settings.json`.
- Diagnostic warnings are logged to `%LOCALAPPDATA%\MonitorBrightness\monitor_brightness.log`.
- Custom monitor/sun app icon in the title bar and system tray.

## Keyboard controls

The main window can be controlled without clicking the sliders:

- `1`-`9` - select monitor 1 through 9.
- `0` or `A` - select all brightness-capable monitors.
- `Up`, `Right`, or `+` - increase the selected monitor brightness.
- `Down`, `Left`, or `-` - decrease the selected monitor brightness.
- `Ctrl` + adjustment key - fine adjustment by `1%`.
- adjustment key alone - normal adjustment by `5%`.
- `Shift` + adjustment key - coarse adjustment by `10%`.
- `Ctrl+Shift` + adjustment key - large adjustment by `25%`.
- `PageUp` / `PageDown` - coarse `10%` up/down.
- `Home` - set selected target to minimum brightness.
- `End` - set selected target to maximum brightness.

The selected monitor tile is highlighted. When all monitors are selected, all brightness-capable tiles are highlighted and adjustments apply to all of them at the same time.

## Command-line usage

Run from the WinUI project folder:

```powershell
cd MonitorBrightness
dotnet run -c Release -- --help
```

Published executable examples:

```powershell
MonitorBrightness.exe --list
MonitorBrightness.exe --get 2
MonitorBrightness.exe --set 1 75
MonitorBrightness.exe --set all 50
MonitorBrightness.exe --set 1,3 75
MonitorBrightness.exe --set 1-4 60
MonitorBrightness.exe --set 1 3 4 80
MonitorBrightness.exe --identify
```

Commands:

- `--list`, `-l` - list monitors and current brightness.
- `--get [n]`, `-g [n]` - get brightness for monitor `n`, or all monitors if omitted.
- `--set <targets> <value>`, `-s <targets> <value>` - set brightness to `0-100`.
  - Targets can be a single monitor (`1`), all monitors (`all`), comma-separated (`1,3`), a range (`1-4`), or space-separated (`1 3 4`).
- `--identify`, `--id` - print monitor identification information.
- `--help`, `/help`, `-h`, `-?`, `/?` - show CLI help. If any help flag is present, all other CLI flags are ignored.

## Build

```powershell
dotnet build MonitorBrightnessControl.slnx -c Debug
dotnet build MonitorBrightnessControl.slnx -c Release
```

## Test

```powershell
dotnet test MonitorBrightnessControl.slnx -c Debug
dotnet test MonitorBrightnessControl.slnx -c Release
```

## Publish with Native AOT

Publishing the app performs Native AOT compilation. To compile with AOT, run:

```powershell
cd MonitorBrightness
dotnet publish -c Release -r win-x64
```

The published executable is written to:

```text
MonitorBrightness\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\MonitorBrightness.exe
```

## Notes

Brightness control requires DDC/CI support from the monitor and connection path. Most external monitors connected via DisplayPort or HDMI support this, but some displays, adapters, docks, or laptop built-in panels may not.
