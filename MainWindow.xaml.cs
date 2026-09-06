using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CCC.Services;

namespace CCC;

public partial class MainWindow : Window
{
    private readonly BrowserService _browser = new();
    private readonly Logger _logger = new();

    public ICommand OpenAdminCommand { get; }

    public MainWindow()
    {
        InitializeComponent();

        OpenAdminCommand = new RelayCommand(_ => RequestAdminAccess());

        DataContext = this;

        // Enable Ctrl + Shift + A
        PreviewKeyDown += MainWindow_PreviewKeyDown;

        Loaded += (_, _) =>
        {
            Log("CCC v4.7 WPF started.");
            RefreshEndpointInfo();
            RefreshBrowserStatus();
        };
    }

    // ============================================================
    // LOGGING
    // ============================================================

    private void Log(string message)
    {
        _logger.Write(message);

        ActivityLog.AppendText(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");

        ActivityLog.ScrollToEnd();
    }

    // ============================================================
    // CLEAN CHROME + EDGE
    // ============================================================

    private async void CleanButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Clear Chrome and Edge cache/cookies for the current Windows user?",
                "CCC • Confirm Clean",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        CleanButton.IsEnabled = false;
        TerminateChromeButton.IsEnabled = false;
        ProgressBar.IsIndeterminate = true;
        StatusText.Text = "Cleaning...";

        try
        {
            Log("Starting Chrome + Edge cache/cookie cleanup.");

            await _browser.CleanCurrentUserAsync(Log);

            Log("Browser cleanup completed.");

            StatusText.Text = "Ready";

            MessageBox.Show(
                "Chrome and Edge cache/cookies were cleared.",
                "CCC • Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log($"Cleanup failed: {ex.Message}");

            StatusText.Text = "Error";

            MessageBox.Show(
                ex.Message,
                "CCC • Cleaning Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ProgressBar.IsIndeterminate = false;

            CleanButton.IsEnabled = true;
            TerminateChromeButton.IsEnabled = true;

            RefreshBrowserStatus();
        }
    }

    // ============================================================
    // TERMINATE CHROME
    // ============================================================

    private void TerminateChromeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var count = _browser.TerminateChrome();

        Log($"Chrome terminated ({count} process(es)).");

        RefreshBrowserStatus();
    }

    // ============================================================
    // REFRESH STATUS
    // ============================================================

    private void RefreshStatusButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        RefreshEndpointInfo();
        RefreshBrowserStatus();

        Log("Browser and endpoint status refreshed.");
    }

    // ============================================================
    // BROWSER STATUS
    // ============================================================

    private void RefreshBrowserStatus()
    {
        var chrome = _browser.GetBrowserStatus("chrome");

        ChromeVersionText.Text =
            chrome.Installed
                ? $"Installed version {chrome.Version}"
                : "Not Installed";

        ChromeStatusText.Text =
            chrome.Running
                ? "RUNNING"
                : "NOT RUNNING";

        ChromeStatusText.Foreground =
            chrome.Running
                ? Brushes.LightSkyBlue
                : Brushes.LightGray;

        ChromeDot.Fill =
            chrome.Running
                ? Brushes.Gold
                : Brushes.Gray;

        var edge = _browser.GetBrowserStatus("msedge");

        EdgeVersionText.Text =
            edge.Installed
                ? $"Installed version {edge.Version}"
                : "Not Installed";

        EdgeStatusText.Text =
            !edge.Installed
                ? "NOT INSTALLED"
                : edge.Running
                    ? "RUNNING"
                    : "NOT RUNNING";

        EdgeStatusText.Foreground =
            !edge.Installed
                ? (Brush)FindResource("DangerBrush")
                : edge.Running
                    ? Brushes.Gold
                    : Brushes.LightGray;

        EdgeDot.Fill =
            !edge.Installed
                ? (Brush)FindResource("DangerBrush")
                : edge.Running
                    ? Brushes.Gold
                    : Brushes.Gray;
    }

    // ============================================================
    // ENDPOINT INFORMATION
    // ============================================================

    private void RefreshEndpointInfo()
    {
        ComputerNameText.Text = Environment.MachineName;
        ComputerUserText.Text = Environment.UserName;

        try
        {
            var candidates = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    !n.Description.Contains(
                        "Virtual",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains(
                        "VMware",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains(
                        "Hyper-V",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains(
                        "VirtualBox",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains(
                        "Docker",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains(
                        "Tailscale",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains(
                        "WireGuard",
                        StringComparison.OrdinalIgnoreCase) &&
                    !n.Description.Contains(
                        "VPN",
                        StringComparison.OrdinalIgnoreCase))
                .Select(n => new
                {
                    n.Name,

                    Addresses = n.GetIPProperties()
                        .UnicastAddresses
                        .Where(a =>
                            a.Address.AddressFamily ==
                            System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(a => a.Address)
                })
                .Where(x => x.Addresses.Any())
                .FirstOrDefault();

            ComputerIpText.Text =
                candidates?.Addresses
                    .FirstOrDefault()
                    ?.ToString()
                ?? "N/A";

            AdapterText.Text =
                candidates == null
                    ? ""
                    : $"• {candidates.Name}";
        }
        catch
        {
            ComputerIpText.Text = "N/A";
            AdapterText.Text = "";
        }

        OsText.Text =
            Environment.OSVersion.VersionString;

        RamText.Text =
            $"{GetRamGb():0.#} GB RAM";

        UptimeText.Text =
            $"UPTIME: {GetUptime()}";
    }

    // ============================================================
    // RAM
    // ============================================================

    private static double GetRamGb()
    {
        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");

            var value =
                searcher
                    .Get()
                    .Cast<ManagementObject>()
                    .First()["TotalVisibleMemorySize"];

            return Convert.ToDouble(value) / 1024 / 1024;
        }
        catch
        {
            return 0;
        }
    }

    // ============================================================
    // UPTIME
    // ============================================================

    private static string GetUptime()
    {
        var uptime =
            TimeSpan.FromMilliseconds(
                Environment.TickCount64);

        return
            $"{(int)uptime.TotalDays}d " +
            $"{uptime.Hours}h " +
            $"{uptime.Minutes}m";
    }

    // ============================================================
    // IT ADMINISTRATOR - CTRL + SHIFT + A
    // ============================================================

    private void MainWindow_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (Keyboard.Modifiers ==
                (ModifierKeys.Control | ModifierKeys.Shift) &&
            e.Key == Key.A)
        {
            e.Handled = true;

            RequestAdminAccess();
        }
    }

    // ============================================================
    // REQUEST ADMIN ACCESS
    // ============================================================

    private void RequestAdminAccess()
    {
        // Already elevated
        if (Elevation.IsAdministrator())
        {
            OpenAdminDashboard();
            return;
        }

        // Not elevated - trigger UAC
        Elevation.RestartElevated();
    }

    // ============================================================
    // OPEN IT ADMINISTRATOR WINDOW
    // ============================================================

    private void OpenAdminDashboard()
    {
        var admin = new AdminWindow
        {
            Owner = this
        };

        admin.ShowDialog();
    }
}

// ================================================================
// RELAY COMMAND
// ================================================================

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;

    public RelayCommand(Action<object?> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        _execute(parameter);
    }
}