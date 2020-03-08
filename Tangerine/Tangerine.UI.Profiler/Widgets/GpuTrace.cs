using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lime;
using DrawCallInfo = Lime.Graphics.Platform.ProfilingResult;
using Tangerine.UI.Timeline;

namespace Tangerine.UI
{
	internal class GpuTrace : Widget
	{
		private const float FontHeight = 20;

		private static readonly Color4 textColor = new Color4(204, 204, 204);

		public readonly DrawCallsTimeline Timeline;

		private readonly ColorCheckBox sceneFilter;
		private readonly ThemedEditBox nodeFilter;
		private readonly ThemedSimpleText ownersLabel;
		private readonly ThemedSimpleText materialLabel;
		private readonly ThemedSimpleText passIndexLabel;
		private readonly ThemedSimpleText renderTimeLabel;
		private readonly CustomDropDownList ownersList;

		public Action NodeFilteringChanged;

		public GpuTrace()
		{
			Presenter = new WidgetFlatFillPresenter(new Color4(32, 33, 35));
			Layout = new VBoxLayout();

			Timeline = new DrawCallsTimeline {
				DrawCallSelected = DrawCallSelected
			};

			ThemedSimpleText CreateText(string text, Thickness padding) =>
				new ThemedSimpleText {
					Text       = text,
					Color      = textColor,
					FontHeight = FontHeight,
					Padding    = padding
				};

			var ownersListSize = new Vector2(160, 22);

			AddNode(new Widget {
				Layout = new HBoxLayout(),
				Anchors = Anchors.LeftRight,
				MinMaxHeight = 32,
				Nodes = {
					CreateText("Scene only", new Thickness(6, 0)),
					(sceneFilter = new ColorCheckBox(Color4.White) {
						Padding = new Thickness(0, 16, 4, 4),
						Checked = false
					}),
					CreateText("Node id", new Thickness(0, 6, 0)),
					(nodeFilter = new ThemedEditBox {
						MinMaxWidth = 160
					}),
					(ownersLabel = CreateText("Owners", new Thickness(16, 6, 0))),
					new Widget { // ownersList background
						Presenter = new WidgetFlatFillPresenter(new Color4(63, 63, 63)),
						MinMaxSize = ownersListSize,
						Padding = new Thickness(1),
						Nodes = { (ownersList = new CustomDropDownList {
							MinMaxSize = ownersListSize,
							Size = ownersListSize,
							Padding = new Thickness(6, 16, 0, 0),
							Color = Color4.White
						}) }
					},
					CreateText("Material", new Thickness(16, 0, 0)),
					(materialLabel = CreateText("", new Thickness(6, 0))),
					CreateText("Pass", new Thickness(6, 0)),
					(passIndexLabel = CreateText("", new Thickness(0, 16, 0))),
					CreateText("Render time", new Thickness(6, 0)),
					(renderTimeLabel = CreateText("", new Thickness(0, 0))),
				}
			});
			AddNode(Timeline);

			nodeFilter.TextWidget.FontHeight = FontHeight;

			nodeFilter.Submitted += (text) => {
				Timeline.RegexNodeFilter = string.IsNullOrEmpty(text) ? null : new Regex(text);
				NodeFilteringChanged?.Invoke();
			};
			sceneFilter.Changed += (e) => {
				Timeline.IsSceneOnly = e.Value;
				NodeFilteringChanged?.Invoke();
			};
		}

		private void DrawCallSelected(DrawCallInfo drawCall)
		{
			string ownersText = "Owners";
			ownersList.Items.Clear();
			var pi = drawCall.ProfilingInfo;
			if (pi.Owners != null) {
				if (pi.Owners is List<object> owners) {
					ownersText = "Owners batch " + owners.Count;
					var dictionary = new Dictionary<string, int>();
					foreach (var item in owners) {
						string owner = GetOwnerName(item);
						if (dictionary.ContainsKey(owner)) {
							dictionary[owner]++;
						} else {
							dictionary.Add(owner, 1);
						}
					}
					foreach (var v in dictionary) {
						ownersList.Items.Add(new DropDownList.Item(v.Key + " " + v.Value));
					}
				} else {
					ownersList.Items.Add(new DropDownList.Item(GetOwnerName(pi.Owners)));
				}
			}
			else {
				ownersList.Items.Add(new DropDownList.Item("Null"));
			}
			materialLabel.Text = pi.Material?.GetType().Name ?? "...";
			passIndexLabel.Text = drawCall.RenderPassIndex < 0 ? "..." : drawCall.RenderPassIndex.ToString();
			ownersLabel.Text = ownersText;
			renderTimeLabel.Text = string.Format("{0} ms", (drawCall.Finish - drawCall.Start) / 1000f);
			ownersList.Index = 0;
		}

		private string GetOwnerName(object owner)
		{
			string name;
			if (owner == null) {
				name = "Null";
			} else if (owner is Node node) {
				name = node.Id ?? "Node id unset";
			} else if (owner is string str) {
				name = str;
			} else {
				throw new InvalidOperationException();
			}
			return name;
		}

		private class CustomDropDownList : DropDownList
		{
			public CustomDropDownList()
			{
				var text = new ThemedSimpleText {
					Id = "TextWidget",
					VAlignment = VAlignment.Center,
					FontHeight = 20,
					Color = textColor,
				};
				AddNode(text);
				text.ExpandToContainerWithAnchors();
				text.X += 4;
			}
		}
	}
}
