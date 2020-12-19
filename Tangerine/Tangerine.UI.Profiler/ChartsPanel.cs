#if PROFILER

using System;
using System.Threading;
using Lime;
using Lime.Profiler;
using Tangerine.UI.Charts;

namespace Tangerine.UI
{
	using LegendItemDescription = ChartsLegend.ItemDescription;
	
	internal class ChartsPanel : Widget
	{
		private int[] previousUpdateGC = new int[3];
		private int[] previousRenderGC = new int[3];
		
		public ChartsPanel(Func<Image> SearchIconBuilder, out ChartVisibilityControllers visibilityControllers)
		{
			Layout = new VBoxLayout();
			Anchors = Anchors.LeftRight;
			FixedHorizontalSpacingCharts.Parameters GetParameters(int chartsCount) => 
				new FixedHorizontalSpacingCharts.Parameters {
					ControlPointsCount = 160,
					ChartsCount = chartsCount,
					ControlPointsSpacing = 5
				};
			Widget CreateLegendWrapper(string name, ChartsLegend chartsLegend) => new Widget {
				Layout = new VBoxLayout(),
				Padding = new Thickness(0, 8, 4, 4),
				Nodes = {
					new ThemedSimpleText(name),
					chartsLegend,
				}
			};
			LegendItemDescription CreateFloatLegendItem(string name) =>
				new LegendItemDescription {
					Label = name,
					ValueFormat = "{0:0.##}"
				};
			LegendItemDescription CreateIntLegendItem(string name) =>
				new LegendItemDescription {
					Label = name,
					ValueFormat = "{0:0.}"
				};
			ChartsInfo<LineCharts> CreateLineCharts(string name, ChartsLegend.ItemDescription[] items) {
				var chartsGroup = new LineCharts(GetParameters(items.Length));
				var groupList = new [] { chartsGroup };
				var chartsLegend = new ChartsLegend(groupList, items);
				return new ChartsInfo<LineCharts> {
					ChartsGroup = chartsGroup,
					ChartsLegend = chartsLegend,
					ChartsContainer = new ChartsContainer(groupList) {
						BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
					},
					LegendWrapper = CreateLegendWrapper(name, chartsLegend)
				};
			}
			ChartsInfo<StackedAreaCharts> CreateAreaCharts(string name, ChartsLegend.ItemDescription[] items) {
				var chartsGroup = new StackedAreaCharts(GetParameters(items.Length));
				var groupList = new [] { chartsGroup };
				var chartsLegend = new ChartsLegend(groupList, items);
				return new ChartsInfo<StackedAreaCharts> {
					ChartsGroup = chartsGroup,
					ChartsLegend = chartsLegend,
					ChartsContainer = new ChartsContainer(groupList) {
						BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
					},
					LegendWrapper = CreateLegendWrapper(name, chartsLegend)
				};
			}
			var updateCharts = CreateAreaCharts("Main Thread", new[] {
					CreateFloatLegendItem("Update"),
					CreateFloatLegendItem("Selected")
				});
			var renderCharts = CreateAreaCharts("Render Thread",new[] {
					CreateFloatLegendItem("Render"),
					CreateFloatLegendItem("Selected")
				});
			var gpuCharts = CreateAreaCharts("GPU",new[] {
					CreateFloatLegendItem("Drawing"),
					CreateFloatLegendItem("Selected")
				});
			var geometryChartsDescriptions = new [] {
				CreateIntLegendItem("Saved By Batching"),
				CreateIntLegendItem("Draw Calls"),
				CreateIntLegendItem("Vertices"),
				CreateIntLegendItem("Triangles"),
			};
			var sceneGeometryCharts = CreateLineCharts("Scene Geometry", geometryChartsDescriptions);
			var fullGeometryCharts = CreateLineCharts("Full Geometry", geometryChartsDescriptions);
			var gcChartsDescriptions = new [] {
				CreateFloatLegendItem("Memory"),
				CreateIntLegendItem("GC 0"),
				CreateIntLegendItem("GC 1"),
				CreateIntLegendItem("GC 2"),
			};
			var updateGcCharts = CreateLineCharts("Update GC", gcChartsDescriptions);
			var renderGcCharts = CreateLineCharts("Render GC", gcChartsDescriptions);
			void SetMinMaxHeight<T>(int value, ChartsInfo<T> chartsInfo) where T : Widget, IChartsGroup {
				chartsInfo.LegendWrapper.MinMaxHeight = value;
				chartsInfo.ChartsContainer.MinMaxHeight = value;
				chartsInfo.ChartsGroup.MinMaxHeight = value;
			}
			SetMinMaxHeight(64, updateCharts);
			SetMinMaxHeight(64, renderCharts);
			SetMinMaxHeight(64, gpuCharts);
			SetMinMaxHeight(96, sceneGeometryCharts);
			SetMinMaxHeight(96, fullGeometryCharts);
			SetMinMaxHeight(96, updateGcCharts);
			SetMinMaxHeight(96, renderGcCharts);
			Nodes.Add(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					new Widget {
						Layout = new VBoxLayout { Spacing = 2 },
						Padding = new Thickness(4, 0, 0, 0),
						Nodes = {
							updateCharts.LegendWrapper,
							renderCharts.LegendWrapper,
							gpuCharts.LegendWrapper,
							sceneGeometryCharts.LegendWrapper,
							fullGeometryCharts.LegendWrapper,
							updateGcCharts.LegendWrapper,
							renderGcCharts.LegendWrapper
						}
					},
					new Widget {
						Layout = new VBoxLayout { Spacing = 2 },
						Nodes = {
							updateCharts.ChartsContainer,
							renderCharts.ChartsContainer,
							gpuCharts.ChartsContainer,
							sceneGeometryCharts.ChartsContainer,
							fullGeometryCharts.ChartsContainer,
							updateGcCharts.ChartsContainer,
							renderGcCharts.ChartsContainer
						}
					}
				}
			});
			visibilityControllers = new ChartVisibilityControllers {
				MainThreadChartsSetVisible = value => { 
					updateCharts.LegendWrapper.Visible = value;
					updateCharts.ChartsContainer.Visible = value;
				},
				RenderThreadChartsSetVisible = value => { 
					renderCharts.LegendWrapper.Visible = value;
					renderCharts.ChartsContainer.Visible = value;
				},
				GpuChartsSetVisible = value => { 
					gpuCharts.LegendWrapper.Visible = value;
					gpuCharts.ChartsContainer.Visible = value;
				},
				SceneGeometryChartsSetVisible = value => { 
					sceneGeometryCharts.LegendWrapper.Visible = value;
					sceneGeometryCharts.ChartsContainer.Visible = value;
				},
				FullGeometryChartsSetVisible = value => { 
					fullGeometryCharts.LegendWrapper.Visible = value;
					fullGeometryCharts.ChartsContainer.Visible = value;
				},
				UpdateGcChartsSetVisible = value => { 
					updateGcCharts.LegendWrapper.Visible = value;
					updateGcCharts.ChartsContainer.Visible = value;
				},
				RenderGcChartsSetVisible = value => {
					renderGcCharts.LegendWrapper.Visible = value;
					renderGcCharts.ChartsContainer.Visible = value;
				}
			};
			ProfilerTerminal.FrameProfilingFinished += frame => {
				RemoteStopwatchExtension.Frequency = frame.StopwatchFrequency;
				var charts = updateCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.UpdateThreadElapsedTicks.TicksToMilliseconds());
				charts[1].Enqueue(0);
				var legend = updateCharts.ChartsLegend;
				legend.SetValue(frame.UpdateThreadElapsedTicks.TicksToMilliseconds(), 0);
				legend.SetValue(0, 1);
				updateCharts.ChartsGroup.Rebuild();
				
