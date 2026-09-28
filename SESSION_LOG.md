# tablet-ab — session log

Chronological log of development sessions. One entry per session, newest at the
bottom. Keep entries short and factual: decisions, work done, blockers, and the
exact next step. Link to commits/files where useful.

> Why the plan and phases stay separate: `PLAN.md` is the stable design,
> `PHASES.md` is the live checklist, this file is the history of how we got
> between them and what was learned.

## Entry template

```
## YYYY-MM-DD — <short title>

- Agent: <model / tool>
- Goal:
- Decisions:
- Work done:
- Files touched:
- Blockers:
- Next:
```

---

## 2026-09-27 — Kickoff: scope, decisions, documentation

- Agent: opencode (deepseek/deepseek-v4.1-flash)
- Goal: investigate the existing WebUSB A/B tool and osu!lazer internals, then
  define the standalone app and bootstrap its documentation.

- Investigation (source of the design):
  - Existing web A/B: `hs611-fw/web/` — `usb.ts` (WebUSB bulk), `protocol.ts`
    (v1–v5 frames + commands), `sensor.ts`, panels `ProfileChart`, `Heatmap`,
    `Waterfall`, `Position` (line-segment trail), `Settings`, `Calibration`.
  - Firmware profiles: `DEBUG_MIN`/`DEBUG_DUMP` (256c:6111 vendor bulk, full
    knobs), `RELEASE` (256c:006f HID, report id 8, OTD), `REL_DEBUG`
    (release-over-vendor, no commands), `REL_DIAG` (HID diag bytes).
  - Release is SOF-paced (`hid_sof_take` single latch); only continuous path is
    `SET_RAMP mode 1`, conflated with ramp mitigation
    (`docs/tracking-window.md` §1/§2.2).
  - Frequency is 12 discrete indices (`min_freq_arr`, `acq_excite` switch); the
    HW-timer backend could take a raw ARR.
  - osu!lazer renders via **Veldrid** (selectable D3D11/Vulkan/Metal/OpenGL) and
    reads tablets via bundled **OpenTabletDriver 0.6.7** on an `AboveNormal`
    blocking-read thread; uses `AbsoluteTabletMode`; drops input while the
    window is inactive. Internal vs external tablet modes.

- Decisions:
  - Standalone project (its own repository).
  - Targets **Linux + Windows**.
  - Faithful **input** only; lean Veldrid + ImGui.NET app, no osu.Framework.
  - Release input modes: **internal** (OTD library, lazer-style, user area
    mapping) and **external** (external driver / OS pointer).
  - Firmware changes now: `SET_PACING` (SOF vs continuous) + continuous
    frequency (`SET_FREQ_ARR`, HW backend). Custom drive sequences deferred.
  - Amplitude visualization scope: **ProfileChart (bars) only**.
  - Cursor screen: discrete points with configurable trail, **both** raw units
    and lazer-mapped pixels, switchable.

- Work done:
  - Created the project directory and wrote `PLAN.md`, `PHASES.md`,
    `SESSION_LOG.md`.

- Files touched:
  - `tablet-ab/PLAN.md` (new)
  - `tablet-ab/PHASES.md` (new)
  - `tablet-ab/SESSION_LOG.md` (new)

- Blockers: none.

- Next: **Phase 1 — scaffold** the .NET solution (`TabletAb.Core`,
  `TabletAb.Input`, `TabletAb.App`), Veldrid window, ImGui.NET, frame
  limiter/vsync options.

---

## 2026-09-27 — Phase 1: scaffold (solution, Veldrid, ImGui)

- Agent: opencode (deepseek/deepseek-v4.1-flash) + research subagent
- Goal: stand up the buildable app skeleton with selectable Veldrid backend,
  ImGui UI and lazer-like frame pacing.

- Environment verified (Arch): .NET SDK 8.0.131; Wayland session; Vulkan
  loader + `radeon_icd.json` (AMD RADV); mesa OpenGL; `sdl2-compat` present;
  glslang/spirv-tools present (optional).

