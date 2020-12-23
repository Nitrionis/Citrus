#if PROFILER

using System;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Graphics;

namespace Tangerine.UI
{
	internal struct ChartVisibilityControllers
	{
		public Action<bool> MainThreadChartsSetVisible;
		public Action<bool> RenderThreadChartsSetVisible;
		public Action<bool> GpuChartsSetVisible;
		public Action<bool> SceneGeometryChartsSetVisible;
		public Action<bool> FullGeometryChartsSetVisible;
		public Action<bool> UpdateGcChartsSetVisible;
		public Action<bool> RenderGcChartsSetVisible;
	}

	internal struct TimelineVisibilityControllers
	{
		public Action<bool> MainThreadTimelineSetVisible;
		public Action<bool> RenderThreadTimelineSetVisible;
		public Action<bool> GpuTimelineSetVisible;
	}
	
	internal class OptionsPanel : Widget
	{
		private ChartVisibilityControllers chartVisibilityControllers;
		private TimelineVisibilityControllers timelineVisibilityControllers;
		
		public OptionsPanel(
			ChartVisibilityControllers chartVisibilityControllers, 
			TimelineVisibilityControllers timelineVisibilityControllers)
		{
			this.chartVisibilityControllers = chartVisibilityControllers;
			this.timelineVisibilityControllers = timelineVisibilityControllers;
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
				checkBox = new ThemedCheckBox { Checked = true };
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
							CreateCheckBox("Update Thread GC", out var updateGcChartsCheckBox),
							CreateCheckBox("Render Thread GC", out var renderGcChartsCheckBox),
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
			mainTheadChartsCheckBox.Changed += args =>
				this.chartVisibilityControllers.MainThreadChartsSetVisible(args.Value);
			renderTheadChartsCheckBox.Changed += args =>
				this.chartVisibilityControllers.RenderThreadChartsSetVisible(args.Value);
			gpuChartsCheckBox.Changed += args =>
				this.chartVisibilityControllers.GpuChartsSetVisible(args.Value);
			sceneGeometryChartsCheckBox.Changed += args =>
				this.chartVisibilityControllers.SceneGeometryChartsSetVisible(args.Value);
			fullGeometryChartsCheckBox.Changed += args =>
				this.chartVisibilityControllers.FullGeometryChartsSetVisible(args.Value);
			updateGcChartsCheckBox.Changed += args =>
				this.chartVisibilityControllers.UpdateGcChartsSetVisible(args.Value);
			renderGcChartsCheckBox.Changed += args =>
				this.chartVisibilityControllers.RenderGcChartsSetVisible(args.Value);
			mainTheadTimelineCheckBox.Changed += args =>
				this.timelineVisibilityControllers.MainThreadTimelineSetVisible(args.Value);
			renderTheadTimelineCheckBox.Changed += args =>
				this.timelineVisibilityControllers.RenderThreadTimelineSetVisible(args.Value);
			gpuTimelineCheckBox.Changed += args =>
				this.timelineVisibilityControllers.GpuTimelineSetVisible(args.Value);
		}
	}
}

#endif // PROFILER