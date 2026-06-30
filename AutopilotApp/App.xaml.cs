using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace ETS2Autopilot;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!IsRunningAsAdministrator())
        {
            if (TryRelaunchAsAdministrator())
            {
                Shutdown();
                return;
            }
            // Bruger afviste UAC-prompten; fortsæt uden admin (vJoy vil ikke virke).
        }
        base.OnStartup(e);
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool TryRelaunchAsAdministrator()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule!.FileName!;
            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas",
            };
            Process.Start(psi);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Brugeren klikkede "Nej" i UAC-prompten.
            return false;
        }
    }
}
