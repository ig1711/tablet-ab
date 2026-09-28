# tablet-ab — standalone tablet A/B testing interface

A standalone desktop application for A/B-testing custom drawing-tablet firmware
under **osu!lazer-like conditions**. It replaces the WebUSB browser tool, which
cannot poll USB faithfully (browser scheduling jitter, no true 1000 Hz report
cadence), and is meant to be reusable by any custom firmware developer — not
only this project's HS611 work.

Status: **scaffolding**. See `PHASES.md` for progress and `SESSION_LOG.md` for
the cross-session development log.

---

## 1. Goals

1. Drive debug A/B firmware live (coil-amplitude frames + runtime knobs) over a
   native, faithful USB path.
2. Reproduce the input conditions osu!lazer runs under, so A/B results transfer
   to real gameplay.
3. Visualize:
   - the raw per-coil **amplitude profile** (bar chart) exactly like the web
     tool's `ProfileChart`;
   - a **cursor points** view: the current point plus a configurable number of
     previous points as a fading trail of discrete points (no interpolated /
     connecting lines).
4. Work with:
   - debug A/B firmware (vendor-class bulk stream, full command set);
   - release firmware that emits OpenTabletDriver-type HID reports (report id 8).

## 2. Must-have controls

| Feature | Notes |
|---|---|
| Frequency selection | 12 vendor indices **plus** continuous raw-ARR (HW-timer backend) |
| Drive periods | burst length + prime / per-read params; custom drive sequences deferred |
| ADC options | samples per channel, ADC clock prescaler, coil recovery |
| Window options | radius, per-axis coil count, re-acquire threshold/period, coarse stride, re-centre strategy + hyst/persist/step |
| Settles | settle sites A/B/C/D |
| Pacing | **SOF-paced** vs **continuous / free-run** (new firmware command) |
| Scan order | ascending, descending, outside-in interleaved, center-out interleaved |

## 3. Visualizations

- **Amplitude profile (bars)** — port of `ProfileChart.tsx` (X and Y axes only;
  heatmap and waterfall are explicitly out of scope for now).
- **Cursor points** — new; discrete points with configurable trail length,
  switchable coordinate space:
  - raw tablet / channel units, and
  - osu!lazer-mapped absolute screen pixels.

## 4. osu!lazer reference facts

Checked against `ppy/osu`, `ppy/osu-framework` and `OpenTabletDriver/OpenTabletDriver`:

- Rendering: **Veldrid** (`ppy.Veldrid`), backend **selectable** —
  D3D11 / Vulkan / Metal / OpenGL / OpenGLES (`osu.Framework/Graphics/Veldrid/VeldridRenderer.cs`).
  There is no single fixed API; "Vulkan or OpenGL" both apply.
- Tablet input: bundled **OpenTabletDriver 0.6.7**
  (`osu.Framework/Input/Handlers/Tablet/OpenTabletDriverHandler.cs`,
  `TabletDriver.cs`). OTD reads on a dedicated `ThreadPriority.AboveNormal`
  thread that blocks on `stream.Read()` and raises one event per report
  (`OpenTabletDriver/InputDeviceEndpoint.cs`).
- lazer uses `AbsoluteTabletMode` (no smoothing) and **drops input while the
  window is inactive** (`OpenTabletDriverHandler.enqueueInput`).
- Tablet modes exposed to the user: **internal** (lazer owns OTD and drives the
  cursor; user configures area / output area / rotation / pressure threshold)
  and **external** (an external driver moves the OS cursor; lazer reads it).

## 5. Architecture

Standalone .NET 8 solution; **Veldrid** for rendering (same GPU abstraction as
lazer, selectable Vulkan/OpenGL/D3D11) and **ImGui.NET** for control panels.
No osu.Framework dependency — input fidelity comes from the input thread, not
the UI framework.

```
tablet-ab/
  PLAN.md
  PHASES.md
  SESSION_LOG.md
  src/
    TabletAb.Core/     # protocol: frames, commands, stats, device profiles
    TabletAb.Input/    # transports + lazer-equivalent input pipeline
    TabletAb.App/      # Veldrid window, ImGui control panels, visualizers
  profiles/            # device profiles (ids, handshake, coordinate mapping)
```

### 5.1 Input modes (mirrors lazer's tablet settings)

- **Internal** — app owns the device and drives a synthetic cursor.
  - Release HID (`256c:006f`) read through the **OTD 0.6.7 library** exactly
    like lazer (`TabletDriver.Create()` → `DeviceReported`), with configurable
    tablet area / output area / rotation / pressure threshold via OTD
    `AbsoluteOutputMode`. OTD 0.6.7 already ships a Huion HS611 configuration
    covering `256c:006F`, and the release firmware presents the matching device
    string (index 201), so **no custom OTD configuration is required**; custom
    configs can still be supplied for other firmware.
  - Vendor debug (`256c:6111`, and the `REL_DEBUG` release-over-vendor build)
    read with a custom **libusb** bulk reader feeding the same point pipeline.
