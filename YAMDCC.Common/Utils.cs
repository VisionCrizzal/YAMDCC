// This file is part of YAMDCC (Yet Another MSI Dragon Center Clone).
// Copyright © Sparronator9999 and Contributors 2023-2025.
//
// YAMDCC is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the Free
// Software Foundation, either version 3 of the License, or (at your option)
// any later version.
//
// YAMDCC is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
// more details.
//
// You should have received a copy of the GNU General Public License along with
// YAMDCC. If not, see <https://www.gnu.org/licenses/>.

using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Windows.Forms;

namespace YAMDCC.Common;

/// <summary>
/// A collection of miscellaneous useful utilities
/// </summary>
public static class Utils
{
    /// <summary>
    /// Shows an information dialog.
    /// </summary>
    /// <param name="message">
    /// The message to show in the info dialog.
    /// </param>
    /// <param name="title">
    /// The text to show in the title bar of the dialog.
    /// </param>
    /// <param name="buttons">
    /// One of the <see cref="MessageBoxButtons"/> values
    /// that specifies which buttons to display in the dialog.
    /// </param>
    /// <returns>
    /// One of the <see cref="DialogResult"/> values.
    /// </returns>
    public static DialogResult ShowInfo(string message, string title,
        MessageBoxButtons buttons = MessageBoxButtons.OK)
    {
        return MessageBox.Show(message, title, buttons, MessageBoxIcon.Asterisk);
    }

    /// <summary>
    /// Shows a warning dialog.
    /// </summary>
    /// <param name="message">
    /// The message to show in the warning dialog.
    /// </param>
    /// <param name="title">
    /// The text to show in the title bar of the dialog.
    /// </param>
    /// <param name="button">
    /// One of the <see cref="MessageBoxDefaultButton"/> values
    /// that specifies the default button for the dialog.
    /// </param>
    /// <returns>
    /// One of the <see cref="DialogResult"/> values.
    /// </returns>
    public static DialogResult ShowWarning(string message, string title,
        MessageBoxDefaultButton button = MessageBoxDefaultButton.Button1)
    {
        return MessageBox.Show(message, title, MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, button);
    }

    /// <summary>
    /// Shows an error dialog.
    /// </summary>
    /// <param name="message">
    /// The message to show in the error dialog.
    /// </param>
    /// <returns>
    /// One of the <see cref="DialogResult"/> values.
    /// </returns>
    public static DialogResult ShowError(string message)
    {
        return MessageBox.Show(message, "Error",
            MessageBoxButtons.OK, MessageBoxIcon.Stop);
    }

    /// <summary>
    /// Gets a <see cref="Version"/> that can be used to compare
    /// application versions.
    /// </summary>
    /// <returns>
    /// A <see cref="Version"/> object corresponding to the entry application's
    /// version if parsing was successful, otherwise <see langword="null"/>.
    /// </returns>
    public static Version GetCurrentVersion()
    {
        return GetVersion(Application.ProductVersion);
    }

    public static Version GetVersion(string verString)
    {
        // expected version format: X.Y.Z-SUFFIX[.W]+REVISION,
        if (verString.Contains("-"))
        {
            // remove suffix and revision
            verString = verString.Remove(verString.IndexOf('-'));
        }
        else if (verString.Contains("+"))
        {
            // remove revision if suffix doesn't exist for some reason
            verString = verString.Remove(verString.IndexOf('+'));
        }

        return Version.TryParse(verString, out Version ver) ? ver : null;
    }
    public static int GetCurrentSuffixVer(int suffixNum = 0)
    {
        return GetSuffixVer(Application.ProductVersion, suffixNum);
    }

    public static int GetSuffixVer(string verString, int suffixNum = 0)
    {
        // format: X.Y.Z-SUFFIX[.W]+REVISION,
        // where W is a beta/release candidate version if applicable
        if (verString.Contains("-"))
        {
            verString = verString.Remove(0, verString.IndexOf('-') + 1);
        }

        if (verString.Contains("."))
        {
            suffixNum++;
            int index;
            do
            {
                // SUFFIX[.W][-SUFFIX2[.V]...]+REVISION
                index = verString.IndexOf('.');
                if (index == -1)
                {
                    return -1;
                }
                verString = verString.Remove(0, index + 1);
                suffixNum--;
            }
            while (suffixNum > 0);

            if (verString.Contains("-"))
            {
                // remove other suffixes
                verString = verString.Remove(verString.IndexOf('-'));
            }
            else if (verString.Contains("+"))
            {
                // remove Git hash, if it exists (for "dev" detection)
                verString = verString.Remove(verString.IndexOf('+'));
            }
        }
        else
        {
            // suffix with version probably doesn't exist...
            return -1;
        }

        return int.TryParse(verString, out int version) ? version : -1;
    }