- Decisions / package set (researched + verified by a scratch build):
  - `Veldrid` 4.9.0, `Veldrid.SDL2` 4.9.0, `Veldrid.StartupUtilities` 4.9.0,
    `Veldrid.SPIRV` 1.0.15.
  - UI: `Veldrid.ImGui` **5.89.2-ga121087cad** (prerelease-only, frozen at the
    Veldrid 4.9.0 commit) + **`ImGui.NET` pinned to 1.89.2** (newer versions are
    binary-incompatible and crash on the first frame). Controller type is
    `Veldrid.ImGuiRenderer`.
  - `Vk` 1.0.25 pinned only so `Program.cs` can install a `libdl` DllImport
    resolver.
  - Backend selection at startup (Vulkan default on Linux, D3D11 on Windows);
    hot-switch kept experimental/opt-in.

- Gotchas handled:
  - **libdl**: Veldrid 4.9 -> Vk 1.0.25 P/Invokes `libdl`, absent on glibc
    >= 2.34; `GraphicsDevice.IsBackendSupported` caches its result, so the
    resolver is installed at the very top of `Main`.
  - **sdl2-compat (SDL3) on Arch**: disposing an OpenGL device can segfault in
    SDL's Wayland thread, so `DisposeDevice` skips dispose for OpenGL.
  - **Hot backend switching** is unreliable (Vulkan -> OpenGL), so the UI combo
    only switches when an opt-in checkbox is set; otherwise it instructs a
    restart with `--backend`.

- Work done:
  - Solution `tablet-ab.sln` with `TabletAb.Core`, `TabletAb.Input`,
    `TabletAb.App`; `Directory.Build.props`, `.gitignore`.
  - App: `Program.cs`, `AppOptions.cs`, `AbApplication.cs`,
    `Rendering/FrameSyncMode.cs`, `Rendering/FrameLimiter.cs`,
    `UI/StatusPanel.cs`.
  - Core/Input seams: `ProductInfo`, `DeviceKind`, `IDeviceTransport`.
  - `--frames <n>` smoke-test flag.

- Verification:
  - `dotnet build` — 0 warnings, 0 errors.
  - Vulkan and OpenGL both start (AMD RADV RENOIR) and exit 0 with
    `--frames 90 --frame-sync unlimited`.

- Dependencies to install:
  - Arch: **none** (Vulkan + OpenGL + SDL2 already present).
  - Windows: none for D3D11/OpenGL (natives bundled); Vulkan needs the vendor
    runtime (`vulkan-1.dll`), else it falls back to D3D11. Windows 6111 debug
    device will later need a WinUSB binding (Phase 3).

- Files touched: all new under `tablet-ab/` (see `PHASES.md` Phase 1).

- Blockers: none.

- Next: **Phase 2 — core protocol port.** Port `web/src/lib/protocol.ts`
  (v1–v5 `parseFrame`, all command builders, `LiveStats`, `deviceDeltaUs`) into
  `TabletAb.Core`, with unit tests over captured frames.

---

## 2026-09-27 — Phase 2: core protocol port

- Agent: opencode (deepseek/deepseek-v4.1-flash) + 2 verification subagents
- Goal: port the HS611 wire protocol into `TabletAb.Core` and prove it matches
  the web tool.

- Recon: no raw binary frame dumps exist in `hs611-fw/logs` (only decoded
  calibration JSON/CSV), so tests use synthetic frames. Node 26 (native TS type
  stripping) and `web/node_modules` are present, enabling a runtime TS-vs-C#
  cross-check.

- Work done (all in `src/TabletAb.Core/Protocol` unless noted):
  - `ProtocolConstants.cs` — sizes, magic, versions, modes, command ids.
  - `ProtocolCatalog.cs` — all 8 dropdown catalogs.
  - `Frame.cs`, `FrameParser.cs` — v1–v5 decode with correct field gating.
  - `CommandBuilder.cs` — all 22 command builders with matching clamps.
  - `DeviceTime.cs` (`DeltaUs`, wrapping), `MonotonicClock.cs`.
  - `Statistics/LiveStatistics.cs` + `LiveStatsSnapshot.cs` — scalar stats
    ported from `sensor.ts` (per-coil history deferred to the visualizer phase).

