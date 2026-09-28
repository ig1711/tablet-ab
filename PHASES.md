# tablet-ab — phases

Progress tracker. Update this file whenever a phase or task changes state.
Detailed reasoning lives in `PLAN.md`; chronological notes live in
`SESSION_LOG.md`.

Legend: `[ ]` todo · `[~]` in progress · `[x]` done · `[-]` cancelled/deferred.

## Status

| Phase | Name | Status |
|---|---|---|
| 0 | Decisions & documentation | `[x]` |
| 1 | Scaffold (solution, Veldrid, ImGui) | `[x]` |
| 2 | Core protocol port | `[x]` |
| 3 | Vendor USB transport (Linux + Windows) | `[x]` |
| 4 | Firmware: `SET_PACING` + `SET_FREQ_ARR` | `[x]` |
| 5 | A/B control panel | `[x]` |
| 6 | ProfileChart port | `[x]` |
| 7 | Input pipeline (internal / external) | `[x]` |
| 8 | Cursor points screen | `[x]` |
| 9 | Validation & packaging | `[ ]` |
| 10 | Protocol v6 + `hs611-min-ab` firmware | `[x]` |

**Next up:** Phase 9 — validation & packaging.

---

## Phase 10 — Protocol v6 + `hs611-min-ab` firmware `[x]`

- [x] New isolated firmware at
      [hs611-min-ab](https://github.com/ig1711/hs611-min-ab) (protocol v6 in
      `src/protocol.h`; docs in its `README.md`).
- [x] `TabletAb.Core.Protocol` updated to v6: explicit header fields, a u16
      `flags` word, consolidated commands (`SET_ADC`, `SET_WINDOW`, `SET_REACQ`,
      `SET_BACKEND`), `ProtocolConstants.Current = V6`.
- [x] Legacy v1–v5 frames still decode (read-only) for old captures/firmware.
- [x] `AcquisitionSettings.PushAll`, `SimulatedFrameTransport`, UI and tests
      migrated to v6.
- [x] Canonical reference: [`docs/protocol.md`](docs/protocol.md).
- [x] Protocol v6.1: `SET_SETTLE_CYC` (0x15) — per-site settle in DWT cycles
      (72 = 1 µs) for sub-microsecond AFE tuning; tablet-ab exposes a
      "Cycle-accurate settle" toggle (v5 falls back to whole-µs `SET_SETTLE`).

---

## Phase 0 — Decisions & documentation `[x]`

- [x] Choose project location: standalone repository, separate from the firmware.
- [x] Target platforms: **Linux + Windows**.
- [x] Fidelity: faithful **input** only; lean Veldrid app (no osu.Framework).
- [x] Release input: **internal** (OTD library, lazer-style, user area mapping)
  and **external** (external driver / OS pointer) modes.
- [x] Firmware changes allowed: `SET_PACING` + continuous frequency now; custom
  drive sequences deferred.
- [x] Amplitude visualization scope: **ProfileChart (bars) only**.
- [x] Cursor coordinates: **both** raw units and lazer-mapped pixels, switchable.
- [x] Write `PLAN.md`, `PHASES.md`, `SESSION_LOG.md`.

## Phase 1 — Scaffold `[x]`

- [x] Create solution `tablet-ab.sln` with `TabletAb.Core`, `TabletAb.Input`,
  `TabletAb.App`.
- [x] Veldrid window (Vulkan + OpenGL backends) with a render loop.
- [x] ImGui.NET integration for control panels.
- [x] Frame limiter / vsync options mirroring lazer (`FrameSyncMode`, `FrameLimiter`).
- [x] Build + run on Arch (Vulkan and OpenGL, clean startup and shutdown).

**Exit:** app builds and opens on Linux; backend selectable.

Notes:
- Backend hot-switch is **experimental and opt-in** (unreliable on some
  compositors); default is restart with `--backend <name>`. See `PLAN.md` §9.
- `--frames <n>` added for automated smoke tests.

## Phase 2 — Core protocol port `[x]`

- [x] Port `parseFrame` (v1–v5) and `Frame` (`Protocol/FrameParser.cs`, `Frame.cs`).
- [x] Port all `buildSet*Command` builders and constants (`CommandBuilder.cs`,
  `ProtocolConstants.cs`, `ProtocolCommands`, `ProtocolVersion`, `ProtocolCatalog`).
- [x] Port `LiveStats` accumulation / `deviceDeltaUs` (`Statistics/LiveStatistics.cs`,
  `LiveStatsSnapshot.cs`, `Protocol/DeviceTime.cs`).
- [x] Unit tests for frame parsing across all protocol versions (and commands,
  deltas and statistics).

