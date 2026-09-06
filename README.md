# CCC — Clear Cookies Cache (WPF)

CCC is a Windows WPF application targeting .NET 8.

## Open in Visual Studio

1. Install Visual Studio 2022 with the **.NET desktop development** workload.
2. Open `CCC.csproj`.
3. Open `MainWindow.xaml`.
4. Use the XAML designer to adjust the UI.
5. Press **F5** to build and run.

The application is fixed at **600×600**.

## Main functions

- Chrome + Edge cache/cookie cleanup for the current user.
- Browser status detection.
- Chrome process termination.
- Endpoint information.
- Activity logging to `C:\ProgramData\CCC\Logs`.
- Hidden IT Administrator dashboard with `Ctrl + Shift + A`.
- Chrome repair/reinstall through UAC.
- WinGet application deployment.
- Network/system troubleshooting tools.

## Notes

The project intentionally uses `asInvoker` so normal users do not receive an elevation prompt. The Chrome repair action requests elevation only when it is invoked.
