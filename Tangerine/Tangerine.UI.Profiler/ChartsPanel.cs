#if PROFILER

using System;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;
using Tangerine.UI.Charts;

namespace Tangerine.UI
{
	using LegendItemDescription = ChartsLegend.ItemDescription;
	using VerticalSlice = FixedHorizontalSpacingCharts.VerticalSlice;
	
	internal class ChartsPanel : Widget
	{
		private const int HistorySize = 160;
		private const int ControlPointsSpacing = 5;

		private readonly FixedCapacityQueue<ProfiledFrame> history;
		
		private readonly int[] previousUpdateGC = new int[3];
		private readonly int[] previousRenderGC = new int[3];
		
		private readonly ChartsInfo<StackedAreaCharts> updateCharts;
		private readonly ChartsInfo<StackedAreaCharts> renderCharts;
		private readonly ChartsInfo<StackedAreaCharts> gpuCharts;
		
		private readonly ChartsInfo<LineCharts> sceneGeometryCharts;
		private readonly ChartsInfo<LineCharts> fullGeometryCharts;
		private readonly ChartsInfo<LineCharts> updateGcCharts;
		private readonly ChartsInfo<LineCharts> renderGcCharts;

		private int sliceIndex = -1;

		public event Action<long> SliceSelected; 
		
