namespace TabletAb.Input.Tests;

public class VendorUsbLocatorTests
{
    [Fact]
    public void Find_ReturnsNonNullList_OrReportsMissingLibUsb()
    {
        IReadOnlyList<VendorUsbDeviceInfo> devices;

        try
        {
            devices = VendorUsbLocator.Find();
        }
        catch (DllNotFoundException)
        {
            // libusb-1.0 is not installed on this machine, so enumeration cannot
            // run at all. That is an environment limitation, not a code failure.
            return;
        }

        Assert.NotNull(devices);
        Assert.All(devices, device =>
        {
            Assert.Equal(VendorUsbLocator.Hs611VendorId, device.VendorId);
            Assert.True(device.ProductId is VendorUsbLocator.Hs611DebugProductId
                or VendorUsbLocator.S620DebugProductId);
        });
    }
}