- Verification:
  - Subagent 1 built `tests/TabletAb.Core.Tests` (xUnit) and ran it: 46 tests
    pass; no Core bugs found.
  - Subagent 2 independently ran the real `protocol.ts` under Node 26 and diffed
    against the C# parser: **0 differences** across 21 synthetic frames
    (v1–v5 + edge cases: bad magic, unknown/zero version, short, truncated),
    111 command builds (incl. negatives, `int.Min/Max`, null optionals), 8
    timestamp deltas, 46 constants and 8 catalogs.
  - Added `LiveStatisticsTests` (12 tests); `dotnet test` now **58 passed, 0
    failed**.

- Nuances recorded:
  - v1 reads the device timestamp at offset 12, overlapping amplitudes. This
    mirrors `protocol.ts` exactly (inherent to the v1 layout), not a port bug.
  - TS builders accept any JS `number` and coerce with `|0`; the C# API takes
    `int`, so values outside `Int32` cannot be expressed. Identical for every
    `int` value.

- Files touched: new Protocol/ + Statistics/ files in `TabletAb.Core`; new
  `tests/TabletAb.Core.Tests` project added to `tablet-ab.sln`.

- Blockers: none.

- Next: **Phase 3 — vendor USB transport.** libusb bulk reader thread
  (`AboveNormal`) + command writer for `256c:6111` on Linux and Windows;
  document the Windows WinUSB binding.

---

## 2026-09-27 — Phase 3: vendor USB transport

- Agent: opencode (deepseek/deepseek-v4.1-flash) + 2 subagents (libusb
  research/verification; tests + transport review)
- Goal: native bulk transport for the vendor-class debug firmware.

- Recon: no `256c:6111` attached (only mouse/fingerprint/bt/camera present), so
  the live device path could not be exercised. A `256c:6111` udev rule already
  exists on this machine (`72-hs611-webusb.rules`).

- Binding decision (verified by a subagent): **`LibUsbDotNet` 3.0.224**
  (namespace `LibUsbDotNet.LibUsb`). It loads the native lib here (real libusb
  errors returned during enumeration), resolves `libusb-1.0.so.0` on Linux and
  expects a shipped `libusb-1.0.dll` on Windows. No `libdl` resolver needed.

- Work done (`src/TabletAb.Input`):
  - `IFrameSource`/`ICommandSink`, `FrameReceivedEventArgs`,
    `TransportLostEventArgs`.
  - `VendorUsbTransport` — find/open/claim, bulk IN reader on an `AboveNormal`
    thread, bulk OUT command writer, counters, `Lost` event.
  - `VendorUsbLocator`, `VendorUsbOptions`.
  - `SimulatedFrameTransport` — hardware-free frame generator (moving Gaussian
    bump) for development and tests.
- App wiring: `--source none|vendor|sim`, `--sim-rate`, `--list-devices`,
  `--dump-frames N`; connect/disconnect buttons and a live stats readout in the
  status panel; startup connect via `--source`.
- Docs/asset: `docs/usb-setup.md`; conditional `libusb-1.0.dll` copy in
  `TabletAb.App.csproj` for Windows.

- Verification:
  - `--list-devices` → libusb enumerated, reported "no 256c:6111 device"
    (exit 1) with no load error.
  - `--dump-frames 2000 --source sim` → exactly 1000.0 Hz, 0 errors, exit 0.
  - `--dump-frames 10 --source vendor` → clean "device not found" (exit 1).
  - UI smoke (`--source sim --frames 150`, and no-source `--frames 60`) → exit 0.
  - Subagent added `tests/TabletAb.Input.Tests` (3 tests, pass) and reviewed the
    transport: found no API misuse but several race/robustness issues, which I
    fixed (Close/Open + Close/Send races, zero-length read fatality, timeouts as
    errors, throwing subscriber). Full suite: **61 tests pass**; solution builds
    with 0 warnings.

- Blockers: none in code. **Live hardware verification pending** — needs the
  debug firmware flashed and the tablet plugged in.

- Next: **Phase 4 — firmware `SET_PACING` + `SET_FREQ_ARR`** in `hs611-fw`
  (`src/min/debug_proto.h`, `src/min/usb_min.c`, `src/min/acq_timed.c`), then
  surface them in the app.

---

## 2026-09-27 — Phase 4: firmware SET_PACING + SET_FREQ_ARR

