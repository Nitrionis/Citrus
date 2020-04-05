using Lime;
using Lime.Profilers;
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
		private readonly ThemedSimpleText ownerLabel;

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
							CreateText("Owner", itemsOffset),
							(ownerLabel = CreateText("", groupOffset)),
						}
					},
				}
			});
			AddNode(Timeline);
		}

		private void OnCpuUsageSelected(CpuUsage usage)
		{
			usageReasonLabel.Text = usage.Reason.ToString();
			updateTimeLabel.Text = string.Format("{0} ms", (usage.Finish - usage.Start) / 1000f);
			if (usage.Owner is Node node) {
				ownerLabel.Text = node.Id ?? "Node id unset";
			} else if (usage.Owner is string id) {
				ownerLabel.Text = id;
			} else {
				ownerLabel.Text = "Null";
			}
		}
	}
}
