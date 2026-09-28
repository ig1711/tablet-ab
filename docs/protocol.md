# HS611 A/B firmware wire protocol (v6)

Canonical reference for the vendor-class USB protocol used by the
**hs611-min-ab** A/B firmware and consumed by tablet-ab. The firmware's
`src/protocol.h` is the source of truth; this document mirrors it.

The previous firmware (`hs611-fw` `DEBUG_MIN`) spoke versions 1–5 and is still
read by the parser, but the app builds only v6 commands and this document
describes v6. Setup (drivers, udev) is in [`usb-setup.md`](usb-setup.md).

## Transport

- USB vendor-class device, **VID:PID `256c:6111`**.
- One vendor-specific interface (class `0xFF`), endpoints discovered by the host:
  - **bulk IN** — the device streams 168-byte frames.
  - **bulk OUT** — the host sends 64-byte commands.
- Everything is little-endian. The device does not acknowledge commands; the
  next frame reflects the new settings.

## Frame (device → host)

Fixed **168 bytes**:

| off | size | field |
|---|---|---|
| 0 | 2 | magic `'H' 'S'` (`0x48 0x53`) |
| 2 | 1 | version (`6`) |
| 3 | 1 | seq (wraps) |
| 4 | 2 | flags (`FrameFlags`, below) |
| 6 | 1 | backend (0 software, 1 hardware, 2 repeat-coil) |
| 7 | 1 | estimator (0 n-centroid, 1 vendor, 2 gaomon, 3 log-gaussian) |
| 8 | 2 | x position, 1/256 channel (1-based) |
| 10 | 2 | y position, 1/256 channel (1-based) |
| 12 | 1 | drive frequency index (1..12) |
| 13 | 1 | read burst periods (6..32) |
| 14 | 1 | ADC samples per channel (1..7) |
| 15 | 1 | ADC clock select (0..3) |
| 16 | 1 | x tracking-window coils (0 = full scan) |
| 17 | 1 | y tracking-window coils (0 = full scan) |
| 18 | 1 | ramp-mitigation mode (0..7) |
| 19 | 1 | re-centre mode (0..3) |
| 20 | 1 | scan order (0..3) |
| 21 | 1 | warm-up reads per axis (0..8) |
| 22 | 1 | x peak loop (1..41, 0 = none) |
| 23 | 1 | y peak loop (1..27, 0 = none) |
| 24 | 4 | device time, microseconds (wraps ~59.6 s) |
| 28 | 4 | scan duration, microseconds |
| 32 | 82 | x amplitudes: 41 × u16 (axis-B loops) |
| 114 | 54 | y amplitudes: 27 × u16 (axis-A loops) |

### Flags (u16 at offset 4)

| bit | mask | meaning |
|---|---|---|
| 0 | 0x0001 | pen coupling (peak amplitude above `NearThreshold` = 100) |
| 1 | 0x0002 | windowed scan active |
| 2 | 0x0004 | full/coarse re-acquire happened this frame |
| 3 | 0x0008 | a saturated window was retried |
| 4 | 0x0010 | follow-peak re-centre active |
| 5 | 0x0020 | sticky re-centre active |
| 6 | 0x0040 | descending scan order |
| 7 | 0x0080 | free-run (carry-over) acquisition |

### Backend (offset 6)

| value | meaning |
|---|---|
| 0 | software: bit-banged drive + software-triggered ADC |
| 1 | hardware: TIMER1 carrier + ADC/DMA/TIMER2 |
| 2 | repeat-coil diagnostic (x[] is a time series of one coil) |

### Estimator (offset 7)

`0` n-point centroid, `1` vendor rational, `2` Gaomon 5-point, `3`
log-Gaussian.

## Command (host → device)

Exactly **64 bytes**, zero padded. `[0]` is the opcode. Multi-byte values are
little-endian. `0` in an optional parameter means "keep the device's current
value", except where noted.

