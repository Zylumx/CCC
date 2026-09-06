using System;
using System.Diagnostics;
using System.Security.Principal;

namespace CCC.Services;

public static class Elevation
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();

        var principal = new WindowsPrincipal(identity);

        return principal.IsInRole(
            WindowsBuiltInRole.Administrator);
    }

    public static void RestartElevated()
    {
        var exePath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(exePath))
            return;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = "--admin"
            };

            Process.Start(startInfo);

            System.Windows.Application.Current.Shutdown();
        }
        catch
        {
            // User cancelled the UAC prompt.
        }
    }
}