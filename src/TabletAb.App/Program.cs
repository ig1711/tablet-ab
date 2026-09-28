using System.Runtime.InteropServices;

// NOTE: this is a top-level program. The resolver below MUST run before any
// code path can touch Vulkan (see comment).
using TabletAb.App;

// Veldrid 4.9.0 pulls in Vk 1.0.25, which P/Invokes "libdl". glibc >= 2.34 no
// longer ships libdl.so (merged into libc), so Vulkan probing throws and Veldrid
// reports Vulkan as unsupported. GraphicsDevice.IsBackendSupported caches its
// result in a Lazy<bool>, so this must be installed before the first probe.
if (OperatingSystem.IsLinux())
{
    NativeLibrary.SetDllImportResolver(
        typeof(Vulkan.VulkanNative).Assembly,
        static (name, assembly, searchPath) =>
            name == "libdl" ? NativeLibrary.Load("libdl.so.2", assembly, searchPath) : IntPtr.Zero);
}

AppOptions options = AppOptions.Parse(args);

if (options.ListDevices)
    return DeviceCli.ListDevices();

if (options.DumpFrames > 0)
    return DeviceCli.DumpFrames(options);

using var app = new AbApplication(options);
app.Run();
return 0;