- Agent: opencode (deepseek/deepseek-v4.1-flash) + 2 subagents (Core tests;
  firmware verification)

- Work done in the `hs611-fw` firmware repo:
  - `src/min/debug_proto.h`: `DBG_CMD_SET_FREQ_ARR 0x24`,
    `DBG_CMD_SET_PACING 0x25`, `DBG_PACING_SOF/CONTINUOUS/AUTO`, doc block.
  - `src/min/usb_min.c`: `min_pacing` (default `MIN_PACING =
    DBG_PACING_AUTO`) and `min_pacing_apply()`; `min_ramp_apply()` now delegates
    to it; new `SET_FREQ_ARR`/`SET_PACING` command cases.
  - `src/min/acq_timed.{c,h}`: `g_freq_arr_override` +
    `acq_min_set_freq_arr`/`acq_min_get_freq_arr`; `min_carrier_config` uses the
    override else the index table (clamp 100..400); `SET_FREQ` clears the
    override (last one wins).
  - Docs: `docs/tracking-window.md` §2.2 (pacing) and new §2.4 (continuous
    frequency).
- Work done in this repo (tablet-ab):
  - `ProtocolCommands.SetFrequencyArr/SetPacing`, `ProtocolConstants.Pacing*`,
    `CommandBuilder.SetFrequencyArr/SetPacing`, `ProtocolCatalog.PacingModes`.

- Verification:
  - All three firmware variants build with `arm-none-eabi-gcc` 16.2.0, 0 errors
    / 0 warnings: DEBUG_MIN (61.09% flash), REL_DEBUG (44.83%), RELEASE
    (41.33%).
  - Subagent review: pacing truth table correct; explicit pacing independent of
    `SET_RAMP`; `min_free_run` used consistently; last-write-wins for
    frequency; ids `0x24`/`0x25` collision-free; REL_DEBUG still compiles. No
    functional bugs.
  - Subagent tests: +4 tests → **65 total** (62 Core + 3 Input), all pass.

- Design notes recorded by the verification:
  - `auto` (default) preserves legacy carry-over-only free-run.
  - `SET_FREQ_ARR` is hardware-backend only and is not reflected in the frame's
    `freq` metadata; software backend ignores it.
  - `release_compat.c` has no `acq_min_set_freq_arr` stub (REL_DEBUG compiles
    the call sites out); a future REL_DEBUG reference would need one.

- Blockers: none. On-device pacing/ARR verification still pending hardware.

- Next: **Phase 5 — A/B control panel.** Surface frequency (+continuous), drive
  periods, ADC, window, settles, pacing, scan order and estimator controls in
  the ImGui panel, pushing via `ICommandSink`.

---

## 2026-09-27 — Phase 5: A/B control panel

- Agent: opencode (deepseek/deepseek-v4.1-flash) + 2 subagents (settings tests;
  panel review)

- Work done:
  - `src/TabletAb.Input/AcquisitionSettings.cs` — the full acquisition model
    (web `state.ts` defaults) with `PushAll(ICommandSink)` mirroring the web
    attach sequence. Placed in Input so it is testable without Veldrid.
  - `src/TabletAb.App/UI/AcquisitionPanel.cs` — an ImGui window with collapsible
    sections (Frequency & pacing, Drive, ADC, Window & re-acquire, Tracking,
    Estimator, Settles & diagnostics). Every widget pushes its command
    immediately via an `Action<byte[]>`; "Push all settings" re-sends the set.
  - `AbApplication` wiring: owns the settings + panel, `SendCommand` no-ops when
    disconnected, `PushAllSettings()` on connect.
  - `StatusPanel` placeholder replaced with a pointer to the A/B window.

- Verification:
  - Build 0 warnings / 0 errors; UI smoke with the simulated source exits 0.
  - Subagent added 7 `PushAll` tests (recording sink) → **72 tests** total
    (62 Core + 10 Input), all pass.
  - Subagent review mapped all required controls to the right builders/catalogs
    and found three defects, now fixed:
    - F1 continuous-frequency toggle was enabled on the software backend and did
      not restore the index table when switching to software;
    - F2 window coil counts used a raw integer field instead of the
      `WindowCoils` catalog dropdown;
    - F3 `CommandBuilder.SetFrequencyArr` did not clamp to 100..400 on the host.

