using System;
using System.IO;

namespace LicenseGeneratorGUI;

public static class Logger
{
    public static void Log(string message, Exception? ex = null)
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (ex != null)
            {
                logEntry += $"\nEXCEPTION: {ex.Message}\nINNER: {ex.InnerException?.Message}\nSTACK: {ex.StackTrace}";
            }
            logEntry += "\n" + new string('=', 50) + "\n";
            File.AppendAllText(path, logEntry);
        }
        catch { }
    }
}