**Exit:** byte-exact parse vs the web tool. **Verified** by an independent
runtime cross-check that ran the real `web/src/lib/protocol.ts` under Node 26
and diffed it against the C# parser: **0 differences** over 21 synthetic frames
(v1–v5, edge cases), 111 command builds, 8 timestamp deltas, 46 constants and 8
catalogs. 58 xUnit tests pass.

Notes:
- `LiveStatistics` ports the scalar stats only; the per-coil history ring buffer
  and coil statistics from `sensor.ts` stay with the amplitude visualizer
  (Phase 6).
- For v1 frames the parser reads the device timestamp at offset 12, which
  overlaps the first amplitudes — this matches the TypeScript source exactly and
  is inherent to the v1 layout, not a port bug.

## Phase 3 — Vendor USB transport (Linux + Windows) `[x]`

- [x] libusb backend (`VendorUsbTransport`): claim vendor interface, bulk IN
  reader thread (`AboveNormal`), bulk OUT command writer.
- [x] Device discovery for `256c:6111` / `REL_DEBUG` (`VendorUsbLocator`).
- [x] Disconnect / reconnect handling, error surfacing (`Lost` event, CLI exit
  codes, UI status).
- [x] Windows: document the WinUSB (Zadig) binding and the `libusb-1.0.dll`
  requirement (`docs/usb-setup.md`, conditional copy in `TabletAb.App.csproj`).
- [x] `SimulatedFrameTransport` and headless CLI (`--list-devices`,
  `--dump-frames`) for hardware-free development and verification.

**Exit:** live 168 B frames at the true rate on both OSes.
**Status:** implementation complete and unit-tested (3 tests) + simulated
stream verified at 1000 Hz; `--list-devices` proves libusb loads/enumerates.
**Live hardware read (real vendor device) is still pending** — no `256c:6111`
was attached during development. Plug in the debug firmware and run
`--dump-frames 3000 --source vendor` to confirm.

Notes:
- `LibUsbDotNet` **3.0.224** (namespace `LibUsbDotNet.LibUsb`) is the binding;
  Linux uses the system `libusb-1.0.so.0`, Windows needs `libusb-1.0.dll`.
- Robustness fixes applied after review: `Close()` now holds the open gate for
  the whole teardown (no Open/Close race) and `Cleanup` holds the write gate (no
  Close/Send use-after-free); a successful zero-length read is no longer fatal;
  read timeouts are not counted as errors; a throwing `FrameReceived` subscriber
  no longer kills the reader.

## Phase 4 — Firmware: `SET_PACING` + `SET_FREQ_ARR` `[x]`

In the `hs611-fw` repo:

- [x] `src/min/debug_proto.h`: added `SET_PACING` (0x25) and `SET_FREQ_ARR`
  (0x24) plus the `DBG_PACING_*` constants and docs.
- [x] `src/min/usb_min.c`: `min_pacing` separated from `min_ramp_mode` via
  `min_pacing_apply()`; `auto` preserves the legacy carry-over behaviour.
- [x] `src/min/acq_timed.c/.h`: raw-ARR override (`g_freq_arr_override`,
  `acq_min_set_freq_arr`/`acq_min_get_freq_arr`), clamp 100..400, cleared by
  `SET_FREQ` (last one wins).
- [x] Software-backend continuous frequency: unsupported (hardware-timer
  backend only); documented.
- [x] App-side protocol: `CommandBuilder.SetFrequencyArr`/`SetPacing`,
  `ProtocolCommands.Set*`, `ProtocolConstants.Pacing*`,
  `ProtocolCatalog.PacingModes` + tests. (UI controls are Phase 5.)

**Exit:** on-device toggle of pacing and ARR sweep changes cadence/frequency.
**Status:** implementation complete; all three firmware variants build with 0
errors / 0 warnings. Independent review found no functional bugs. On-device
verification is pending hardware (same caveat as Phase 3).

Notes:
- Pacing modes: `0` SOF, `1` continuous, `2` auto (legacy: free-run only while
  the carry-over ramp mode is set). Explicit pacing is independent of `SET_RAMP`.
- `SET_FREQ_ARR` is hardware-backend only and its clamp band is 100..400 ARR.
- The test suite is now **65 tests** (62 Core + 3 Input).

## Phase 5 — A/B control panel `[x]`

- [x] Frequency: 12 indices + continuous ARR slider (hardware backend only).
- [x] Drive periods: burst (6–29) + prime periods/repeats + per-read steady
  (via ramp mode).
- [x] ADC: samples (1–7), clock prescaler, coil recovery.
- [x] Window: radius, per-axis N (catalog), re-acquire threshold/period, coarse
  stride.
- [x] Settles A/B/C/D.
- [x] Pacing: SOF / continuous / auto.
- [x] Scan order: asc / desc / outside-in / center-out.
- [x] Re-centre mode + hyst/persist/step/deadband; estimator + centroid base +
  log-Gaussian coefficients; warm-up; flat-hold; repeat-coil.