- Design notes:
  - Pacing is surfaced independently of ramp mitigation (phase 4 command).
  - Web-only host statistics ('measure base/jitter', telemetry grid) remain in
    the status panel, not the A/B panel.
  - `Freeze display` is stored in the settings; it will gate the visualizers in
    Phase 6/8.

- Blockers: none. On-device control round-trip pending hardware.

- Next: **Phase 6 — ProfileChart port.** X/Y raw-count bars matching
  `ProfileChart.tsx` (LO 300 / HI 3200 / PEAK_MIN 810, gold peak, sample-and-hold),
  fed from the live frame stream.

---

## 2026-09-27 — Phase 6: ProfileChart port

- Agent: opencode (deepseek/deepseek-v4.1-flash) + 2 subagents (layout tests;
  renderer review)

- Design: split the port into pure math (testable, no graphics) and drawing.
  - `src/TabletAb.Core/Visualization/ProfileChartLayout.cs` — constants, plot
    geometry, `Fraction`, `ResolvePeakIndex`, `GetBar`, label thresholds.
  - `src/TabletAb.App/UI/ProfileChartPanel.cs` — X and Y charts drawn with the
    ImGui draw list (gradient bars via `AddRectFilledMultiColor`, dashed top
    reference, gold device peak, index/value/scale labels).
  - `AbApplication` draws both charts from `_statistics.Latest`; `Freeze display`
    now holds the last shown frame without stopping the stream.

- Verification:
  - 12 new layout tests → **84 tests** total (74 Core + 10 Input), all pass;
    build 0 warnings.
  - Subagent renderer review found one real bug — the Y-axis reference/baseline
    used the bar group span instead of the full plot width (visible ~7-slot
    offset and overflow on Y) — now fixed to `PadLeft .. PadLeft+PlotWidth`.
    Also aligned fonts to the web (index 11, values 10, scale 11 middle-anchored,
    peak 12) and centred the "waiting" text.
  - Geometry, scaling, peak selection (reported channel, not argmax), colours,
    gradient direction, ImU32 packing and sample-and-hold all verified matching.

- Blockers: none.

- Next: **Phase 7 — input pipeline.** Internal mode (OTD library for release HID
  + custom OTD config; libusb points for debug) and external mode (OS pointer),
  plus the raw-units ↔ lazer-mapped-pixels coordinate mapping.

---

## 2026-09-27 — Phase 7: input pipeline (internal / external)

- Agent: opencode (deepseek/deepseek-v4.1-flash) + 3 subagents (OTD research;
  external-pointer research; tests + review)

- Work done:
  - Core: `Input/PointerPoint.cs`, `Input/TabletArea.cs`, and
    `Input/TabletAreaTransform.cs` — a port of OTD 0.6.7's `AbsoluteOutputMode`
    raw→mm→output-pixel transform (matrix order, rotation sign, clamping,
    normalised output).
  - `Input/IPointerSource.cs`; `DebugFramePointerSource` (vendor frames →
    normalised points, same gate as `Position.tsx`); `ExternalPointerSource`
    (push-based OS pointer); `OtdPointerSource` (OTD library for release HID,
    off-thread Detect, report subscription, area transform).
  - App: `--pointer off|debug|otd|external`, mode selector + point readout in
    the status panel, external capture from `Sdl2Window.MouseMove`, lifecycle
    (detach/re-attach across mode changes and connect/disconnect).

- Verification:
  - 27 new tests → **100 total** (80 Core + 20 Input), all pass; build 0
    warnings.
  - All three pointer modes start and shut down cleanly (exit 0); OTD `Detect()`
    runs without a device.
  - Review found the key issue: OTD 0.6.7 **does** ship a Huion HS611 config for
    `256C:006F` (the earlier research mis-read decimal vs hex), and 11 configs
    share that id (disambiguated by device string). My injected custom config
    would have double-matched and opened the device twice. Fixed by dropping the
    custom provider and using OTD's built-in set (exactly what lazer does).
  - Also fixed from review: OTD Open/Close race (Close now waits for the detect
    task before disposing), a ServiceProvider leak on partial open, hotplug
    re-hooking via `TabletsChanged`, and dead fallback logic.