		public ChartsPanel(out ChartVisibilityControllers visibilityControllers)
		{
			history = new FixedCapacityQueue<ProfiledFrame>(HistorySize);
			Layout = new VBoxLayout();
			Anchors = Anchors.LeftRight;
			FixedHorizontalSpacingCharts.Parameters GetParameters(int chartsCount) => 
				new FixedHorizontalSpacingCharts.Parameters {
					ControlPointsCount = HistorySize,
					ChartsCount = chartsCount,
					ControlPointsSpacing = ControlPointsSpacing
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
				chartsGroup.SliceSelected += OnSliceSelected;
				return new ChartsInfo<LineCharts> {
					ChartsGroup = chartsGroup,
					ChartsLegend = chartsLegend,
					ChartsContainer = new ChartsContainer(groupList) {
						BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
					},
					LegendWrapper = CreateLegendWrapper(name, chartsLegend),
					LinesContainer = new LinesContainer(1, new EmptyScaleProvider())
				};
			}
			ChartsInfo<StackedAreaCharts> CreateAreaCharts(string name, ChartsLegend.ItemDescription[] items) {
				var chartsGroup = new StackedAreaCharts(GetParameters(items.Length));
				var groupList = new [] { chartsGroup };
				var chartsLegend = new ChartsLegend(groupList, items);
				chartsGroup.SliceSelected += OnSliceSelected;
				return new ChartsInfo<StackedAreaCharts> {
					ChartsGroup = chartsGroup,
					ChartsLegend = chartsLegend,
					ChartsContainer = new ChartsContainer(groupList) {
						BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
					},
					LegendWrapper = CreateLegendWrapper(name, chartsLegend),
					LinesContainer = new LinesContainer(5, new StackedAreaChartsScaleProvider(chartsGroup))
				};
			}
			void InitSliceIndicator(LinesContainer linesContainer, Vector2 finalSize) {
				linesContainer.Lines[0] = new Line {
					Label = null,
					Color = Color4.White,
					Start = new Vector2(0, float.Epsilon),
					End = new Vector2(0, finalSize.Y - float.Epsilon)
				};
			}
			void InitLines(LinesContainer linesContainer, Vector2 finalSize) {
				InitSliceIndicator(linesContainer, finalSize);
				linesContainer.Lines[1] = new Line {
					Label = null,
					Color = Color4.White,
					Start = new Vector2(float.Epsilon, 0),
					End = new Vector2(finalSize.X - float.Epsilon, 0)
				};
				linesContainer.Lines[2] = new Line {
					Color = Color4.Green,
					Start = new Vector2(float.Epsilon, 1000f / 60f),
					End = new Vector2(finalSize.X - float.Epsilon, 1000f / 60f)
				};
				linesContainer.Lines[3] = new Line {
					Color = Color4.Yellow,
					Start = new Vector2(float.Epsilon, 1000f / 30f),
					End = new Vector2(finalSize.X - float.Epsilon, 1000f / 30f)
				};
				linesContainer.Lines[4] = new Line {
					Label = null,
					Color = Color4.Red,
					Start = new Vector2(float.Epsilon, 1000f / 15f),
					End = new Vector2(finalSize.X - float.Epsilon, 1000f / 15f)
				};
			}
			updateCharts = CreateAreaCharts("Main Thread", new[] {
				CreateFloatLegendItem("Update"),
				CreateFloatLegendItem("Selected")
			});
			renderCharts = CreateAreaCharts("Render Thread",new[] {
				CreateFloatLegendItem("Render"),
				CreateFloatLegendItem("Selected")
			});
			gpuCharts = CreateAreaCharts("GPU",new[] {
				CreateFloatLegendItem("Drawing"),
				CreateFloatLegendItem("Selected")
			});
			var geometryChartsDescriptions = new [] {
				CreateIntLegendItem("Saved By Batching"),
				CreateIntLegendItem("Draw Calls"),
				CreateIntLegendItem("Vertices"),
				CreateIntLegendItem("Triangles"),
			};
			sceneGeometryCharts = CreateLineCharts("Scene Geometry", geometryChartsDescriptions);
			fullGeometryCharts = CreateLineCharts("Full Geometry", geometryChartsDescriptions);
			var gcChartsDescriptions = new [] {
				CreateFloatLegendItem("Memory"),
				CreateIntLegendItem("GC 0"),
				CreateIntLegendItem("GC 1"),
				CreateIntLegendItem("GC 2"),
			};
			updateGcCharts = CreateLineCharts("Update GC", gcChartsDescriptions);
			renderGcCharts = CreateLineCharts("Render GC", gcChartsDescriptions);
			void SetMinMaxHeight<T>(int value, ChartsInfo<T> chartsInfo) where T : Widget, IChartsGroup {
				chartsInfo.LegendWrapper.MinMaxHeight = value;
				chartsInfo.ChartsContainer.MinMaxHeight = value;
				chartsInfo.ChartsGroup.MinMaxHeight = value;
			}
			const float chartWidth = ControlPointsSpacing * (HistorySize - 1);
			SetMinMaxHeight(64, updateCharts);
			InitLines(updateCharts.LinesContainer, new Vector2(chartWidth, 64));
			SetMinMaxHeight(64, renderCharts);
			InitLines(renderCharts.LinesContainer, new Vector2(chartWidth, 64));
			SetMinMaxHeight(64, gpuCharts);
			InitLines(gpuCharts.LinesContainer, new Vector2(chartWidth, 64));
			SetMinMaxHeight(96, sceneGeometryCharts);
			InitSliceIndicator(sceneGeometryCharts.LinesContainer, new Vector2(chartWidth, 96));
			SetMinMaxHeight(96, fullGeometryCharts);
			InitSliceIndicator(fullGeometryCharts.LinesContainer, new Vector2(chartWidth, 96));
			SetMinMaxHeight(96, updateGcCharts);
			InitSliceIndicator(updateGcCharts.LinesContainer, new Vector2(chartWidth, 96));
			SetMinMaxHeight(96, renderGcCharts);
			InitSliceIndicator(renderGcCharts.LinesContainer, new Vector2(chartWidth, 96));
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
			void SetVisible<T>(bool value, ChartsInfo<T> chartsInfo) where T : Widget, IChartsGroup {
				chartsInfo.LegendWrapper.Visible = value;
				chartsInfo.ChartsContainer.Visible = value;
			}
			visibilityControllers = new ChartVisibilityControllers {
				MainThreadChartsSetVisible = value => SetVisible(value, updateCharts),
				RenderThreadChartsSetVisible = value => SetVisible(value, renderCharts),
				GpuChartsSetVisible = value => SetVisible(value, gpuCharts),
				SceneGeometryChartsSetVisible = value => SetVisible(value, sceneGeometryCharts),
				FullGeometryChartsSetVisible = value => SetVisible(value, fullGeometryCharts),
				UpdateGcChartsSetVisible = value => SetVisible(value, updateCharts),
				RenderGcChartsSetVisible = value => SetVisible(value, renderCharts)
			};
			Updating += delta => {
				if (updateCharts.ChartsContainer.Visible) {
					updateCharts.ChartsGroup.Rebuild();
				}
				if (renderCharts.ChartsContainer.Visible) {
					renderCharts.ChartsGroup.Rebuild();
				}
				if (gpuCharts.ChartsContainer.Visible) {
					gpuCharts.ChartsGroup.Rebuild();
				}
				if (sceneGeometryCharts.ChartsContainer.Visible) {
					sceneGeometryCharts.ChartsGroup.Rebuild();
				}
				if (fullGeometryCharts.ChartsContainer.Visible) {
					fullGeometryCharts.ChartsGroup.Rebuild();
				}
				if (updateGcCharts.ChartsContainer.Visible) {
					updateGcCharts.ChartsGroup.Rebuild();
				}
				if (renderGcCharts.ChartsContainer.Visible) {
					renderGcCharts.ChartsGroup.Rebuild();
				}
			};
			ProfilerTerminal.FrameProfilingFinished += EnqueueFrameValuesToCharts;
		}