- [x] Live push on change; connect-time `PushAll`; frame metadata shown in the
  status panel.

**Exit:** every control round-trips and matches the web tool's effect.
**Status:** implemented (`AcquisitionSettings` in `TabletAb.Input`,
`AcquisitionPanel` in `TabletAb.App`); 7 new `PushAll` tests. On-device
round-trip pending hardware.

Notes:
- `AcquisitionSettings` lives in `TabletAb.Input` (not the App) so it is
  testable without Veldrid.
- Independent review found and I fixed three defects: the continuous-frequency
  toggle is now disabled on the software backend (and switching to software
  restores the index table); window coil counts use the `WindowCoils` catalog
  dropdown; `SetFrequencyArr` now clamps to 100..400 on the host too.
- Web-only host statistics ('measure base/jitter', the telemetry grid) live in
  the status panel instead of the A/B panel.

## Phase 6 — ProfileChart port `[x]`

- [x] Pure-math layout `TabletAb.Core/Visualization/ProfileChartLayout.cs`:
  `LO 300 / HI 3200 / PEAK_MIN 810`, pad 62/18/26/34, shared slot count,
  fraction clamp, peak resolution, `ShowIndex`/`ShowValue` thresholds.
- [x] `TabletAb.App/UI/ProfileChartPanel.cs`: X and Y charts drawn with the
  ImGui draw list — raw-count bars, sample-and-hold, gold device peak,
  gradient bars, index/value/scale labels.
- [x] Fed from the live frame stream; respects `Freeze display`.

**Exit:** same device state renders equivalently in app and web.
**Status:** implemented; 12 layout tests. Independent renderer review found one
real bug (the Y-axis reference/baseline span used the bar group instead of the
full plot) plus font-anchor differences, both fixed.

## Phase 7 — Input pipeline (internal / external) `[x]`

- [x] Internal: OTD 0.6.7 library for release HID (`256c:006f`), like lazer.
  Built-in Huion HS611 config is used (no custom config needed); detection is
  off-thread; reports map through the OTD area transform.
- [x] Internal: debug adapter turns vendor frames (`XPos`/`YPos`) into points.
- [x] External: OS pointer sampling via `Sdl2Window.MouseMove` client coords.
- [x] Coordinate mapping: `TabletAreaTransform` (raw→mm→output pixels, clamped)
  and normalised output, ported from OTD's `AbsoluteOutputMode`.
- [x] App wiring: `--pointer off|debug|otd|external`, mode selector + point
  readout in the status panel.

**Exit:** points flow in both modes for both firmware profiles.
**Status:** implemented and unit-tested; all three modes start/shut down cleanly
on this machine. Hardware validation of the OTD path is pending a connected
release-firmware tablet.

Notes:
- OTD 0.6.7 ships **11** configs for `256C:006F` (Huion reuses the id),
  disambiguated by the device string; the firmware presents the HS611 string.
  An injected custom config would have risked double-matching, so none is used.
- External mode is focus-limited on Wayland (no global cursor); run with
  X11/XWayland for parity. OTD mode currently maps to a fixed 1920x1080 output
  area; making it configurable (and tied to the window) is a Phase 8/9 task.

## Phase 8 — Cursor points screen `[x]`

- [x] Current point + configurable N previous points as discrete fading points
  (`PointerTrail` ring buffer + `CursorPointsPanel`).
- [x] No interpolated / connecting lines (only per-point circles; grid and
  crosshair lines are separate).
- [x] Zoom + centre-on-latest; jitter readout over a fixed 240-point window.
- [x] Coordinate space switch: normalized / raw / lazer-mapped.
- [x] Fed from the active `IPointerSource` (debug frames, OTD, external).

**Exit:** debug and release both render correctly; trail length configurable.
**Status:** implemented (`PointerTrail` in Core, `CursorPointsPanel` in App).
Independent review confirmed the "no connecting lines" requirement is met, and
found two real bugs which are fixed: a stale trail on pen-lift/disconnect (now
cleared on detach and after a 300 ms gap) and jitter stats coupled to trail
length (now a fixed window).

Notes:
- Default window enlarged to 1560x1000 so all panels are on-screen initially.
- 7 new `PointerTrail` tests; total **107 tests**.

## Phase 9 — Validation & packaging `[ ]`

- [ ] Cadence / latency measured vs `tools/hidmon.py` and lazer/OTD.
- [ ] Cross-check against the existing web A/B.
- [ ] Linux + Windows builds.
- [ ] Setup documentation (udev rules, WinUSB, OTD config).

**Exit:** reproducible builds and a documented setup path.
