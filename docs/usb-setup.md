# USB setup

How to make the vendor-class debug firmware (`256c:6111`, HS611; or
`256c:6112`, Gaomon S620 fork) reachable by `tablet-ab`. Release HID firmware
(`256c:006f`) is a separate path (Phase 7) and is read via OpenTabletDriver /
HID, not libusb.

The wire protocol itself is documented in [`protocol.md`](protocol.md). The
current debug firmware is [hs611-min-ab](https://github.com/ig1711/hs611-min-ab)
(protocol v6/v7).

## Linux

Enumeration works unprivileged, but opening the device, claiming the interface
and bulk transfers need read/write access to the USB node. Add a udev rule:

```sh
# /etc/udev/rules.d/72-hs611.rules
SUBSYSTEM=="usb", ATTR{idVendor}=="256c", ATTR{idProduct}=="6111", MODE="0660", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="256c", ATTR{idProduct}=="6112", MODE="0660", TAG+="uaccess"
```

Then reload:

```sh
sudo udevadm control --reload-rules && sudo udevadm trigger
```

- Requires the system `libusb` package (provides `libusb-1.0.so.0`). The
  `LibUsbDotNet` package loads the versioned system library; nothing is bundled.
- A vendor-class interface (class `0xFF`) has no kernel driver, so no driver
  detach is needed. `VendorUsbTransport` still requests auto-detach defensively.
- Verify:

  ```sh
  dotnet run --project src/TabletAb.App -- --list-devices
  dotnet run --project src/TabletAb.App -- --dump-frames 3000 --source vendor
  ```

## Windows

A vendor-class device has no inbox driver, so the debug firmware needs the
**WinUSB** driver bound to its vendor interface, installed with
[Zadig](https://zadig.akeo.ie/) (choose the device, select the WinUSB driver,
install). OpenTabletDriver must not be running for the debug device.

`LibUsbDotNet` bundles no native library on Windows either. Ship the official
`libusb-1.0.dll` (x64) in `third_party/`; the app project copies it beside the
executable (see the conditional `None` item in `TabletAb.App.csproj`).

- Release HID firmware (`256c:006f`) is unaffected by Zadig: it is a normal HID
  device for OpenTabletDriver.
- Verify with the same `--list-devices` / `--dump-frames` commands.

## Which device id belongs to which build

| build | VID:PID | transport |
|---|---|---|
| `DEBUG_MIN=1` / `DEBUG_DUMP=1` | 256c:6111 | vendor bulk (this doc) |
| `RELEASE=1 REL_DEBUG=1` | 256c:6111 | vendor bulk, no commands |
| Gaomon S620 debug fork | 256c:6112 | vendor bulk (this doc) |
| `RELEASE=1` | 256c:006f | HID report id 8 (Phase 7) |
