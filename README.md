# tablet-ab

A standalone desktop application for A/B-testing custom drawing-tablet firmware.

It renders with **Veldrid** (Vulkan /OpenGL / OpenGLES / D3D11) and drives its control
panels with **ImGui.NET**. Input fidelity comes from a dedicated reader thread, not the UI framework.

The reference firmware it drives is
[hs611-min-ab](https://github.com/ig1711/hs611-min-ab); the shared wire protocol
is documented in [`docs/protocol.md`](docs/protocol.md).

---

## Features

- Live vendor-class USB transport (libusb) for debug firmware, plus a
  hardware-free simulated source.
- **Amplitude profile** bars (X and Y) ported from the original web
  `ProfileChart`.
- **Cursor points** view: current point plus a configurable fading trail of
  discrete points (raw units or lazer-mapped pixels).
- Full **A/B control panel**: frequency (discrete + continuous ARR), drive
  periods, ADC, tracking window, settles, pacing, scan order, re-centre.
- Three pointer input modes mirroring lazer: **internal** (OpenTabletDriver
  library), **debug** (vendor frames), and **external** (OS pointer).
- Selectable graphics backend, explicit frame pacing (vsync / unlimited / Nx),
  and headless CLI modes.

## Requirements

| | Linux | Windows |
|---|---|---|
| SDK | .NET 8 SDK | .NET 8 SDK |
| Runtime | .NET 8 runtime (`dotnet-runtime-8.0`) | .NET 8 runtime |
| Native libs | system **libusb** (`libusb-1.0.so.0`), GPU drivers | **libusb-1.0.dll** (x64) beside the exe, WinUSB driver |
| Device access | udev rule (see [`docs/usb-setup.md`](docs/usb-setup.md)) | Zadig → WinUSB binding |

The app is **framework-dependent** by default, so the .NET 8 runtime must be
installed on the target machine. Use a self-contained publish if you want a
runtime-free executable (see below).

## Repository layout

```
tablet-ab/
  src/
    TabletAb.Core/     protocol: frames, commands, stats, device profiles
    TabletAb.Input/    transports + lazer-equivalent input pipeline
    TabletAb.App/      Veldrid window, ImGui panels, visualizers (tablet-ab exe)
  tests/
    TabletAb.Core.Tests/
    TabletAb.Input.Tests/
  docs/
    protocol.md        wire protocol reference
    usb-setup.md       udev / WinUSB / device-id setup
  Directory.Build.props
  tablet-ab.sln
```

## Build

All commands are run from the repository root.

### Linux

```sh
dotnet restore
dotnet build tablet-ab.sln -c Release
```

### Windows

```powershell
dotnet restore
dotnet build tablet-ab.sln -c Release
```

Before building/publishing on Windows, drop the official 64-bit
`libusb-1.0.dll` into `third_party/` at the repo root. `TabletAb.App.csproj`
copies it next to the executable automatically when present. Linux needs
nothing extra (it loads the system `libusb-1.0.so.0`).

### Run from source

```sh
dotnet run --project src/TabletAb.App -c Release
```

### Tests

```sh
dotnet test tablet-ab.sln -c Release
```

## Publish

Produces a self-contained folder you can zip and ship. The example uses
`publish/` as the output (already git-ignored).

### Linux (x64)

```sh
dotnet publish src/TabletAb.App/TabletAb.App.csproj \
  -c Release -r linux-x64 --self-contained false \
  -o publish/linux-x64
```

### Windows (x64)

```powershell
dotnet publish src/TabletAb.App/TabletAb.App.csproj `
  -c Release -r win-x64 --self-contained false `
  -o publish/windows-x64
```

Keep `libusb-1.0.dll` in `third_party/` when you publish for Windows so it
lands in the output folder.

### Self-contained (no .NET install required)

Add `--self-contained true` and, optionally, `-p:PublishSingleFile=true`:

```sh
dotnet publish src/TabletAb.App/TabletAb.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -o publish/linux-x64-standalone
```

Note: Veldrid ships native libraries (`libveldrid-spirv.so` / `.dll`, `SDL2`
etc.), so publish them as separate files or ensure they are extracted — a
single-file publish still expects the native deps beside it unless you extract
them.

## Use the published executable

```sh
# Linux
./publish/linux-x64/tablet-ab
```

```powershell
# Windows
.\publish\windows-x64\tablet-ab.exe
```

Framework-dependent builds need the .NET 8 runtime installed. The app prints the
chosen backend, device name and the list of available backends on startup.

### Headless verification (no window)

```sh
./tablet-ab --list-devices
./tablet-ab --dump-frames 3000 --source vendor
./tablet-ab --dump-frames 1000 --source sim
```

`--list-devices` enumerates `256c:6111` devices; `--dump-frames` streams N
frames and prints live statistics. Useful over SSH/CI. See
[`docs/usb-setup.md`](docs/usb-setup.md) for driver setup.

## Change the graphics backend to OpenGL

The app probes backends in this default order:

- **Windows:** Direct3D 11 → Vulkan → OpenGL
- **Linux:** Vulkan → OpenGL → OpenGLES

### At launch (recommended)

```sh
./tablet-ab --backend opengl
```

`--backend` accepts `vulkan`, `opengl`, `opengles`, `d3d11`, or `metal`
(whichever the machine supports). The default is chosen automatically if the
requested one is unavailable. The startup log reports the backend actually in
use and the full list of supported backends.

### From the UI

The **Status** panel has a backend selector. By default a change only prints a
message telling you which `--backend` flag to restart with, because hot-switching
is unreliable on some compositors (notably Vulkan → OpenGL with Arch's
`sdl2-compat`). Tick **Allow hot switch** in the Status panel to make the
selector recreate the window/device in place — this is experimental.

### Linux note

Veldrid 4.9.0 P/Invokes `libdl`, which glibc ≥ 2.34 no longer ships as a
separate file. [`Program.cs`](src/TabletAb.App/Program.cs) installs a
`DllImportResolver` that redirects `libdl` to `libdl.so.2` before the first
Vulkan probe, so Vulkan/OpenGL detection works on modern distros out of the box.

## CLI options

```
--backend <vulkan|opengl|opengles|d3d11>   preferred graphics backend
--frame-sync <unlimited|vsync|2x|4x|8x>    frame pacing (default: vsync)
--refresh-hz <n>                           display refresh for the Nx modes (default: 60)
--width <n> --height <n>                   window size (default: 1560x1000)
--frames <n>                               run n frames then exit (smoke test)
--source <none|vendor|sim>                 connect a device at startup
--sim-rate <hz>                            simulated source rate (default: 1000)
--dump-frames <n>                          headless: read n frames, print stats, exit
--list-devices                             headless: list 256c:6111 devices, exit
--pointer <off|debug|otd|external>         cursor-point input mode (default: off)
--protocol <auto|v5|v6>                    firmware command protocol (default: auto)
-h, --help                                 show help
```

## Documentation

- [`docs/protocol.md`](docs/protocol.md) — wire protocol reference.
- [`docs/usb-setup.md`](docs/usb-setup.md) — udev rules, WinUSB/Zadig, and
  which device id belongs to which firmware build.
- [`PLAN.md`](PLAN.md) — goals, architecture and osu!lazer reference facts.
- [`PHASES.md`](PHASES.md) — implementation progress.

## License / third-party

- UI font: Noto Sans Regular (SIL Open Font License). See
  [`src/TabletAb.App/Assets/NotoSans-LICENSE.txt`](src/TabletAb.App/Assets/NotoSans-LICENSE.txt).
- Bundles Veldrid, ImGui.NET, LibUsbDotNet and OpenTabletDriver via NuGet; see
  their respective licenses.