- Blockers / caveats:
  - Hardware validation of the OTD release-HID path is pending (no tablet
    attached). The release firmware must present the HS611 device string
    (index 201) for the built-in config to match.
  - External mode is focus-limited on native Wayland; X11/XWayland gives the
    global capture (documented).
  - OTD mode maps to a fixed 1920x1080 output area; making it configurable and
    window-derived is deferred.

- Next: **Phase 8 — cursor points screen.** Discrete points + a configurable
  fading trail (no connecting lines) fed from the active `IPointerSource`, with
  the raw ↔ lazer-mapped coordinate switch.

---

## 2026-09-27 — Phase 8: cursor points screen

- Agent: opencode (deepseek/deepseek-v4.1-flash) + 2 subagents (trail tests;
  panel review)

- Work done:
  - `src/TabletAb.Core/Input/PointerTrail.cs` — thread-safe ring buffer with a
    configurable display count (newest N, oldest-first snapshot).
  - `src/TabletAb.App/UI/CursorPointsPanel.cs` — the cursor screen: discrete
    per-point circles with age-based alpha, newest highlighted, area frame,
    grid, centre crosshair, zoom, centre-on-latest, clear, and a
    normalized/raw/mapped coordinate switch.
  - `OtdPointerSource` exposes `RawMax`/`MappedMax`; `AbApplication` wires the
    trail, per-mode bounds, and the panel.

- Verification:
  - 7 new `PointerTrail` tests → **107 total** (87 Core + 20 Input), all pass;
    build 0 warnings.
  - UI smoke (simulated source + debug pointer) runs and exits 0.
  - Independent review **confirmed the core requirement**: the trail is drawn
    only as per-point circles, with no connecting/interpolated segments. It
    found two real bugs, both fixed:
    - stale trail on pen-lift/disconnect (trail now cleared on detach and after
      a 300 ms gap with no points);
    - jitter stats coupled to trail length (now a fixed 240-point window).
  - Also fixed the default layout so all panels fit (window 1560x1000; cursor
    panel below the charts).

- Notes deemed cosmetic and left: the raw-vs-normalized one-channel offset for
  debug points, the area border/grid anchored to the full area at zoom > 1, and
  an axis-agnostic jitter badge threshold.

- Next: **Phase 9 — validation & packaging.** Compare against the web A/B and
  OTD/lazer, publish Linux + Windows builds, and finalise the setup docs.

---

## 2026-09-27 — UI rework (fixed tiles) + pen-away freeze investigation

- Agent: opencode (deepseek/deepseek-v4.1-flash) + 1 review subagent
- Trigger: manual testing feedback.

- UI rework:
  - New `UI/TileHost.cs`: fixed tiles instead of floating windows, with a
    per-tile **Fullscreen / Tile** toggle in the header.
  - Layout (`AbApplication.DrawLayout`): status 36%×15%, A/B controls 36%×85%
    below it, right 64% column split into profiles 40% (X and Y combined into one
    tile, two equal children) and cursor 60%.
  - UI scaled 1.5x (`ScaleAllSizes(1.5)` once + `FontGlobalScale = 1.5` each
    frame). Style is re-applied after an experimental backend hot-switch.
  - Panels converted to content-only drawers; profile charts and the cursor plot
    now render inside clipped child regions, which **fixes the zoom bug where the
    cursor plot overlaid its own controls**.

- Review: layout matches the requested fractions with no gaps/overlaps; Begin/End
  and BeginChild/EndChild are balanced; the only real defect (style lost after a
  backend hot-switch) is fixed, plus a stable ImGui id for the fullscreen button.

- Investigation — "pen away for too long, app freezes; Push all settings
  unfreezes":
  - Firmware `debug_min_step` (src/min/usb_min.c:988-1011): while cold, between
    re-acquire attempts (gated by `SET_REACQ_PERIOD`, the app pushes 200 ms) the
    device does **not** scan; it rebuilds and re-sends the **last** profile every
    SOF. So the host keeps receiving frames but they carry stale amplitudes and
    the stale peaks/position, and `flags` bit0 (pen) stays set.
  - Effect in the app: the charts and the cursor point look frozen because the
    same frame is re-sent; nothing new is measured until the next re-acquire or
    a settings push (which resets `min_reacq_ts_valid` and forces a scan).
  - Also a host-side amplifier: `DebugFramePointerSource` validates a point from
    amplitude+peaks, so a stale high-amplitude frame keeps the trail alive with
    duplicate points.
  - Options discussed (no change made yet): keep it; set the re-acquire period to
    0 so the device rescans every frame; make the host treat the stale stream as
    pen-away; or have the firmware emit an explicit pen-away frame when it goes
    cold.