    public static string GetCurrentVerSuffix()
    {
        return GetVerSuffix(Application.ProductVersion);
    }

    public static string GetVerSuffix(string verString, int suffixNum = 0)
    {
        // format: X.Y.Z-SUFFIX[.W]+REVISION,
        // where W is a beta/release candidate version if applicable
        string verSuffix = verString;

        if (verSuffix.Contains("-"))
        {
            suffixNum++;
            int index;
            do
            {
                // SUFFIX[.W][-SUFFIX2[.V]...]+REVISION
                index = verSuffix.IndexOf('-');
                verSuffix = verSuffix.Remove(0, index + 1);
                suffixNum--;
            }
            while (index != -1 && suffixNum > 0);

            if (verSuffix.Contains("."))
            {
                // remove suffix version number
                verSuffix = verSuffix.Remove(verSuffix.IndexOf('.'));
            }
            else if (verSuffix.Contains("+"))
            {
                // remove Git hash, if it exists (for "dev" detection)
                verSuffix = verSuffix.Remove(verSuffix.IndexOf('+'));
            }
        }
        else
        {
            // suffix probably doesn't exist...
            verSuffix = string.Empty;
        }

        return verSuffix.ToLowerInvariant();
    }

    public static string GetVerString()
    {
        // format: X.Y.Z-SUFFIX[.W]+REVISION,
        // where W is a beta/release candidate version if applicable
        string prodVer = Application.ProductVersion;

        return GetCurrentVerSuffix() switch
        {
            // only show the version number (e.g. X.Y.Z):
            "release" => prodVer.Remove(prodVer.IndexOf('-')),
            "dev" => prodVer.Contains("+")
                // probably a development release (e.g. X.Y.Z-dev+REVISION);
                // show shortened Git commit hash if it exists:
                ? prodVer.Remove(prodVer.IndexOf('+') + 8)
                // Return the product version if not in expected format
                : prodVer,
            // everything else (i.e. beta, RC, etc.)
            _ => prodVer.Contains(".") && prodVer.Contains("+")
                // Beta releases should be in format X.Y.Z-beta.W+REVISION.
                // Remove the revision (i.e. only show X.Y.Z-beta.W):
                ? prodVer.Remove(prodVer.IndexOf('+'))
                // Just return the product version if not in expected format
                : prodVer,
        };
    }

    /// <summary>
    /// Gets the Git revision of this program, if available.
    /// </summary>
    /// <returns>
    /// The Git hash of the program version if available,
    /// otherwise <see cref="string.Empty"/>.
    /// </returns>
    public static string GetRevision()
    {
        string prodVer = Application.ProductVersion;

        return prodVer.Contains("+")
            ? prodVer.Remove(0, prodVer.IndexOf('+') + 1)
            : string.Empty;
    }

