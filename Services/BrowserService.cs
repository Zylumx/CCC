using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CCC.Services;

public sealed class BrowserStatus
{
    public bool Installed { get; init; }

    public bool Running { get; init; }

    public string Version { get; init; } = "Unknown";

    public string? Path { get; init; }
}

public sealed class BrowserService
{
    // ============================================================
    // BROWSER STATUS
    // ============================================================

    public BrowserStatus GetBrowserStatus(string processName)
    {
        var process =
            processName.Equals(
                "chrome",
                StringComparison.OrdinalIgnoreCase)
                ? "chrome.exe"
                : "msedge.exe";

        var path = FindBrowserExecutable(process);

        bool installed = path != null;

        bool running =
            Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(process))
            .Length > 0;

        string version = "Unknown";

        if (path != null)
        {
            try
            {
                version =
                    FileVersionInfo
                        .GetVersionInfo(path)
                        .FileVersion
                    ?? "Unknown";
            }
            catch
            {
            }
        }

        return new BrowserStatus
        {
            Installed = installed,
            Running = running,
            Version = version,
            Path = path
        };
    }


    // ============================================================
    // TERMINATE CHROME
    // ============================================================

    public int TerminateChrome()
    {
        int count = 0;

        foreach (var process in
                 Process.GetProcessesByName("chrome"))
        {
            try
            {
                process.Kill(true);
                count++;
            }
            catch
            {
            }
        }

        return count;
    }


    // ============================================================
    // CLEAN CURRENT USER
    // ============================================================

    public async Task CleanCurrentUserAsync(
        Action<string> log)
    {
        TerminateBrowser("chrome", log);

        TerminateBrowser("msedge", log);

        await Task.Delay(300);

        string local =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        string chrome =
            Path.Combine(
                local,
                "Google",
                "Chrome",
                "User Data");

        string edge =
            Path.Combine(
                local,
                "Microsoft",
                "Edge",
                "User Data");

        ClearBrowserData(
            chrome,
            log);

        ClearBrowserData(
            edge,
            log);
    }


    // ============================================================
    // CHROME REPAIR
    // ============================================================

    public async Task RepairChromeAsync(
        Action<string> status)
    {
        // ========================================================
        // 1. STOP CHROME
        // ========================================================

        status("Stopping Chrome...");

        foreach (var process in
                 Process.GetProcessesByName("chrome"))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
            }
        }

        await Task.Delay(1500);


        // ========================================================
        // 2. STOP GOOGLE UPDATE SERVICES
        // ========================================================

        status("Stopping Google services...");

        await ProcessRunner.RunAsync(
            "sc.exe",
            "stop GoogleUpdaterInternalService");

        await ProcessRunner.RunAsync(
            "sc.exe",
            "stop GoogleUpdaterService");

        await ProcessRunner.RunAsync(
            "sc.exe",
            "stop gupdate");

        await ProcessRunner.RunAsync(
            "sc.exe",
            "stop gupdatem");

        await Task.Delay(1000);


        // ========================================================
        // 3. RUN CHROME OFFICIAL UNINSTALLER
        // ========================================================

        status("Uninstalling Chrome...");

        string[] chromeExecutables =
        {
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                @"Google\Chrome\Application\chrome.exe"),

            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                @"Google\Chrome\Application\chrome.exe")
        };

        bool uninstallerFound = false;

        foreach (string chromeExe in
                 chromeExecutables)
        {
            if (!File.Exists(chromeExe))
                continue;

            uninstallerFound = true;

            string? chromeDirectory =
                Path.GetDirectoryName(chromeExe);

            if (string.IsNullOrWhiteSpace(
                chromeDirectory))
            {
                continue;
            }

            string setupExe =
                Path.Combine(
                    chromeDirectory,
                    "Installer",
                    "setup.exe");

            if (!File.Exists(setupExe))
            {
                status(
                    "Chrome uninstaller was not found.");
                continue;
            }

            status(
                "Running Chrome's official uninstaller...");

            var uninstallResult =
                await ProcessRunner.RunAsync(
                    setupExe,
                    "--uninstall --system-level --force-uninstall");

            if (!string.IsNullOrWhiteSpace(
                uninstallResult.Output))
            {
                status(
                    uninstallResult.Output.Trim());
            }

            if (!string.IsNullOrWhiteSpace(
                uninstallResult.Error))
            {
                status(
                    uninstallResult.Error.Trim());
            }
        }

        if (!uninstallerFound)
        {
            status(
                "Chrome executable not found. " +
                "Continuing cleanup...");
        }

        await Task.Delay(2000);


        // ========================================================
        // 4. REMOVE GOOGLE SCHEDULED TASKS
        // ========================================================

        status(
            "Removing Google scheduled tasks...");

        await ProcessRunner.RunAsync(
            "schtasks.exe",
            "/Delete /TN \"GoogleUpdateTaskMachineCore\" /F");

        await ProcessRunner.RunAsync(
            "schtasks.exe",
            "/Delete /TN \"GoogleUpdateTaskMachineUA\" /F");

        await ProcessRunner.RunAsync(
            "schtasks.exe",
            "/Delete /TN \"GoogleUpdaterTaskSystem\" /F");


        // ========================================================
        // 5. REMOVE GOOGLE SERVICES
        // ========================================================

        status(
            "Removing Google updater services...");

        await ProcessRunner.RunAsync(
            "sc.exe",
            "delete GoogleUpdaterInternalService");

        await ProcessRunner.RunAsync(
            "sc.exe",
            "delete GoogleUpdaterService");

        await ProcessRunner.RunAsync(
            "sc.exe",
            "delete gupdate");

        await ProcessRunner.RunAsync(
            "sc.exe",
            "delete gupdatem");


        // ========================================================
        // 6. REMOVE CHROME USER DATA
        // ========================================================

        status(
            "Removing Chrome user data...");

        string usersPath = @"C:\Users";

        if (Directory.Exists(usersPath))
        {
            foreach (string profile in
                     Directory.GetDirectories(usersPath))
            {
                string name =
                    Path.GetFileName(profile);

                if (new[]
                    {
                        "Public",
                        "Default",
                        "Default User",
                        "All Users"
                    }
                    .Contains(
                        name,
                        StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                DeleteDirectory(
                    Path.Combine(
                        profile,
                        @"AppData\Local\Google\Chrome"),
                    status);

                DeleteDirectory(
                    Path.Combine(
                        profile,
                        @"AppData\Roaming\Google\Chrome"),
                    status);

                DeleteDirectory(
                    Path.Combine(
                        profile,
                        @"AppData\Local\Google\Update"),
                    status);
            }
        }


        // ========================================================
        // 7. REMOVE CHROME PROGRAM FILES
        // ========================================================

        status(
            "Removing remaining Chrome files...");

        DeleteDirectory(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                @"Google\Chrome"),
            status);

        DeleteDirectory(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                @"Google\Chrome"),
            status);


        // ========================================================
        // 8. REMOVE CHROME REGISTRY REGISTRATION
        // ========================================================

        status(
            "Removing Chrome registration...");

        DeleteRegistryKey(
            Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Google Chrome",
            status);

        DeleteRegistryKey(
            Registry.LocalMachine,
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Google Chrome",
            status);

        DeleteRegistryKey(
            Registry.LocalMachine,
            @"SOFTWARE\Google\Chrome",
            status);

        DeleteRegistryKey(
            Registry.LocalMachine,
            @"SOFTWARE\WOW6432Node\Google\Chrome",
            status);


        // ========================================================
        // 9. VERIFY CHROME REMOVAL
        // ========================================================

        status(
            "Verifying Chrome removal...");

        await Task.Delay(2000);

        string[] remainingChromeExecutables =
        {
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                @"Google\Chrome\Application\chrome.exe"),

            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                @"Google\Chrome\Application\chrome.exe")
        };

        bool chromeStillExists =
            remainingChromeExecutables.Any(
                File.Exists);

        if (chromeStillExists)
        {
            throw new InvalidOperationException(
                "Chrome could not be completely removed. " +
                "The Chrome executable is still present.");
        }

        status(
            "Chrome successfully removed.");


        // ========================================================
        // 10. REINSTALL CHROME
        // ========================================================

        status(
            "Installing Chrome with WinGet...");

        var result =
            await ProcessRunner.RunAsync(
                "winget.exe",
                "install " +
                "--id Google.Chrome " +
                "-e " +
                "--source winget " +
                "--accept-source-agreements " +
                "--accept-package-agreements");

        if (!string.IsNullOrWhiteSpace(
            result.Output))
        {
            status(
                result.Output.Trim());
        }

        if (!string.IsNullOrWhiteSpace(
            result.Error))
        {
            status(
                result.Error.Trim());
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "WinGet failed to install Chrome.\n\n" +
                $"Exit code: {result.ExitCode}\n\n" +
                $"{result.Error}");
        }


        // ========================================================
        // 11. VERIFY CHROME INSTALLATION
        // ========================================================

        status(
            "Verifying Chrome installation...");

        string[] installedChromePaths =
        {
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                @"Google\Chrome\Application\chrome.exe"),

            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                @"Google\Chrome\Application\chrome.exe")
        };

        bool installed = false;

        for (int i = 0; i < 10; i++)
        {
            if (installedChromePaths.Any(
                File.Exists))
            {
                installed = true;
                break;
            }

            await Task.Delay(1000);
        }

        if (!installed)
        {
            throw new InvalidOperationException(
                "Chrome installation did not complete. " +
                "WinGet returned successfully, but " +
                "chrome.exe could not be found.");
        }

        status(
            "Chrome installation verified.");

        status(
            "Chrome repair completed successfully.");
    }


    // ============================================================
    // REGISTRY CLEANUP
    // ============================================================

    private void DeleteRegistryKey(
        RegistryKey root,
        string subKey,
        Action<string> status)
    {
        try
        {
            root.DeleteSubKeyTree(
                subKey,
                throwOnMissingSubKey: false);

            status(
                $"Removed registry: {subKey}");
        }
        catch (Exception ex)
        {
            status(
                $"Registry cleanup warning: {ex.Message}");
        }
    }


    // ============================================================
    // TERMINATE BROWSER
    // ============================================================

    private static void TerminateBrowser(
        string name,
        Action<string> log)
    {
        foreach (var process in
                 Process.GetProcessesByName(name))
        {
            try
            {
                process.Kill(true);

                log(
                    $"Stopped {name}.");
            }
            catch
            {
            }
        }
    }


    // ============================================================
    // CLEAR BROWSER DATA
    // ============================================================

    private static void ClearBrowserData(
        string root,
        Action<string> log)
    {
        if (!Directory.Exists(root))
            return;

        var relativePaths =
            new[]
            {
                "Cache",
                "Code Cache",
                "GPUCache",
                @"Service Worker\CacheStorage",
                @"Network\Cookies",
                @"Network\Cookies-journal"
            };

        foreach (string item in relativePaths)
        {
            string path =
                Path.Combine(
                    root,
                    item);

            if (Directory.Exists(path))
            {
                DeleteDirectory(
                    path,
                    log);
            }
            else if (File.Exists(path))
            {
                DeleteFile(
                    path,
                    log);
            }
        }
    }


    // ============================================================
    // DELETE DIRECTORY
    // ============================================================

    private static void DeleteDirectory(
        string path,
        Action<string> log)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(
                path,
                recursive: true);

            log(
                $"Removed {path}");
        }
        catch (Exception ex)
        {
            log(
                $"Could not remove {path}: {ex.Message}");
        }
    }


    // ============================================================
    // DELETE FILE
    // ============================================================

    private static void DeleteFile(
        string path,
        Action<string> log)
    {
        try
        {
            File.Delete(path);

            log(
                $"Removed {path}");
        }
        catch (Exception ex)
        {
            log(
                $"Could not remove {path}: {ex.Message}");
        }
    }


    // ============================================================
    // FIND BROWSER EXECUTABLE
    // ============================================================

    private static string? FindBrowserExecutable(
        string exe)
    {
        var roots =
            new[]
            {
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),

                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),

                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData)
            };

        foreach (string root in
                 roots.Where(
                     Directory.Exists))
        {
            try
            {
                string? hit =
                    Directory
                        .EnumerateFiles(
                            root,
                            exe,
                            SearchOption.AllDirectories)
                        .FirstOrDefault();

                if (hit != null)
                    return hit;
            }
            catch
            {
            }
        }

        return null;
    }
}