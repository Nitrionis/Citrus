#if PROFILER

using Lime;
using Lime.Profiler;
using Lime.Profiler.Graphics;

namespace Tangerine.UI
{
	internal class OptionsPanel : Widget
	{
		public OptionsPanel()
		{
			Presenter = new WidgetFlatFillPresenter(
				ColorTheme.Current.IsDark
					? Theme.Colors.GrayBackground.Lighten(0.06f)
					: Theme.Colors.GrayBackground.Darken(0.06f)
			);
			Layout = new HBoxLayout { Spacing = 16 };
			Padding = new Thickness(4, 4, 0, 4);
			Anchors = Anchors.LeftRight;
			MaxHeight = 128;
			Widget CreateCheckBox(string name, out ThemedCheckBox checkBox) {
				checkBox = new ThemedCheckBox();
				return new Widget {
					Layout = new HBoxLayout { Spacing = 4 },
					Nodes = {
						checkBox,
						new ThemedSimpleText(name)
					}
				};
			}
			Nodes.Add(new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					new ThemedSimpleText("Charts") {
						Padding = new Thickness(4)
					},
					new Widget {
						Padding = new Thickness(4),
						Layout = new VBoxLayout { Spacing = 4 },
						Nodes = {
							CreateCheckBox("CPU Main Thread", out var mainTheadChartsCheckBox),
							CreateCheckBox("CPU Render Thread", out var renderTheadChartsCheckBox),
							CreateCheckBox("GPU (In developing)", out var gpuChartsCheckBox),
							CreateCheckBox("Scene Geometry", out var sceneGeometryChartsCheckBox),
							CreateCheckBox("Full Geometry", out var fullGeometryChartsCheckBox),
						}
					}
				}
			});
			Nodes.Add(new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					new ThemedSimpleText("Timeline") {
						Padding = new Thickness(4)
					},
					new Widget {
						Padding = new Thickness(4),
						Layout = new VBoxLayout { Spacing = 4 },
						Nodes = {
							CreateCheckBox("CPU Main Thread", out var mainTheadTimelineCheckBox),
							CreateCheckBox("CPU Render Thread", out var renderTheadTimelineCheckBox),
							CreateCheckBox("GPU (In developing)", out var gpuTimelineCheckBox),
						}
					}
				}
			});
			Nodes.Add(new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					new ThemedSimpleText("Options") {
						Padding = new Thickness(4)
					},
					new Widget {
						Padding = new Thickness(4),
						Layout = new VBoxLayout { Spacing = 4 },
						Nodes = {
							CreateCheckBox("GPU Profiling (In developing)", out var gpuProfilingCheckBox),
							CreateCheckBox("Batch Break Reasons", out var batchBreakReasonsCheckBox),
							CreateCheckBox("Overdraw Mode", out var overdrawModeCheckBox),
						}
					}
				}
			});
			Nodes.Add(Spacer.HFill());
			Updating += delta => {
				batchBreakReasonsCheckBox.Checked = ProfilerTerminal.BatchBreakReasonsRequired;
				overdrawModeCheckBox.Checked = Overdraw.Enabled;
			};
			batchBreakReasonsCheckBox.Clicked += () =>
				ProfilerTerminal.BatchBreakReasonsRequired = !ProfilerTerminal.BatchBreakReasonsRequired;
			overdrawModeCheckBox.Clicked += () =>
				ProfilerTerminal.OverdrawEnabled = !ProfilerTerminal.OverdrawEnabled;
		}
	}
}

#endif // PROFILER