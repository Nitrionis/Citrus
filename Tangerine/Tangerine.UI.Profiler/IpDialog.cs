using System.Net;
using Lime;

namespace Tangerine.UI
{
	internal class IpDialog
	{
		private readonly Window window;

		public struct Result
		{
			public IPAddress IP;
			public int Port;
		}

		private Result result = new Result { IP = null, Port = 0 };

		public IpDialog(bool isPortRequired = false)
		{
			ThemedButton okButton;
			var dropDownList = InitializeIpList();
			var editBox = new ThemedEditBox {
				Visible = isPortRequired
			};
			var statusLabel = new ThemedSimpleText {
				Text = !isPortRequired ? "Data receiver mode parameters" : "Data source mode parameters"
			};
			window = new Window(new WindowOptions {
				Title = "Choose IP",
				Visible = false,
				Style = WindowStyle.Dialog,
			});
			new ThemedInvalidableWindowWidget(window) {
				LayoutBasedWindowSize = true,
				Padding = new Thickness(8),
				Layout = new VBoxLayout { Spacing = 16 },
				Nodes = {
					statusLabel,
					new Widget {
						Layout = new HBoxLayout { Spacing = 8 },
						Nodes = {
							new Widget {
								Layout = new VBoxLayout(),
								Nodes = {
									new ThemedSimpleText("ip"),
									new ThemedSimpleText("port") { Visible = isPortRequired }
								}
							},
							new Widget {
								Layout = new VBoxLayout(),
								Nodes = {
									dropDownList,
									editBox
								}
							},
						}
					},
					(okButton = new ThemedButton("Ok"))
				}
			};
			okButton.Clicked += () => {
				result = new Result();
				bool isIpValid = IPAddress.TryParse(dropDownList.Text, out result.IP);
				bool isPortValid = int.TryParse(editBox.Text, out result.Port);
				if (isIpValid && (!isPortRequired || isPortValid)) {
					window.Close();
				} else {
					statusLabel.Text =
						"wrong " +
						(!isIpValid ? "ip " : "") +
						(!isIpValid && !isPortValid ? "and " : "") +
						(!isPortValid ? "port" : "");
				}
			};
		}

		private ThemedDropDownList InitializeIpList()
		{
			var ipPresets = new ThemedDropDownList() {
				MinMaxWidth = 160
			};
			int index = 0;
			int selectedItemIndex = 0;
			int minIpLength = int.MaxValue;
			foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList) {
				string ipStr = ip.ToString();
				if (minIpLength > ipStr.Length) {
					minIpLength = ipStr.Length;
					selectedItemIndex = index;
				}
				ipPresets.Items.Add(new CommonDropDownList.Item(ipStr));
				index += 1;
			}
			ipPresets.Index = selectedItemIndex;
			return ipPresets;
		}

		public Result Show()
		{
			window.ShowModal();
			return result;
		}
	}
}