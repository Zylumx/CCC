using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace CCC.Services;

public static class ProcessRunner
{
    public sealed record Result(
        int ExitCode,
        string Output,
        string Error);

    public static async Task<Result> RunAsync(
        string fileName,
        string arguments)
    {
        string resolvedFileName = ResolveExecutable(fileName);

        var psi = new ProcessStartInfo
        {
            FileName = resolvedFileName,
            Arguments = arguments,

            UseShellExecute = false,

            RedirectStandardOutput = true,
            RedirectStandardError = true,

            CreateNoWindow = true,

            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,

            WorkingDirectory = Environment.SystemDirectory
        };

        using var process = new Process
        {
            StartInfo = psi
        };

        try
        {
            if (!process.Start())
            {
                return new Result(
                    -1,
                    "",
                    $"Unable to start process: {resolvedFileName}");
            }
        }
        catch (Exception ex)
        {
            return new Result(
                -1,
                "",
                $"Failed to start {resolvedFileName}: {ex.Message}");
        }

        Task<string> outputTask =
            process.StandardOutput.ReadToEndAsync();

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string output = await outputTask;
        string error = await errorTask;

        return new Result(
            process.ExitCode,
            output,
            error);
    }

    private static string ResolveExecutable(string fileName)
    {
        if (!fileName.Equals(
                "winget.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        // First try the WindowsApps execution alias.
        string localAlias = System.IO.Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "winget.exe");

        if (System.IO.File.Exists(localAlias))
        {
            return localAlias;
        }

        // Try resolving through Windows PATH.
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "winget.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            if (process != null)
            {
                string result =
                    process.StandardOutput
                        .ReadToEnd()
                        .Trim();

                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(result))
                {
                    string firstPath =
                        result.Split(
                            Environment.NewLine,
                            StringSplitOptions.RemoveEmptyEntries)[0]
                        .Trim();

                    if (System.IO.File.Exists(firstPath))
                    {
                        return firstPath;
                    }
                }
            }
        }
        catch
        {
            // Continue to normal winget resolution.
        }

        // Fall back to winget.exe and let Windows resolve it.
        return "winget.exe";
    }
}