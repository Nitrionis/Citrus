using System.Collections.Generic;
using System.Net;
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

		private bool isProfilingEnabled;

		public MainControlPanel(Widget settingsWidget)
		{
			Layout = new HBoxLayout();
			Padding = new Thickness(8, 0, 2, 0);
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
			AddNode(new ThemedButton("Settings") {
				Clicked = () => { settingsWidget.Visible = !settingsWidget.Visible; }
			});
			pauseСontinueButton = new ThemedButton("Pause|Сontinue") {
				Clicked = () => { LimeProfiler.IsProfilingEnabled = !LimeProfiler.IsProfilingEnabled; }
			};
			AddNode(pauseСontinueButton);
			ipPortLabel = new ThemedButton("ip:port") {
				Enabled = false,
				Visible = false
			};
			AddNode(ipPortLabel);
			Tasks.Add(StateUpdateTask);
		}

		private IEnumerator<object> StateUpdateTask()
		{
			while (true) {
				if (isProfilingEnabled != LimeProfiler.IsProfilingEnabled) {
					isProfilingEnabled = LimeProfiler.IsProfilingEnabled;
					pauseСontinueButton.Text = isProfilingEnabled ? "Pause" : "Сontinue";
				}
				yield return null;
			}
		}

		private void ProfilingModeChanged(CommonDropDownList.ChangedEventArgs args)
		{
			if (args.ChangedByUser) {
				if (args.Index == 0) {
					LimeProfiler.SetContext(new LocalContext());
					ipPortLabel.Visible = false;
				} else {
					bool isSourceMode = args.Index == 2;
					var endPointInfo = new IpDialog(isPortRequired: isSourceMode).Show();
					var ipEndPoint = new IPEndPoint(endPointInfo.IP, endPointInfo.Port);
					NetworkContext networkContext = isSourceMode ?
						(NetworkContext)new ClientContext() :
						(NetworkContext)new ServerContext();
					LimeProfiler.SetContext(networkContext);
					if (!networkContext.TryLaunch(ipEndPoint)) {
						LimeProfiler.SetContext(new LocalContext());
					}
					ipPortLabel.Visible = !isSourceMode;
				}
			}
		}
	}
}