		private void EnqueueFrameValuesToCharts(ProfiledFrame frame)
		{
			history.Enqueue(frame);
			sliceIndex = sliceIndex >= 0 ? sliceIndex - 1 : -1;
				
			RemoteStopwatchExtension.Frequency = frame.StopwatchFrequency;

			var charts = updateCharts.ChartsGroup.Charts;
			charts[0].Enqueue(Logarithm(frame.UpdateThreadElapsedTicks.TicksToMilliseconds()));
			charts[1].Enqueue(0);

			charts = renderCharts.ChartsGroup.Charts;
			charts[0].Enqueue(Logarithm(frame.RenderThreadElapsedTicks.TicksToMilliseconds()));
			charts[1].Enqueue(0);

			charts = gpuCharts.ChartsGroup.Charts;
			charts[0].Enqueue(Logarithm(frame.GpuElapsedTime.TicksToMilliseconds()));
			charts[1].Enqueue(0);
			
			charts = sceneGeometryCharts.ChartsGroup.Charts;
			charts[0].Enqueue(frame.SceneSavedByBatching);
			charts[1].Enqueue(frame.SceneDrawCallCount);
			charts[2].Enqueue(frame.SceneVerticesCount);
			charts[3].Enqueue(frame.SceneTrianglesCount);

			charts = fullGeometryCharts.ChartsGroup.Charts;
			charts[0].Enqueue(frame.FullSavedByBatching);
			charts[1].Enqueue(frame.FullDrawCallCount);
			charts[2].Enqueue(frame.FullVerticesCount);
			charts[3].Enqueue(frame.FullTrianglesCount);

			charts = updateGcCharts.ChartsGroup.Charts;
			charts[0].Enqueue(frame.EndOfUpdateMemory);
			for (int i = 0; i < charts.Count - 1; i++) {
				int value = i < frame.UpdateThreadGarbageCollections.Length
					? frame.UpdateThreadGarbageCollections[i] : 0;
				charts[i + 1].Enqueue(value - previousUpdateGC[i]);
				previousUpdateGC[i] = value;
			}

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

			if (sliceIndex < 0) {
				SetFrameValuesToLegend(frame);
			}
		}

		private void SetSelectedAreaInTimeCharts(ObjectsSummaryResponse response)
		{
			void SetValues(StackedAreaCharts chartsGroup, int sliceIndex, float all, float selected) {
				float logarithmizedAll = Logarithm(all);
				float percent = Math.Min(selected, all) / all;
				chartsGroup.Charts[0].Heights[sliceIndex] = logarithmizedAll * (1f - percent);
				chartsGroup.Charts[1].Heights[sliceIndex] = logarithmizedAll * percent;
			}
			for (int i = 0, j = 0; i < HistorySize; i++) {
				var frame = history.GetItem(i);
				if (
					frame.Identifier >= response.FirstFrameIdentifer &&
					frame.Identifier <= response.LastFrameIdentifer
					) 
				{
					float allTime = frame.UpdateThreadElapsedTicks.TicksToMilliseconds();
					float selectedTime = response.UpdateTimeForEachFrame[j];
					SetValues(updateCharts.ChartsGroup, i, allTime, selectedTime);
					
					allTime = frame.RenderThreadElapsedTicks.TicksToMilliseconds();
					selectedTime = response.UpdateTimeForEachFrame[j];
					SetValues(updateCharts.ChartsGroup, i, allTime, selectedTime);
					
					allTime = frame.GpuElapsedTime.TicksToMilliseconds();
					selectedTime = response.UpdateTimeForEachFrame[j];
					SetValues(updateCharts.ChartsGroup, i, allTime, selectedTime);
					
					++j;
				}
			}
		}
		
