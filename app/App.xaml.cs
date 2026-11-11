using System.Windows;
using GenFrame.Setup.Services;

namespace GenFrame.Setup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // The elevated copy that writes to the driver profile: no window, it only does that and ends.
        if (Vendor.Nvidia && e.Args.Length == 2 && e.Args[0] == NvDrs.ApplyArgument) {
            Environment.Exit(NvDrs.ApplyRequest(e.Args[1]));
        }
        Vendor.ApplyTheme(Resources);
        base.OnStartup(e);
    }
}
