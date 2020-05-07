using System;
using System.Collections;
using System.Collections.Generic;
using Lime;
using Lime.Graphics.Platform;
using Tangerine.UI.Timeline;

namespace Tangerine.UI
{
	internal class CpuTrace : Widget
	{
		private const float FontHeight = 20;

		private static readonly Color4 textColor = ColorTheme.Current.Profiler.TimelineRulerAndText;

		public readonly CpuUsageTimeline Timeline;
		private readonly ThemedSimpleText usageReasonLabel;
		private readonly ThemedSimpleText updateTimeLabel;
		private readonly ThemedSimpleText ownersLabel;
		private readonly CustomDropDownList ownersList;

		public CpuTrace()
		{
			Id = "Profiler CpuTrace";
			Layout = new VBoxLayout();
			Anchors = Anchors.LeftRight;

			Timeline = new CpuUsageTimeline {
				CpuUsageSelected = OnCpuUsageSelected
			};

			ThemedSimpleText CreateText(string text, Thickness padding) =>
				new ThemedSimpleText {
					Text = text,
					Color = textColor,
					FontHeight = FontHeight,
					Padding = padding
				};

			var groupOffset = new Thickness(0, 16, 0);
			var itemsOffset = new Thickness(6, 0);
			var ownersListSize = new Vector2(160, 22);

			AddNode(new Widget {
				Anchors = Anchors.LeftRight,
				MinMaxHeight = 24,
				Presenter = new WidgetFlatFillPresenter(
					ColorTheme.Current.Profiler.TimelineHeaderBackground),
				Nodes = {
					new Widget {
						Layout = new HBoxLayout(),
						Nodes = {
							CreateText("Update", new Thickness(16, 6, 0)),
							CreateText("Reason", new Thickness(32, 6, 0)),
							(usageReasonLabel = CreateText("", groupOffset)),
							CreateText("Time", itemsOffset),
							(updateTimeLabel = CreateText("", groupOffset)),
							(ownersLabel = CreateText("Owner", itemsOffset)),
							new Widget { // ownersList background
								Presenter = new WidgetFlatFillPresenter(
									ColorTheme.Current.Profiler.TimelineBackground),
								MinMaxSize = ownersListSize,
								Padding = new Thickness(0, 0, 2, 0),
								Nodes = { (ownersList = new CustomDropDownList {
									MinMaxSize = ownersListSize,
									Size = ownersListSize,
									Padding = groupOffset,
									Color = ColorTheme.Current.Profiler.TimelineRulerAndText
								}) }
							},
						}
					},
				}
			});
			AddNode(Timeline);
		}

		private void OnCpuUsageSelected(CpuUsage usage)
		{
			string ownersLabelText = "Owners";
			ownersList.Items.Clear();
			switch (usage.Owners) {
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
			usageReasonLabel.Text = usage.Reasons.ToString();
			updateTimeLabel.Text = string.Format("{0} ms", (usage.Finish - usage.Start) / 1000f);
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