		private void SetFrameValuesToLegend(ProfiledFrame frame)
		{
			var legend = updateCharts.ChartsLegend;
			legend.SetValue(frame.UpdateThreadElapsedTicks.TicksToMilliseconds(), 0);
			legend.SetValue(0, 1);

			legend = renderCharts.ChartsLegend;
			legend.SetValue(frame.RenderThreadElapsedTicks.TicksToMilliseconds(), 0);
			legend.SetValue(0, 1);

			legend = gpuCharts.ChartsLegend;
			legend.SetValue(frame.GpuElapsedTime.TicksToMilliseconds(), 0);
			legend.SetValue(0, 1);

			legend = sceneGeometryCharts.ChartsLegend;
			legend.SetValue(frame.SceneSavedByBatching, 0);
			legend.SetValue(frame.SceneDrawCallCount, 1);
			legend.SetValue(frame.SceneVerticesCount, 2);
			legend.SetValue(frame.SceneTrianglesCount, 3);

			legend = fullGeometryCharts.ChartsLegend;
			legend.SetValue(frame.FullSavedByBatching, 0);
			legend.SetValue(frame.FullDrawCallCount, 1);
			legend.SetValue(frame.FullVerticesCount, 2);
			legend.SetValue(frame.FullTrianglesCount, 3);

			legend = updateGcCharts.ChartsLegend;
			legend.SetValue(frame.EndOfUpdateMemory, 0);
			for (int i = 0; i < updateGcCharts.ChartsGroup.Charts.Count - 1; i++) {
				legend.SetValue(
					i < frame.UpdateThreadGarbageCollections.Length ? 
						frame.UpdateThreadGarbageCollections[i] : 0, i + 1);
			}

			legend = renderGcCharts.ChartsLegend;
			legend.SetValue(frame.EndOfRenderMemory, 0);
			for (int i = 0; i < renderGcCharts.ChartsGroup.Charts.Count - 1; i++) {
				legend.SetValue(
					frame.RenderThreadGarbageCollections != null && 
					i < frame.RenderThreadGarbageCollections.Length ? 
						frame.RenderThreadGarbageCollections[i] : 0, i + 1);
			}
		}

		private void OnSliceSelected(VerticalSlice slice)
		{
			sliceIndex = slice.Index;
			var frame = history.GetItem(slice.Index);
			SetFrameValuesToLegend(frame);
			SliceSelected?.Invoke(frame.Identifier);
		}
		
		private static float Logarithm(float value) => value <= 33.3f ? value :
			33.3f + (float)Math.Log((value - 33.3) / 16.0 + 1.0, 2);

		private static float UnLogarithm(float value) => value <= 33.3f ? value :
			33.3f + ((float)Math.Pow(2, value - 33.3) - 1f) * 16.0f;

		private class StackedAreaChartsScaleProvider : IChartScaleProvider
		{
			private readonly StackedAreaCharts charts;
			
			public StackedAreaChartsScaleProvider(StackedAreaCharts charts) => this.charts = charts;

			public float GetScale(int lineIndex) => lineIndex == 0 ? 1 : charts.LastRebuildScaleCoefficient;
		}
		
		private class EmptyScaleProvider : IChartScaleProvider
		{
			public float GetScale(int lineIndex) => 1f;
		}
		
		private struct ChartsInfo<ChartsType> where ChartsType : Widget, IChartsGroup
		{
			public Widget LegendWrapper;
			public ChartsLegend ChartsLegend;
			public ChartsType ChartsGroup;
			public LinesContainer LinesContainer;
			public ChartsContainer ChartsContainer;
		}
	}
}

#endif // PROFILER