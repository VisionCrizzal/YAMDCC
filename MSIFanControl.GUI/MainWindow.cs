﻿// This file is part of MSI Fan Control.
// Copyright © Sparronator9999 2023-2024.
//
// MSI Fan Control is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the Free
// Software Foundation, either version 3 of the License, or (at your option)
// any later version.
//
// MSI Fan Control is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
// more details.
//
// You should have received a copy of the GNU General Public License along with
// MSI Fan Control. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;
using MSIFanControl.Config;
using MSIFanControl.IPC;

namespace MSIFanControl.GUI
{
	public partial class MainWindow : Form
	{
		#region Fields
		/// <summary>
		/// The path where program data is stored.
		/// </summary>
		private static readonly string DataPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
			"Sparronator9999", "MSI Fan Control");

		/// <summary>
		/// The MSI Fan Control config that is currently open for editing.
		/// </summary>
		private FanControlConfig Config;

		/// <summary>
		/// The client that connects to the MSI Fan Control Service
		/// </summary>
		private readonly NamedPipeClient<ServiceResponse, ServiceCommand> IPCClient =
			new NamedPipeClient<ServiceResponse, ServiceCommand>("MSIFC-Server");

		private readonly ResourceManager Resources;

		private readonly NumericUpDown[] numUpTs = new NumericUpDown[6];
		private readonly NumericUpDown[] numDownTs = new NumericUpDown[6];
		private readonly NumericUpDown[] numFanSpds = new NumericUpDown[7];
		private readonly TrackBar[] tbFanSpds = new TrackBar[7];

		private readonly ToolTip ttMain = new ToolTip();
		#endregion

		public MainWindow(ResourceManager resMan)
		{
			InitializeComponent();
			Resources = resMan;

			// Set the window icon using the application icon.
			// Saves about 8-9 KB from not having to embed the same icon twice.
			Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);

			// set literally every tooltip
			tsiLoadConf.ToolTipText = Resources.GetString("ttLoadConf");
			tsiSaveConf.ToolTipText = Resources.GetString("ttSaveConf");
			tsiApply.ToolTipText = Resources.GetString("ttApply");
			tsiRevert.ToolTipText = Resources.GetString("ttRevert");
			tsiExit.ToolTipText = Resources.GetString("ttSelfExplan");
			tsiECMon.ToolTipText = Resources.GetString("ttECMon");
			tsiAbout.ToolTipText = Resources.GetString("ttAbout");
			tsiSource.ToolTipText = Resources.GetString("ttSource");
			ttMain.SetToolTip(cboFanSel, Resources.GetString("ttFanSel"));
			ttMain.SetToolTip(btnApply, Resources.GetString("ttApply"));
			ttMain.SetToolTip(btnRevert, Resources.GetString("ttRevert"));

			float scale = CurrentAutoScaleDimensions.Height / 72;

			tblCurve.ColumnStyles.Clear();
			tblCurve.ColumnCount = numFanSpds.Length + 2;
			tblCurve.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