| opcode | name | payload |
|---|---|---|
| `0x01` | `PING` | none; the device replies with a frame |
| `0x02` | `SET_FREQ` | `[1]` frequency index 1..12; clears any raw ARR override |
| `0x03` | `SET_FREQ_ARR` | `[1..2]` u16 raw TIMER1 ARR (100..400); `0` restores the index table; hardware backend only |
| `0x04` | `SET_BACKEND` | `[1]` 0 software / 1 hardware (repeat-coil is selected with `REPEAT_COIL`) |
| `0x05` | `SET_BURST` | `[1]` 6..32 read burst periods |
| `0x06` | `SET_SETTLE` | `[1..4]` A B C D microseconds (0..255) |
| `0x07` | `SET_ADC` | `[1]` samples 1..7, `[2]` clock select 0..3 (APB2 /2,/4,/6,/8) |
| `0x08` | `SET_RECOVERY` | `[1]` 0/1 site-D coil recovery |
| `0x09` | `SET_WINDOW` | `[1]` x coils 0..8, `[2]` y coils 0..8 (0 = full scan) |
| `0x0A` | `SET_RECENTER` | `[1]` mode 0..3, `[2]` hysteresis % (mode 2), `[3]` persist frames, `[4]` step coils (0 = snap), `[5]` deadband coils (mode 3) |
| `0x0B` | `SET_SCAN_ORDER` | `[1]` 0 asc / 1 desc / 2 outside-in / 3 center-out |
| `0x0C` | `SET_ESTIMATOR` | `[1]` 0..3 |
| `0x0D` | `SET_LOGAUSS` | `[1]` axis 0=X/1=Y, `[2..3]` u16 baseline, `[4..5]` a1 Q8 (i16), `[6..7]` a3 Q8 (i16) |
| `0x0E` | `SET_NCENTROID` | `[1..2]` u16 additive noise base (counts) |
| `0x0F` | `SET_REACQ` | `[1..2]` u16 threshold, `[3]` coarse stride 0/2..8, `[4..5]` u16 period ms (0 = every frame) |
| `0x10` | `SET_RAMP` | `[1]` mode 0..7, `[2]` prime burst periods, `[3]` prime repeats, `[4..5]` i16 slope per-mille/coil, `[6]` targeted-reverse radius |
| `0x11` | `SET_PACING` | `[1]` 0 SOF / 1 continuous / 2 auto |
| `0x12` | `SET_WARMUP` | `[1]` 0..8 discarded reads before each axis scan |
| `0x13` | `SET_FLAT_TOL` | `[1..2]` u16 flat-top hold tolerance (0 = off) |
| `0x14` | `REPEAT_COIL` | `[1]` 0 off, 1..41 repeat that axis-B coil |

### Mode value reference

- **Re-centre** (`SET_RECENTER[1]`): `0` edge-only, `1` follow-peak, `2`
  de-entangled sticky (Schmitt), `3` deadband sticky (legacy).
- **Scan order** (`SET_SCAN_ORDER[1]`): `0` ascending, `1` descending, `2`
  outside-in interleaved, `3` center-out interleaved (2/3 windowed only).
- **Ramp mode** (`SET_RAMP[1]`): `0` none, `1` carry-over (free-run), `2`
  prime-per-window, `3` per-read steady state, `4` bidirectional average, `5`
  slope correction, `6` targeted reverse, `7` frame-alternated average.
- **Pacing** (`SET_PACING[1]`): `0` SOF-paced (one acquisition per USB frame),
  `1` continuous/free-run, `2` auto (free-run only in the carry-over ramp mode).
- **ADC clock** (`SET_ADC[2]`): `0` APB2/2 · 36 MHz, `1` /4 · 18 MHz, `2` /6 ·
  12 MHz, `3` /8 · 9 MHz.

## Connect-time handshake

On attach the app pushes the full configuration with `PushAll` (see
`AcquisitionSettings.PushAll`): frequency (index or raw ARR), pacing, backend,
burst, ADC, recovery, window, re-acquire, estimator + centroid base, both
log-Gaussian axes, re-centre, scan order, warm-up, flat tolerance, ramp, settles
and repeat-coil. There is no explicit handshake or ack; the first frame after
this sequence reflects the settings.

## Legacy frames (v1–v5)

The parser still decodes the previous firmware's frames so old captures and
hardware keep working, mapping them onto the same model:

- v5 = 168 B, v4 = 160 B, v3 = 156 B, v2 = 152 B, v1 = 148 B.
- v5 header packed a `mode` byte (backend + ramp), an `options` byte, and
  build/send durations; v6 drops those in favour of explicit fields and a u16
  flags word. Build/send durations are not present in v6.

## Driving the legacy v5 firmware

The v6 command opcodes do not match v5 (some collide), so the host must not
send v6 commands to a v5 device. tablet-ab handles this automatically:

- On connect it waits for the first frame, reads its version, and selects the
  matching command set: **v6** for hs611-min-ab, **v5** for the previous
  `DEBUG_MIN` build. The status panel shows the detected `protocol`.
- The v5 adapters expand the settings that v6 merged: `SET_ADC` →
  `SET_ADC_N` (0x13) + `SET_ADC_CLK` (0x16); `SET_REACQ` → `SET_REACQ` (0x18) +
  `SET_COARSE` (0x19) + `SET_REACQ_PERIOD` (0x23); `SET_WINDOW` → `SET_WINDOW_N`
  (0x1B); `SET_BACKEND` → `SET_TIMING` (0x10).

To force a protocol (e.g. if auto-detection is not possible), start with:

```sh
tablet-ab --source vendor --protocol v5
```

`--protocol v6` forces the current firmware; the default is `auto`. Individual
control changes also use the detected command set.