				charts = renderCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.RenderThreadElapsedTicks.TicksToMilliseconds());
				charts[1].Enqueue(0);
				legend = renderCharts.ChartsLegend;
				legend.SetValue(frame.RenderThreadElapsedTicks.TicksToMilliseconds(), 0);
				legend.SetValue(0, 1);
				renderCharts.ChartsGroup.Rebuild();
				
				charts = gpuCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.GpuElapsedTicks.TicksToMilliseconds());
				charts[1].Enqueue(0);
				legend = gpuCharts.ChartsLegend;
				legend.SetValue(frame.GpuElapsedTicks.TicksToMilliseconds(), 0);
				legend.SetValue(0, 1);
				gpuCharts.ChartsGroup.Rebuild();
				
				charts = sceneGeometryCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.SceneSavedByBatching);
				charts[1].Enqueue(frame.SceneDrawCallCount);
				charts[2].Enqueue(frame.SceneVerticesCount);
				charts[3].Enqueue(frame.SceneTrianglesCount);
				legend = sceneGeometryCharts.ChartsLegend;
				legend.SetValue(frame.SceneSavedByBatching, 0);
				legend.SetValue(frame.SceneDrawCallCount, 1);
				legend.SetValue(frame.SceneVerticesCount, 2);
				legend.SetValue(frame.SceneTrianglesCount, 3);
				sceneGeometryCharts.ChartsGroup.Rebuild();
				
				charts = fullGeometryCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.FullSavedByBatching);
				charts[1].Enqueue(frame.FullDrawCallCount);
				charts[2].Enqueue(frame.FullVerticesCount);
				charts[3].Enqueue(frame.FullTrianglesCount);
				legend = fullGeometryCharts.ChartsLegend;
				legend.SetValue(frame.FullSavedByBatching, 0);
				legend.SetValue(frame.FullDrawCallCount, 1);
				legend.SetValue(frame.FullVerticesCount, 2);
				legend.SetValue(frame.FullTrianglesCount, 3);
				fullGeometryCharts.ChartsGroup.Rebuild();

				charts = updateGcCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.EndOfUpdateMemory);
				for (int i = 0; i < charts.Count - 1; i++) {
					int value = i < frame.UpdateThreadGarbageCollections.Length
						? frame.UpdateThreadGarbageCollections[i] : 0;
					charts[i + 1].Enqueue(value - previousUpdateGC[i]);
					previousUpdateGC[i] = value;
				}
				legend = updateGcCharts.ChartsLegend;
				legend.SetValue(frame.EndOfUpdateMemory, 0);
				for (int i = 0; i < charts.Count - 1; i++) {
					legend.SetValue(i < frame.UpdateThreadGarbageCollections.Length ? 
							frame.UpdateThreadGarbageCollections[i] : 0, i + 1);
				}
				updateGcCharts.ChartsGroup.Rebuild();
				
				charts = renderGcCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.EndOfRenderMemory);
				for (int i = 0; i < charts.Count - 1; i++) {
					int value = 
						frame.RenderThreadGarbageCollections != null &&
						i < frame.RenderThreadGarbageCollections.Length ? 
							frame.RenderThreadGarbageCollections[i] : 0;
					charts[i + 1].Enqueue(value - previousRenderGC[i]);
					previousRenderGC[i] = value;
				}
				legend = renderGcCharts.ChartsLegend;
				legend.SetValue(frame.EndOfRenderMemory, 0);
				for (int i = 0; i < charts.Count - 1; i++) {
					legend.SetValue(
						frame.RenderThreadGarbageCollections != null &&
						i < frame.RenderThreadGarbageCollections.Length ?
							frame.RenderThreadGarbageCollections[i] : 0, i + 1);
				}
				renderGcCharts.ChartsGroup.Rebuild();
			};
		}
		
		private struct ChartsInfo<ChartsType> where ChartsType : Widget, IChartsGroup
		{
			public Widget LegendWrapper;
			public ChartsLegend ChartsLegend;
			public ChartsType ChartsGroup;
			public ChartsContainer ChartsContainer;
		}
	}
}

#endif // PROFILER