			for (int i = 0; i < numFanSpds.Length; i++)
			{
				tblCurve.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / numFanSpds.Length));
				numFanSpds[i] = new NumericUpDown()
				{
					Dock = DockStyle.Fill,
					Enabled = false,
					Margin = new Padding(2),
					Tag = i,
				};
				numFanSpds[i].ValueChanged += FanSpdChanged;
				tblCurve.Controls.Add(numFanSpds[i], i + 1, 0);

				tbFanSpds[i] = new TrackBar()
				{
					Dock = DockStyle.Fill,
					Enabled = false,
					Orientation = Orientation.Vertical,
					Tag = i,
					TickFrequency = 5,
					TickStyle = TickStyle.Both,
				};
				tbFanSpds[i].ValueChanged += FanSpdScroll;
				tblCurve.Controls.Add(tbFanSpds[i], i + 1, 1);

				if (i != 0)
				{
					numUpTs[i - 1] = new NumericUpDown()
					{
						Dock = DockStyle.Fill,
						Enabled = false,
						Height = (int)(23 * scale),
						Margin = new Padding(2),
						Tag = i - 1,
					};
					numUpTs[i - 1].ValueChanged += UpTChanged;
					tblCurve.Controls.Add(numUpTs[i - 1], i + 1, 2);
				}
				else
				{
					tblCurve.Controls.Add(new Label
					{
						Dock = DockStyle.Fill,
						Margin = new Padding(4),
						Text = "Default",
						TextAlign = ContentAlignment.MiddleCenter,
					},
					i + 1, 2);
				}

				if (i != numFanSpds.Length - 1)
				{
					numDownTs[i] = new NumericUpDown()
					{
						Dock = DockStyle.Fill,
						Enabled = false,
						Height = (int)(23 * scale),
						Margin = new Padding(2),
						Tag = i,
					};
					numDownTs[i].ValueChanged += DownTChanged;
					tblCurve.Controls.Add(numDownTs[i], i + 1, 3);
				}
				else
				{
					tblCurve.Controls.Add(new Label
					{
						Dock = DockStyle.Fill,
						Margin = new Padding(4),
						Text = "Max",
						TextAlign = ContentAlignment.MiddleCenter,
					},
					i + 1, 3);
				}
			}
		}

		private void MainWindowLoad(object sender, EventArgs e)
		{
			try
			{
				IPCClient.ServerMessage += IPCMessageReceived;
				IPCClient.Start();
				AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
			}
			catch (Exception ex)
			{
				MessageBox.Show(string.Format(Resources.GetString("svcErrorConnect"), ex),
					"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				Application.Exit();
			}

			LoadConf(Path.Combine(DataPath, "CurrentConfig.xml"));
		}

		private void IPCMessageReceived(
			NamedPipeConnection<ServiceResponse, ServiceCommand> connection, ServiceResponse message)
		{
			string[] args = message.Value.Split(' ');
			if (args.Length == 1)
			{
				switch (message.Response)
				{
					case Response.Temp:
						if (int.TryParse(args[0], out int value))
							UpdateFanMon(value, 0);
						break;
					case Response.FanSpeed:
						if (int.TryParse(args[0], out value))
							UpdateFanMon(value, 1);
						break;
					case Response.FanRPM:
						if (int.TryParse(args[0], out value))
							UpdateFanMon(value, 2);
						break;
				}
			}
		}

		private void OnProcessExit(object sender, EventArgs e)
		{
			// Close the connection to the MSI Fan Control
			// Service before exiting the program:
			IPCClient.Stop();
		}

		private void UpdateFanMon(int value, int i)
		{
			switch (i)
			{
				case 0:
					lblTemp.Invoke(new Action(delegate
					{
						lblTemp.Text = $"Temp: {value}°C";
					}));
					break;
				case 1:
					lblFanSpd.Invoke(new Action(delegate
					{
						lblFanSpd.Text = $"Fan speed: {value}%";
					}));
					break;
				case 2:
					lblFanRPM.Invoke(new Action(delegate
					{
						lblFanRPM.Text = value == -1
							? "RPM: 0"
							: $"RPM: {value}";
					}));
					break;
			}
		}

		#region Events
		private void MainWindowFormClosing(object sender, FormClosingEventArgs e)
		{
			// Disable Full Blast if it was enabled while the program was running:
			if (chkFullBlast.Checked)
			{
				ServiceCommand command = new ServiceCommand(Command.FullBlast, "0");
				IPCClient.PushMessage(command);
			}
		}

		#region Tool Strip Menu Items
		private void tsiExitClick(object sender, EventArgs e) =>
			Close();

		private void tsiAboutClick(object sender, EventArgs e) =>
			MessageBox.Show(Resources.GetString("About"), "About",
				MessageBoxButtons.OK, MessageBoxIcon.Information);

		private void tsiSrcClick(object sender, EventArgs e) =>
			// TODO: add GitHub project link
			Process.Start("https://youtu.be/dQw4w9WgXcQ");
		#endregion

		#region Fan and Temp Threshold sliders
		private void FanSpdChanged(object sender, EventArgs e)
		{
			NumericUpDown nud = (NumericUpDown)sender;
			int i = (int)nud.Tag;
			tbFanSpds[i].Value = (int)numFanSpds[i].Value;

			Config.FanConfigs[cboFanSel.SelectedIndex]
				.FanCurveConfigs[cboProfSel.SelectedIndex]
				.TempThresholds[i].FanSpeed = (byte)numFanSpds[i].Value;
		}

		private void FanSpdScroll(object sender, EventArgs e)
		{
			TrackBar tb = (TrackBar)sender;
			int i = (int)tb.Tag;
			numFanSpds[i].Value = tbFanSpds[i].Value;

			Config.FanConfigs[cboFanSel.SelectedIndex]
				.FanCurveConfigs[cboProfSel.SelectedIndex]
				.TempThresholds[i].FanSpeed = (byte)numFanSpds[i].Value;
		}

		private void UpTChanged(object sender, EventArgs e)
		{
			NumericUpDown nud = (NumericUpDown)sender;
			int i = (int)nud.Tag;

			TempThreshold threshold = Config.FanConfigs[cboFanSel.SelectedIndex]
				.FanCurveConfigs[cboProfSel.SelectedIndex]
				.TempThresholds[i + 1];

			// Update associated down threshold slider
			numDownTs[i].Value += nud.Value - threshold.UpThreshold;

			threshold.UpThreshold = (byte)numUpTs[i].Value;
		}

		private void DownTChanged(object sender, EventArgs e)
		{
			NumericUpDown nud = (NumericUpDown)sender;
			int i = (int)nud.Tag;

			Config.FanConfigs[cboFanSel.SelectedIndex]
				.FanCurveConfigs[cboProfSel.SelectedIndex]
				.TempThresholds[i + 1].DownThreshold = (byte)numDownTs[i].Value;
		}
		#endregion

		private void ApplyClick(object sender, EventArgs e)
		{
			// Save the updated config
			Config.Save(Path.Combine(DataPath, "CurrentConfig.xml"));

			// Tell the service to reload and apply the updated config
			ServiceCommand command = new ServiceCommand(Command.ApplyConfig, null);
			IPCClient.PushMessage(command);
		}

		private void LoadConfClick(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog()
			{
				AddExtension = true,
				CheckFileExists = true,
				Filter = "MSI Fan Control config files|*.xml",
				Title = "Load config",
			};

			if (ofd.ShowDialog() == DialogResult.OK)
			{
				LoadConf(ofd.FileName);
			}
		}

		private void SaveConfClick(object sender, EventArgs e)
		{
			SaveFileDialog sfd = new SaveFileDialog()
			{
				AddExtension = true,
				Filter = "MSI Fan Control config files|*.xml",
				Title = "Save config",
			};

			if (sfd.ShowDialog() == DialogResult.OK)
			{
				Config.Save(sfd.FileName);
			}
		}

		#region Fan config and profiles
		private void LoadConf(string configPath)
		{
			lblStatus.Text = "Loading config, please wait...";

			try
			{
				Config = FanControlConfig.Load(configPath);
			}
			catch
			{
				lblStatus.Text = "Please load a config to start";
				return;
			}
			tsiSaveConf.Enabled = true;

			if (Config.FullBlastConfig is null)
			{
				ttMain.SetToolTip(chkFullBlast, Resources.GetString("ttNotSupported"));
				chkFullBlast.Enabled = false;
			}
			else
			{
				ttMain.SetToolTip(chkFullBlast, Resources.GetString("ttFullBlast"));
				chkFullBlast.Enabled = true;
			}

			if (Config.ChargeLimitConfig is null)
			{
				ttMain.SetToolTip(chkFullBlast, Resources.GetString("ttNotSupported"));
				numChgLim.Enabled = false;
			}
			else
			{
				ttMain.SetToolTip(numChgLim, Resources.GetString("ttChgLim"));
				ChargeLimitConfig cfg = Config.ChargeLimitConfig;
				numChgLim.Enabled = true;
				numChgLim.Value = cfg.Value;
				numChgLim.Maximum = Math.Abs(cfg.MaxValue - cfg.MinValue);
			}

			cboFanSel.Items.Clear();
			for (int i = 0; i < Config.FanConfigs.Length; i++)
				cboFanSel.Items.Add(Config.FanConfigs[i].Name);

			cboFanSel.Enabled = true;
			cboFanSel.SelectedIndex = 0;
			tsiECMon.Enabled = true;

			lblStatus.Text = "Ready";
		}

		private void FanSelIndexChanged(object sender, EventArgs e)
		{
			FanConfig config = Config.FanConfigs[cboFanSel.SelectedIndex];

			cboProfSel.Items.Clear();
			foreach (FanCurveConfig curve in config.FanCurveConfigs)
				cboProfSel.Items.Add(curve.Name);

			for (int i = 0; i < numFanSpds.Length; i++)
			{
				if (config.FanCurveRegs.Length >= i)
				{
					numFanSpds[i].Maximum = tbFanSpds[i].Maximum
						= Math.Abs(config.MaxSpeed - config.MinSpeed);
					numFanSpds[i].Enabled = tbFanSpds[i].Enabled = true;
				}
				else
				{
					numFanSpds[i].Enabled = tbFanSpds[i].Enabled = false;
				}
			}

			cboProfSel.Enabled = true;
			cboProfSel.SelectedIndex = config.CurveSel;

			if (tsiECMon.Checked)
			{
				tmrPoll.Stop();
				PollEC();
				tmrPoll.Start();
			}
		}

		private void ProfSelIndexChanged(object sender, EventArgs e)
		{
			FanConfig config = Config.FanConfigs[cboFanSel.SelectedIndex];
			FanCurveConfig curveConfig = config.FanCurveConfigs[cboProfSel.SelectedIndex];

			config.CurveSel = cboProfSel.SelectedIndex;
			ttMain.SetToolTip(cboProfSel, config.FanCurveConfigs[config.CurveSel].Description);

			int numTempThresholds = config.UpThresholdRegs.Length;

			// Fan curve
			for (int i = 0; i < numFanSpds.Length; i++)
			{
				if (numTempThresholds >= i)
				{
					numFanSpds[i].Value = tbFanSpds[i].Value
						= curveConfig.TempThresholds[i].FanSpeed;
				}
			}

			// Temp thresholds
			for (int i = 0; i < numUpTs.Length; i++)
			{
				if (numTempThresholds >= i)
				{
					TempThreshold t = curveConfig.TempThresholds[i + 1];
					numUpTs[i].Value = t.UpThreshold;
					numDownTs[i].Value = t.DownThreshold;

					numUpTs[i].Enabled = numDownTs[i].Enabled = true;
				}
				else
				{
					numUpTs[i].Enabled = numDownTs[i].Enabled = false;
				}
			}
			btnApply.Enabled = true;
		}
		#endregion

		private void FullBlastToggled(object sender, EventArgs e)
		{
			ServiceCommand command = new ServiceCommand(Command.FullBlast, chkFullBlast.Checked ? "1" : "0");
			IPCClient.PushMessage(command);
		}

		private void ChargeLimChanged(object sender, EventArgs e)
		{
			Config.ChargeLimitConfig.Value = (byte)numChgLim.Value;
		}

		private void tmrPollTick(object sender, EventArgs e)
		{
			PollEC();
		}

		private void PollEC()
		{
			IPCClient.PushMessage(new ServiceCommand(Command.GetTemp, cboFanSel.SelectedIndex.ToString()));
			IPCClient.PushMessage(new ServiceCommand(Command.GetFanSpeed, cboFanSel.SelectedIndex.ToString()));
			IPCClient.PushMessage(new ServiceCommand(Command.GetFanRPM, cboFanSel.SelectedIndex.ToString()));
		}

		private void tsiECMonClick(object sender, EventArgs e)
		{
			if (tsiECMon.Checked)
			{
				tmrPoll.Start();
				PollEC();
				lblFanSpd.Visible = true;
				lblFanRPM.Visible = true;
				lblTemp.Visible = true;
			}
			else
			{
				tmrPoll.Stop();
				lblFanSpd.Visible = false;
				lblFanRPM.Visible = false;
				lblTemp.Visible = false;
			}
		}
		#endregion

		private void tsiUninstallClick(object sender, EventArgs e)
		{
			MessageBox.Show("TODO");
			return;

			if (MessageBox.Show(
				"This will uninstall the MSI Fan Control service from your computer.\n\n" +
				"Only proceed if you would like to delete MSI Fan Control" +
				"from your computer.\n\n" +
				"MSI Fan Control will close once the uninstall is complete.\n\n" +
				"Proceed?", "Uninstall Service",
				MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
			{
				bool delData = MessageBox.Show(
					"Also delete the MSI Fan Control data directory\n" +
					$"(located at {DataPath})?\n\n" +
					"This directory includes service logs, program-specific " +
					"configuration, and a copy of the MSI Fan Control config that gets " +
					"applied automatically by the service.\n\n" +
					"WARNING:\n" +
					"Make sure you saved your currently applied fan config elsewhere!",
					"Delete configuration?",
					MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

				if (delData)
				{
					Directory.Delete(DataPath, true);
				}

				IPCClient.Stop();
				
				// TODO: actually uninstall the MSI Fan Control service

				Application.Exit();
			}
		}
	}
}