- **External** — app claims nothing; samples the OS pointer like lazer in
  external-driver mode (SDL mouse position at frame rate; optional per-report
  via Raw Input on Windows / evdev on Linux).

### 5.2 lazer conditions to replicate

- Dedicated `AboveNormal` blocking-read thread, one event per report.
- Input dropped while the window is inactive.
- No smoothing / no filters on the absolute position.
- Frame limiter / vsync options matching lazer's, so visual judgement transfers.

### 5.3 Firmware profiles

| build | VID:PID | transport | host sees |
|---|---|---|---|
| `DEBUG_MIN=1` / `DEBUG_DUMP=1` | 256c:6111 | vendor bulk IN/OUT | 168 B v5 frames + 64 B commands (all knobs) |
| `RELEASE=1` | 256c:006f | HID | report id 8, 12 B, OTD `UCLogicTiltReportParser` |
| `RELEASE=1 REL_DEBUG=1` | 256c:6111 | vendor bulk | release acquisition, no commands |
| `REL_DIAG=1` | 256c:006f | HID | report bytes repurposed (peaks / window centre) |

## 6. Firmware changes required

Done in the companion firmware repo (`hs611-fw`):

1. **`SET_PACING`** (new command) — `0` SOF-paced, `1` continuous / free-run.
   Today the only continuous path is `SET_RAMP mode 1`, which conflates pacing
   with ramp mitigation. Decouple.
   - `src/min/debug_proto.h`: add command + document.
   - `src/min/usb_min.c:216-231` (`min_step_due` / `min_free_run`): separate
     `min_pacing` from `min_ramp_mode`.
2. **Continuous frequency** (`SET_FREQ_ARR`, raw timer ARR, clamped ~118–400).
   - `src/min/acq_timed.c:53` (`min_freq_arr`), `:158` (ARR clamp),
     `:568` (`acq_min_set_freq`): add an override.
   - Hardware-timer backend only; the software NOP-sled path stays discrete and
     the UI greys the continuous control out there.
3. **Custom drive sequences** — deferred (not in scope for the first build).

## 7. Coordinate pipeline

- Debug frames carry `xPos` / `yPos` in 1/256 channel units.
- Release HID carries raw tablet units.
- Both map to **lazer-mapped pixels** through the same area math OTD uses
  (`Area` in mm → output area in px, with digitizer max and rotation).
- The cursor screen can switch between raw units and mapped pixels.

## 8. Steps

1. **Scaffold** — solution, Veldrid window, ImGui.NET, frame-limiter/vsync config.
2. **Core protocol** — port `web/src/lib/protocol.ts` (v1–v5 frames, all commands, stats).
3. **Vendor transport (Linux + Windows)** — libusb bulk reader thread + command writer.
   Windows 6111 needs a WinUSB driver binding (Zadig/WCID) — document it.
4. **Firmware: `SET_PACING` + `SET_FREQ_ARR`** (see §6).
5. **A/B control panel** — all must-have controls, live push.
6. **ProfileChart port** — raw-count bars, `LO 300 / HI 3200 / PEAK_MIN 810`,
   gold peak, sample-and-hold, identical layout.
7. **Input pipeline** — internal (OTD library; libusb points for 6111) and
   external (OS pointer) modes; coordinate mapping.
8. **Cursor points screen** — discrete points, configurable trail, zoom/centre,
   jitter readout; both coordinate spaces.
9. **Validation & packaging** — cadence/latency vs `tools/hidmon.py` and
   lazer/OTD; Linux + Windows builds; setup doc (udev/WinUSB/OTD config).

## 9. Risks / open items

- **OTD internal mode + custom firmware**: OTD 0.6.7's built-in HS611 config
  covers `256c:006f` provided the firmware presents the matching device string;
  verify against real release firmware. For other firmware, a custom OTD
  configuration may be needed (no public register-local-directory API in the
  core library).
- **Windows 6111**: WinUSB driver install is a user setup step. If unacceptable,
  add a HID-multipart debug transport instead.
- **External per-report points**: OS sampling is frame-rate limited; true
  per-report external mode needs Raw Input (Windows) / evdev (Linux).
- **Release-build pacing** stays compile-time (`REL_*`); only the debug A/B
  build gets runtime `SET_PACING`.
- **S620 profile**: confirm whether Gaomon S620 needs its own geometry/profile
  or is covered by OTD's existing configs.
- **Keeping `web/` in sync** with new commands (optional).

## 10. References

- osu!lazer: https://github.com/ppy/osu
- osu!framework: https://github.com/ppy/osu-framework
- OpenTabletDriver: https://github.com/OpenTabletDriver/OpenTabletDriver
- Companion firmware + web tool (current): `hs611-fw`
  - `web/src/lib/protocol.ts` — wire protocol to port
  - `web/src/components/ProfileChart.tsx` — amplitude visualizer to port
  - `web/src/components/Position.tsx` — cursor view to replace
  - `docs/tracking-window.md`, `docs/amplitude-physics.md`, `docs/calibration.md`
