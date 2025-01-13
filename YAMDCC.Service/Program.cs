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

using System;
using System.ServiceProcess;
using System.Windows.Forms;
using YAMDCC.Common;
using YAMDCC.Logs;

namespace YAMDCC.Service;

internal static class Program
{
    /// <summary>
    /// The <see cref="Logger"/> instance to write logs to.
    /// </summary>
    private static readonly Logger Log = new()
    {
        LogDir = Paths.Logs,
        ConsoleLevel = LogLevel.None,
        FileLevel = LogLevel.Debug,
    };

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += static (sender, e) =>
            Log.Fatal(Strings.GetString("svcException", e.ExceptionObject));

        if (Environment.UserInteractive)
        {
            Utils.ShowError(Strings.GetString("errDirectRun"));
        }
        else
        {
            CommonConfig cfg = CommonConfig.Load();
            Log.Info(
                $"OS version: {Environment.OSVersion}\n" +
                $"Service version: {Application.ProductVersion}");

            Log.FileLevel = cfg.LogLevel;
            Log.Debug("Log level is set to debug mode.");
            ServiceBase.Run(new FanControlService(Log));
        }
    }
}
