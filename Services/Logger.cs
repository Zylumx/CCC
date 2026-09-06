using System;
using System.IO;

namespace CCC.Services;

public sealed class Logger
{
    private static readonly string DirectoryPath = @"C:\ProgramData\CCC\Logs";
    private readonly string _file;

    public Logger()
    {
        try { Directory.CreateDirectory(DirectoryPath); } catch { }
        _file = Path.Combine(DirectoryPath, $"CCC_{DateTime.Now:yyyyMMdd}.log");
    }

    public void Write(string message)
    {
        try
        {
            File.AppendAllText(_file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        catch { }
    }
}
