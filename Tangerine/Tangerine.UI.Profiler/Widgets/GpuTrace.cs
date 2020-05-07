using System;
using System.Collections;
using System.Collections.Generic;
using Lime;
using DrawCallInfo = Lime.Graphics.Platform.GpuUsage;
using Tangerine.UI.Timeline;

namespace Tangerine.UI
{
	internal class GpuTrace : Widget
	{
		private const float FontHeight = 20;

		private static readonly Color4 textColor = ColorTheme.Current.Profiler.TimelineRulerAndText;

		public readonly GpuUsageTimeline Timeline;
		private readonly ThemedSimpleText ownersLabel;
		private readonly CustomDropDownList ownersList;
		private readonly ThemedSimpleText materialLabel;
		private readonly ThemedSimpleText passIndexLabel;
		private readonly ThemedSimpleText renderTimeLabel;
		private readonly ThemedSimpleText verticesCountLabel;
		private readonly ThemedSimpleText trianglesCountLabel;

		public GpuTrace()
		{
			Id = "Profiler GpuTrace";
			Layout = new VBoxLayout();
			Anchors = Anchors.LeftRight;

			Timeline = new GpuUsageTimeline {
				GpuUsageSelected = OnDrawCallSelected
			};

			ThemedSimpleText CreateText(string text, Thickness padding) =>
				new ThemedSimpleText {
					Text       = text,
					Color      = textColor,
					FontHeight = FontHeight,
					Padding    = padding
				};

			var ownersListSize = new Vector2(160, 22);
			var groupOffset = new Thickness(0, 16, 0);
			var itemsOffset = new Thickness(6, 0);

			AddNode(new Widget {
				Anchors = Anchors.LeftRight,
				MinMaxHeight = 24,
				Presenter = new WidgetFlatFillPresenter(
					ColorTheme.Current.Profiler.TimelineHeaderBackground),
				Nodes = {
					new Widget {
						Layout = new HBoxLayout(),
						Nodes = {
							CreateText("Frame", new Thickness(16, 6, 0)),
							CreateText("Draw Call", new Thickness(32, 6, 0)),
							(ownersLabel = CreateText("Owners", new Thickness(16, 6, 0))),
							new Widget { // ownersList background
								Presenter = new WidgetFlatFillPresenter(
									ColorTheme.Current.Profiler.TimelineBackground),
								MinMaxSize = ownersListSize,
								Padding = new Thickness(0,0,2,0),
								Nodes = { (ownersList = new CustomDropDownList {
									MinMaxSize = ownersListSize,
									Size = ownersListSize,
									Padding = new Thickness(6, 16, 0, 0),
									Color = ColorTheme.Current.Profiler.TimelineRulerAndText
								}) }
							},
							CreateText("Material", new Thickness(16, 0, 0)),
							(materialLabel = CreateText("", itemsOffset)),
							CreateText("Pass", itemsOffset),
							(passIndexLabel = CreateText("", groupOffset)),
							CreateText("Render time", itemsOffset),
							(renderTimeLabel = CreateText("", groupOffset)),
							CreateText("Vertices", itemsOffset),
							(verticesCountLabel = CreateText("", groupOffset)),
							CreateText("Triangles", itemsOffset),
							(trianglesCountLabel = CreateText("", groupOffset)),
						}
					},
				}
			});
			AddNode(Timeline);
		}

		private void OnDrawCallSelected(DrawCallInfo drawCall)
		{
			string ownersLabelText = "Owners";
			ownersList.Items.Clear();
			switch (drawCall.GpuCallInfo.Owners) {
				case null: ownersList.Items.Add(new DropDownList.Item("Null")); break;
				case string id: ownersList.Items.Add(new DropDownList.Item(id)); break;
				case Node node: ownersList.Items.Add(new DropDownList.Item(node.Id ?? "Node id unset")); break;
				case IList list:
					int ownersCount = 0;
					ownersLabelText = "Owners batch ";
					var dictionary = new Dictionary<string, int>();
					foreach (var item in list) {
						ownersCount++;
						string owner = GetOwnerId(item);
						if (dictionary.ContainsKey(owner)) {
							dictionary[owner]++;
						} else {
							dictionary.Add(owner, 1);
						}
					}
					foreach (var v in dictionary) {
						ownersList.Items.Add(new DropDownList.Item(v.Key + " " + v.Value));
					}
					ownersLabelText += ownersCount;
					break;
				default: throw new InvalidOperationException();
			}
			ownersLabel.Text = ownersLabelText;
			ownersList.Index = 0;
			var pi = drawCall.GpuCallInfo;
			if (pi.Material is string materialName) {
				materialLabel.Text = materialName;
			} else {
				materialLabel.Text = pi.Material?.GetType().Name ?? "...";
			}
			passIndexLabel.Text = drawCall.RenderPassIndex < 0 ? "..." : drawCall.RenderPassIndex.ToString();
			renderTimeLabel.Text = string.Format("{0} ms", (drawCall.Finish - drawCall.Start) / 1000f);
			verticesCountLabel.Text = drawCall.VerticesCount.ToString();
			trianglesCountLabel.Text = drawCall.TrianglesCount.ToString();
		}

		private string GetOwnerId(object owner)
		{
			switch (owner) {
				case null: return "Null";
				case Node node: return node.Id ?? "Node id unset";
				case string str: return str;
				default: throw new InvalidOperationException();
			}
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
