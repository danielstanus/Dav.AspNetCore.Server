using System.ComponentModel;
using System.Diagnostics;

namespace Dav.AspNetCore.Server.Tests;

/// <summary>
/// Creates a directory link for tests. Prefers a symbolic link and falls back to a Windows junction
/// (which does not require elevation). Returns false when the environment allows neither.
/// </summary>
internal static class TestDirectoryLink
{
    public static bool TryCreate(string linkPath, string targetPath)
    {
        try
        {
            System.IO.Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            // Creating symbolic links requires privileges (or developer mode) on Windows.
        }

        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process == null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0 && System.IO.Directory.Exists(linkPath);
        }
        catch (Exception exception) when (exception is IOException or Win32Exception)
        {
            return false;
        }
    }
}
