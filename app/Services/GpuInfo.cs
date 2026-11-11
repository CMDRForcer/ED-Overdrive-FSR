using System.Runtime.InteropServices;

namespace GenFrame.Setup.Services;

public enum GpuVendor { Unknown, Nvidia, Amd, Intel }

// The vendor of the GPU driving the main display, read from the display devices (no registry, no driver call).
public static class GpuInfo
{
    public static GpuVendor PrimaryVendor()
    {
        try {
            var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            for (uint i = 0; EnumDisplayDevicesW(null, i, ref device, 0); i++) {
                if ((device.StateFlags & PrimaryDevice) != 0) {
                    return VendorOf(device.DeviceId);
                }
                device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            }
        }
        catch (Exception) {
        }
        return GpuVendor.Unknown;
    }

    // "PCI\VEN_10DE&DEV_..." : 10DE NVIDIA, 1002 AMD, 8086 Intel.
    static GpuVendor VendorOf(string id) =>
        id.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase) ? GpuVendor.Nvidia
        : id.Contains("VEN_1002", StringComparison.OrdinalIgnoreCase) ? GpuVendor.Amd
        : id.Contains("VEN_8086", StringComparison.OrdinalIgnoreCase) ? GpuVendor.Intel
        : GpuVendor.Unknown;

    const int PrimaryDevice = 4; // DISPLAY_DEVICE_PRIMARY_DEVICE

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumDisplayDevicesW(string? device, uint index, ref DisplayDevice displayDevice, uint flags);
}
