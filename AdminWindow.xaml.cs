using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CCC.Services;

namespace CCC;

public partial class AdminWindow : Window
{
    private readonly Logger _logger = new();
    private readonly BrowserService _browser = new();

    private readonly List<WingetApp> _apps = new()
    {
        new("Google Chrome", "Google.Chrome"),
        new("Mozilla Firefox", "Mozilla.Firefox"),
        new("7-Zip", "7zip.7zip"),
        new("Notepad++", "Notepad++.Notepad++"),
        new("VLC Media Player", "VideoLAN.VLC"),
        new("Adobe Acrobat Reader", "Adobe.Acrobat.Reader.64-bit"),
        new("Citrix Workspace", "Citrix.Workspace"),
        new("RingCentral", "RingCentral.RingCentral")
    };

    private readonly string[] _tools =
    {
        "PING",
        "NSLOOKUP",
        "IPCONFIG",
        "FLUSH DNS",
        "TRACERT",
        "ROUTE",
        "ARP",
        "SYSTEMINFO",
        "HOSTNAME",
        "WHOAMI",
        "GPUPDATE",
        "SERVICES",
        "RESTART EXPLORER"
    };

    public AdminWindow()
    {
        InitializeComponent();

        AppsList.ItemsSource = _apps;

        foreach (var tool in _tools)
        {
            var button = new Button
            {
                Content = tool,
                Margin = new Thickness(3),
                Padding = new Thickness(7, 4, 7, 4),
                Tag = tool
            };

            button.Click += Tool_Click;
            ToolsPanel.Children.Add(button);
        }

        ShowRepairView();
    }

    // ============================================================
    // TOP NAVIGATION
    // ============================================================

    private void RepairChrome_Click(object sender, RoutedEventArgs e)
    {
        ShowRepairView();

        if (!Elevation.IsAdministrator())
        {
            Elevation.RestartElevated();
            return;
        }

        _ = RepairChromeAsync();
    }

    private void Apps_Click(object sender, RoutedEventArgs e)
    {
        ShowAppsView();
    }

    private void Tools_Click(object sender, RoutedEventArgs e)
    {
        ShowToolsView();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ============================================================
    // VIEW SWITCHING
    // ============================================================

    private void ShowRepairView()
    {
        RepairView.Visibility = Visibility.Visible;
        AppsView.Visibility = Visibility.Collapsed;
        ToolsView.Visibility = Visibility.Collapsed;
    }

    private void ShowAppsView()
    {
        RepairView.Visibility = Visibility.Collapsed;
        AppsView.Visibility = Visibility.Visible;
        ToolsView.Visibility = Visibility.Collapsed;
    }

    private void ShowToolsView()
    {
        RepairView.Visibility = Visibility.Collapsed;
        AppsView.Visibility = Visibility.Collapsed;
        ToolsView.Visibility = Visibility.Visible;
    }

    // ============================================================
    // CHROME REPAIR
    // ============================================================

    private async Task RepairChromeAsync()
    {
        RepairProgress.IsIndeterminate = true;
        RepairStatus.Text = "Repairing Chrome...";

        try
        {
            await _browser.RepairChromeAsync(
                msg => Dispatcher.Invoke(() =>
                {
                    RepairStatus.Text = msg;
                })
            );

            RepairStatus.Text = "Chrome repair completed.";

            MessageBox.Show(
                "Chrome repair/reinstall completed.",
                "CCC • Repair Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            RepairStatus.Text = "Repair failed.";

            MessageBox.Show(
                ex.Message,
                "CCC • Repair Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        finally
        {
            RepairProgress.IsIndeterminate = false;
        }
    }

    // ============================================================
    // WINGET APPLICATION INSTALLATION
    // ============================================================

    private async void InstallApp_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not WingetApp app)
        {
            return;
        }

        try
        {
            button.IsEnabled = false;

            AppendConsole("");
            AppendConsole(
                $"C:\\Windows\\System32> winget install --id {app.Id} -e --source winget --accept-source-agreements --accept-package-agreements --silent --disable-interactivity");

            var result = await ProcessRunner.RunAsync(
                "winget.exe",
                $"install --id \"{app.Id}\" " +
                $"-e " +
                $"--source winget " +
                $"--accept-source-agreements " +
                $"--accept-package-agreements " +
                $"--silent " +
                $"--disable-interactivity"
            );

            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                AppendConsole(result.Output);
            }

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                AppendConsole(result.Error);
            }

            AppendConsole(
                $"WinGet exit code: {result.ExitCode}");

            if (result.ExitCode == 0)
            {
                AppendConsole(
                    $"SUCCESS: {app.Name} installed successfully.");
            }
            else
            {
                AppendConsole(
                    $"FAILED: {app.Name} installation failed.");
            }
        }
        catch (Exception ex)
        {
            AppendConsole(
                $"ERROR installing {app.Name}: {ex.Message}");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    // ============================================================
    // TROUBLESHOOTING TOOLS
    // ============================================================

    private async void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string tool)
        {
            return;
        }

        if (tool == "RESTART EXPLORER")
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Ignore processes that cannot be terminated.
                    }
                }

                await Task.Delay(700);

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        UseShellExecute = true
                    }
                );

                AppendConsole("Explorer restarted.");
            }
            catch (Exception ex)
            {
                AppendConsole($"ERROR: {ex.Message}");
            }

            return;
        }

        string command = tool switch
        {
            "PING" => "ping 8.8.8.8",
            "NSLOOKUP" => "nslookup google.com",
            "IPCONFIG" => "ipconfig /all",
            "FLUSH DNS" => "ipconfig /flushdns",
            "TRACERT" => "tracert 8.8.8.8",
            "ROUTE" => "route print",
            "ARP" => "arp -a",
            "SYSTEMINFO" => "systeminfo",
            "HOSTNAME" => "hostname",
            "WHOAMI" => "whoami /all",
            "GPUPDATE" => "gpupdate /force",
            "SERVICES" => "sc query",
            _ => tool
        };

        AppendConsole(
            $"C:\\Windows\\System32> {command}"
        );

        try
        {
            var parts = SplitCommand(command);

            var result = await ProcessRunner.RunAsync(
                parts.file,
                parts.args
            );

            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                AppendConsole(result.Output);
            }

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                AppendConsole(result.Error);
            }
        }
        catch (Exception ex)
        {
            AppendConsole($"ERROR: {ex.Message}");
        }
    }

    // ============================================================
    // CONSOLE
    // ============================================================

    private void AppendConsole(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ConsoleOutput.AppendText(
            text.TrimEnd() +
            Environment.NewLine
        );

        ConsoleOutput.ScrollToEnd();
    }

    private void ClearConsole_Click(
        object sender,
        RoutedEventArgs e)
    {
        ConsoleOutput.Clear();
    }

    private void CopyConsole_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ConsoleOutput.Text))
        {
            Clipboard.SetText(ConsoleOutput.Text);
        }
    }

    // ============================================================
    // COMMAND PARSER
    // ============================================================

    private static (string file, string args) SplitCommand(
        string command)
    {
        int firstSpace = command.IndexOf(' ');

        if (firstSpace < 0)
        {
            return (command, "");
        }

        return
        (
            command[..firstSpace],
            command[(firstSpace + 1)..]
        );
    }
}

// ================================================================
// WINGET APPLICATION MODEL
// ================================================================

public record WingetApp(
    string Name,
    string Id
);