    public static Icon GetEntryAssemblyIcon()
    {
        return Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location);
    }

    public static string GetAppTitle()
    {
        return Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title;
    }

    /// <summary>
    /// Gets whether the application is running with administrator privileges.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the application is running as
    /// an administrator, otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsAdmin()
    {
        try
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Installs the specified .NET Framework
    /// service to the local computer.
    /// </summary>
    /// <remarks>
    /// The service is not started automatically. Use
    /// <see cref="StartService(string)"/> to start it if needed.
    /// </remarks>
    /// <param name="svcExe">
    /// The path to the service executable.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the service installation
    /// was successful, otherwise <see langword="false"/>.
    /// </returns>
    public static bool InstallService(string svcExe)
    {
        string runtimePath = RuntimeEnvironment.GetRuntimeDirectory();
        int exitCode = RunCmd($"{runtimePath}\\installutil.exe", $"\"{svcExe}.exe\"");
        return exitCode == 0;
    }

    /// <summary>
    /// Uninstalls the specified .NET Framework
    /// service from the local computer.
    /// </summary>
    /// <param name="svcExe">
    /// The path to the service executable.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the service uninstallation
    /// was successful, otherwise <see langword="false"/>.
    /// </returns>
    public static bool UninstallService(string svcExe)
    {
        string runtimePath = RuntimeEnvironment.GetRuntimeDirectory();
        int exitCode = RunCmd($"{runtimePath}\\installutil.exe", $"/u \"{svcExe}.exe\"");
        return exitCode == 0;
    }

    /// <summary>
    /// Starts the specified service.
    /// </summary>
    /// <param name="svcName">
    /// The service name, as shown in <c>services.msc</c>
    /// (NOT to be confused with its display name).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the service started successfully
    /// (or is already running), otherwise <see langword="false"/>.
    /// </returns>
    public static bool StartService(string svcName)
    {
        return RunCmd("net", $"start {svcName}") == 0;
    }

    /// <summary>
    /// Stops the specified service.
    /// </summary>
    /// <param name="svcName">
    /// The service name, as shown in <c>services.msc</c>
    /// (NOT to be confused with its display name).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the service was stopped successfully
    /// (or is already stopped), otherwise <see langword="false"/>.
    /// </returns>
    public static bool StopService(string svcName)
    {
        return RunCmd("net", $"stop {svcName}") == 0;
    }

    /// <summary>
    /// Checks to see if the specified service
    /// is installed on the computer.
    /// </summary>
    /// <param name="svcName">
    /// The service name, as shown in <c>services.msc</c>
    /// (NOT to be confused with its display name).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the service was
    /// found, otherwise <see langword="false"/>
    /// </returns>
    public static bool ServiceExists(string svcName)
    {
        foreach (ServiceController service in ServiceController.GetServices())
        {
            if (service.ServiceName == svcName)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks to see if the specified service
    /// is running or pending start on the computer.
    /// </summary>
    /// <param name="svcName">
    /// The service name, as shown in <c>services.msc</c>
    /// (NOT to be confused with its display name).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the service is
    /// running, otherwise <see langword="false"/>
    /// </returns>
    public static bool ServiceRunning(string svcName)
    {
        try
        {
            using (ServiceController service = new(svcName))
            {
                return service.Status
                    is ServiceControllerStatus.Running
                    or ServiceControllerStatus.StartPending;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Runs the specified executable as admin,
    /// with the specified arguments.
    /// </summary>
    /// <remarks>
    /// The process will be started with <see cref="ProcessStartInfo.UseShellExecute"/>
    /// set to <see langword="false"/>, except if the calling application is not
    /// running as an administrator, in which case
    /// <see cref="ProcessStartInfo.UseShellExecute"/> is set to
    /// <see langword="true"/> instead.
    /// </remarks>
    /// <param name="exe">
    /// The path to the executable to run.
    /// </param>
    /// <param name="args">
    /// The arguments to pass to the executable.
    /// </param>
    /// <param name="waitExit">
    /// <see langword="true"/> to wait for the executable to exit
    /// before returning, otherwise <see langword="false"/>.
    /// </param>
    /// <returns>
    /// The exit code returned by the executable (unless <paramref name="waitExit"/>
    /// is <see langword="true"/>, in which case 0 will always be returned).
    /// </returns>
    /// <exception cref="Win32Exception"/>
    public static int RunCmd(string exe, string args, bool waitExit = true)
    {
        bool shellExecute = false;
        if (!IsAdmin())
        {
            // if running unprivileged, we can't create an admin process
            // directly, so use shell execute (creating new cmd window) instead
            shellExecute = true;
        }

        using (Process p = new()
        {
            StartInfo = new ProcessStartInfo(exe, args)
            {
                CreateNoWindow = true,
                UseShellExecute = shellExecute,
                Verb = "runas",
            },
        })
        {
            p.Start();
            if (waitExit)
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }
        return 0;
    }

    /// <summary>
    /// Gets the computer model name from registry.
    /// </summary>
    /// <returns>
    /// The computer model if the function succeeds,
    /// otherwise <see cref="string.Empty"/>.'
    /// </returns>
    public static string GetPCModel()
    {
        return GetBIOSRegValue("SystemProductName");
    }

    /// <summary>
    /// Gets the computer manufacturer from registry.
    /// </summary>
    /// <returns>
    /// The computer manufacturer if the function succeeds,
    /// otherwise <see cref="string.Empty"/>.
    /// </returns>
    public static string GetPCManufacturer()
    {
        return GetBIOSRegValue("SystemManufacturer");
    }

    private static string GetBIOSRegValue(string name)
    {
        using (RegistryKey biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
        {
            return ((string)biosKey?.GetValue(name, string.Empty)).Trim();
        }
    }
}