- Verification: build 0 warnings; UI smoke (sim + debug pointer) exits 0; 107
  tests pass. Republished `publish/linux-x64/`.

- Next: decide on the pen-away behaviour, then **Phase 9 — validation &
  packaging**.

---

## 2026-09-27 — Fix: pen-away re-acquire freeze (DWT wrap)

- Agent: opencode (deepseek/deepseek-v4.1-flash)
- Symptom: after the pen is away "for too long" the app looks frozen; bringing
  the pen back does not resume scanning; pushing settings unfreezes it.

- Root cause (firmware, not the re-acquire period): the cold re-acquire gate
  stored a deadline in **microseconds** via `now = DWT->CYCCNT / 72U` and
  compared with the signed-difference idiom `(int32_t)(now - deadline) < 0`.
  `DWT->CYCCNT/72` wraps every ~59.7 s (not 2^32 µs), so the idiom is invalid
  across that wrap: a deadline set just before the wrap reads ~60 s in the
  future after it, and the device stays in the idle "re-send the last frame"
  path for up to a minute. Any settings push clears `min_reacq_ts_valid` and
  forces an immediate scan, which is why Push unfreezes it.

- Fix: gate on **raw DWT cycles** (a genuine 2^32 wrapping counter), which the
  signed-difference idiom handles correctly for intervals < 2^31 cycles
  (~29.8 s):
  - `src/min/usb_min.c`: `min_next_reacq_us` -> `min_next_reacq_cyc`;
    `now = CYCCNT/72` removed; deadline `= CYCCNT + ms*72000`; comparison on
    `DWT->CYCCNT`; `SET_REACQ_PERIOD` clamped to 29000 ms.
  - `src/release/release_pen.c`: same latent fix (`rel_next_reacq_us` ->
    `rel_next_reacq_cyc`); only reachable with `REL_REACQ_PERIOD_MS > 0`.

- Verification: DEBUG_MIN, RELEASE and RELEASE+REL_DEBUG all build with 0
  errors / 0 warnings; no stale `*_reacq_us` references remain; remaining
  `CYCCNT/72` uses are only the display timestamps.

- Note: the re-acquire period default was reverted back to 200 ms (the period
  was not the cause).

- To test: reflash `make DEBUG_MIN=1 flash`, then leave the pen away for >60 s
  and bring it back — scanning should resume immediately instead of waiting for
  a settings push.

---

## 2026-09-27 — UI tweaks: drive+settles, cursor visualizer, profile fonts

- Agent: opencode (deepseek/deepseek-v4.1-flash)
- Trigger: manual testing feedback.

- Work done:
  - `UI/AcquisitionPanel.cs`: merged the old "Settles & diagnostics" content into
    the Drive section, renamed the header to **"Drive and settles"**, removed the
    separate section.
  - `UI/CursorPointsPanel.cs`:
    - plot now fills the whole window (independent X/Y scales, no letterboxing);
    - every 5th trail point draws a yellow crosshair instead of a dot;
    - "Centre on latest" is now a **button** that sets a fixed `_viewCentre`; the
      view no longer follows the cursor (reset on coordinate-mode change);
    - artifact fix at high zoom / large trail: clip all plot drawing to the
      child rect and bound the area fill, grid and dashed crosshair to the
      visible span (previously the crosshair spanned the full zoomed area,
      generating huge off-screen geometry).
  - `UI/ProfileChartPanel.cs`: increased index/value/peak/scale number font
    sizes (11/10/12/11 -> 15/14/16/15).

- Verification: build 0 warnings / 0 errors; full suite **107 tests pass**; UI
  smoke (`--source sim --pointer debug --frames 120`) exits 0.

- Next: decide on the pen-away behaviour, then **Phase 9 — validation &
  packaging**.

