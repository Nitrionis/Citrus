using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Lime;
using Lime.Profilers;
using Lime.Profilers.Contexts;

namespace Tangerine.UI
{
	internal class MainControlPanel : Widget
	{
		private ThemedDropDownList profilingMode;
		private ThemedButton pauseСontinueButton;
		private ThemedButton ipPortLabel;
		private ThemedButton sceneFilter;
		private ThemedButton selectButton;
		private ThemedEditBox nodeIdFilter;

		private bool isProfilingEnabled;
		private bool isSceneFilterEnabled;
		private volatile bool isConnectionClosed;

		public Action<bool> SceneFilteringChanged;
		public Action<Regex> NodeFilteringChanged;

		public MainControlPanel(Widget settingsWidget)
		{
			Layout = new HBoxLayout();
			Padding = new Thickness(8, 0, 2, 0);
			AddNode(new ThemedButton("Settings") {
				Clicked = () => { settingsWidget.Visible = !settingsWidget.Visible; }
			});
			profilingMode = new ThemedDropDownList() {
				Padding = new Thickness(0, 4),
				MinMaxWidth = 128
			};
			profilingMode.Items.Add(new ThemedDropDownList.Item("This device mode"));
			profilingMode.Items.Add(new ThemedDropDownList.Item("Data receiver mode"));
			profilingMode.Items.Add(new ThemedDropDownList.Item("Data source mode"));
			profilingMode.Index = 0;
			profilingMode.Changed += ProfilingModeChanged;
			AddNode(profilingMode);
			ipPortLabel = new ThemedButton("ip:port") {
				Enabled = false,
				Visible = false,
				MinMaxWidth = 128
			};
			AddNode(ipPortLabel);
			pauseСontinueButton = new ThemedButton("Pause|Сontinue") {
				Clicked = () => { LimeProfiler.IsProfilingEnabled = !LimeProfiler.IsProfilingEnabled; }
			};
			AddNode(pauseСontinueButton);
			sceneFilter = new ThemedButton("Select scene only") {
				MinMaxWidth = 128
			};
			sceneFilter.Clicked += () => {
				isSceneFilterEnabled = !isSceneFilterEnabled;
				SceneFilteringChanged?.Invoke(isSceneFilterEnabled);
				UpdateSceneFilterLabel();
			};
			AddNode(sceneFilter);
			nodeIdFilter = new ThemedEditBox() {
				MinMaxWidth = 200
			};
			nodeIdFilter.Submitted += (value) => {
				NodeFilteringChanged?.Invoke(
					string.IsNullOrEmpty(value) ? null : new Regex(value));
			};
			selectButton = new ThemedButton("Select Node by id") {
				MinMaxWidth = 128
			};
			selectButton.Clicked += () => {
				NodeFilteringChanged?.Invoke(
					string.IsNullOrEmpty(nodeIdFilter.Text) ? null : new Regex(nodeIdFilter.Text));
			};
			AddNode(selectButton);
			AddNode(nodeIdFilter);
			Tasks.Add(StateUpdateTask);
		}

		public void ResetFilters()
		{
			isSceneFilterEnabled = false;
			UpdateSceneFilterLabel();
		}

		private void UpdateSceneFilterLabel() =>
			sceneFilter.Text = isSceneFilterEnabled ? "Select not only scene" : "Select scene only";

		private IEnumerator<object> StateUpdateTask()
		{
			while (true) {
				if (isProfilingEnabled != LimeProfiler.IsProfilingEnabled) {
					isProfilingEnabled = LimeProfiler.IsProfilingEnabled;
					pauseСontinueButton.Text = isProfilingEnabled ? "Pause" : "Сontinue";
				}
				if (isConnectionClosed) {
					isConnectionClosed = false;
					SetLocalContext();
				}
				yield return null;
			}
		}

		private void ProfilingModeChanged(CommonDropDownList.ChangedEventArgs args)
		{
			if (args.ChangedByUser) {
				if (args.Index == 0) {
					SetLocalContext();
				} else {
					bool isSourceMode = args.Index == 2;
					var endPointInfo = new IpDialog(isPortRequired: isSourceMode).Show();
					if (endPointInfo.IP != null) {
						var ipEndPoint = new IPEndPoint(endPointInfo.IP, endPointInfo.Port);
						var networkContext = isSourceMode ?
							(NetworkContext)new ClientContext() :
							(NetworkContext)new ServerContext();
						networkContext.Closed += () => isConnectionClosed = true;
						if (networkContext.TryLaunch(ipEndPoint)) {
							LimeProfiler.SetContext(networkContext);
							if (!isSourceMode) {
								int port = ((ServerContext)networkContext).LocalEndpoint.Port;
								ipPortLabel.Text = endPointInfo.IP.ToString() + ":" + port;
								ipPortLabel.Visible = true;
							}
						}
					}
				}
			}
		}

		private void SetLocalContext()
		{
			LimeProfiler.SetContext(new LocalContext());
			ipPortLabel.Visible = false;
			profilingMode.Index = 0;
		}
	}
}
