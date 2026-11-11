using System.Windows;
using System.Windows.Media;

namespace GenFrame.Setup.Services;

// Vendor build (edaa/docs/vendors.md): the NVIDIA app carries everything that runs on NVIDIA cards,
// the AMD app everything that runs on AMD cards. Chosen at build time (-p:Vendor=amd).
public static class Vendor
{
    // Not const: no unreachable-code warnings where the other vendor's code is skipped.
#if VENDOR_AMD
    public static readonly bool Amd = true;
    public const string Name = "AMD";
#else
    public static readonly bool Amd = false;
    public const string Name = "NVIDIA";
#endif
    public static readonly bool Nvidia = !Amd;

    // Colour scheme of the vendor: black, dark greys, NVIDIA green or AMD red as the accent,
    // square corners. Logos are never used.
    public static void ApplyTheme(ResourceDictionary resources)
    {
        Set(resources, "WindowBrush", "#000000");
        Set(resources, "PanelBrush", Amd ? "#121212" : "#111111");
        Set(resources, "CardBrush", Amd ? "#1C1C1C" : "#1A1A1A");
        Set(resources, "BorderBrush", "#2E2E2E");
        Set(resources, "TextBrush", "#F2F2F2");
        Set(resources, "MutedBrush", "#9E9E9E");
        Set(resources, "AccentBrush", Amd ? "#ED1C24" : "#76B900");
        Set(resources, "AccentTextBrush", Amd ? "#FFFFFF" : "#000000");
        resources["Corner"] = new CornerRadius(2);

        // The vendors' own typefaces are licensed and not shipped: NVIDIA Sans when it is installed,
        // else Bahnschrift (DIN style, part of Windows 10 and 11); AMD the narrower SemiCondensed cut.
        resources["AppFont"] = new FontFamily(Amd ? "Bahnschrift SemiCondensed, Bahnschrift, Segoe UI" : "NVIDIA Sans, Bahnschrift, Segoe UI");
    }

    static void Set(ResourceDictionary resources, string key, string colour)
    {
        var value = (Color)ColorConverter.ConvertFromString(colour);
        if (resources[key] is SolidColorBrush { IsFrozen: false } brush) {
            brush.Color = value; // StaticResource users hold this instance
        }
        else {
            resources[key] = new SolidColorBrush(value);
        }
    }
}
