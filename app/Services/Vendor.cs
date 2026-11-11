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

    // Colour scheme of the vendor: dark greys, NVIDIA green or AMD red as the accent.
    public static void ApplyTheme(ResourceDictionary resources)
    {
        Set(resources, "WindowBrush", "#121212");
        Set(resources, "PanelBrush", "#1B1B1B");
        Set(resources, "CardBrush", "#262626");
        Set(resources, "BorderBrush", "#3A3A3A");
        Set(resources, "TextBrush", "#EDEDED");
        Set(resources, "MutedBrush", "#A3A3A3");
        Set(resources, "AccentBrush", Amd ? "#ED1C24" : "#76B900");
        Set(resources, "AccentTextBrush", Amd ? "#FFFFFF" : "#000000");
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
