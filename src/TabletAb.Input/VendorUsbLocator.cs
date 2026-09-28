using LibUsbDotNet.LibUsb;

namespace TabletAb.Input;

/// <summary>A discovered vendor-class USB device.</summary>
/// <param name="VendorId">USB vendor id.</param>
/// <param name="ProductId">USB product id.</param>
/// <param name="Product">Product string, empty when not readable.</param>
/// <param name="Serial">Serial string, empty when not readable.</param>
/// <param name="Openable">True when the device could be opened (permissions/driver OK).</param>
public readonly record struct VendorUsbDeviceInfo(
    int VendorId,
    int ProductId,
    string Product,
    string Serial,
    bool Openable);

/// <summary>
/// Enumerates vendor-class debug devices. Enumeration itself works without
/// privileges, but reading the product/serial strings requires opening the
/// device (udev `uaccess` on Linux, or WinUSB on Windows).
/// </summary>
public static class VendorUsbLocator
{
    public const int Hs611VendorId = 0x256C;
    public const int Hs611DebugProductId = 0x6111;

    /// <summary>Gaomon S620 debug-firmware product id (30/19 coil fork).</summary>
    public const int S620DebugProductId = 0x6112;

    private static readonly (int VendorId, int ProductId)[] DefaultIds =
    {
        (Hs611VendorId, Hs611DebugProductId),
        (Hs611VendorId, S620DebugProductId),
    };

    /// <summary>Find all devices matching the default (256c:6111 / 256c:6112) allowlist.</summary>
    public static IReadOnlyList<VendorUsbDeviceInfo> Find() => Find(DefaultIds);

    /// <summary>Find all devices matching an explicit set of (vendor, product) ids.</summary>
    public static IReadOnlyList<VendorUsbDeviceInfo> Find(params (int VendorId, int ProductId)[] ids)
    {
        var found = new List<VendorUsbDeviceInfo>();

        using var context = new UsbContext();
        foreach (IUsbDevice device in context.List())
        {
            int vendorId = 0;
            int productId = 0;
            bool match = false;
            foreach ((int candidateVendor, int candidateProduct) in ids)
            {
                if (device.VendorId == candidateVendor && device.ProductId == candidateProduct)
                {
                    vendorId = candidateVendor;
                    productId = candidateProduct;
                    match = true;
                    break;
                }
            }

            if (!match)
                continue;

            string product = string.Empty;
            string serial = string.Empty;
            bool openable = false;

            try
            {
                device.Open();
                openable = true;
                product = device.Info.Product ?? string.Empty;
                serial = device.Info.SerialNumber ?? string.Empty;
                device.Close();
            }
            catch
            {
                /* enumeration still succeeded; strings just need privileges */
            }

            found.Add(new VendorUsbDeviceInfo(vendorId, productId, product, serial, openable));
        }

        return found;
    }
}
