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
				var chartsGroup = new LineCharts(GetParameters(items.Length)) { IndependentScaling = true };
				var linesContainer = new LinesContainer(1, new EmptyScaleProvider());
				var chartsLegend = new ChartsLegend(new [] { chartsGroup }, items);
				chartsGroup.SliceSelected += OnSliceSelected;
				return new ChartsInfo<LineCharts> {
					ChartsGroup = chartsGroup,
					ChartsLegend = chartsLegend,
					ChartsContainer = new ChartsContainer(new Widget[] { linesContainer, chartsGroup }) {
						BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
					},
					LegendWrapper = CreateLegendWrapper(name, chartsLegend),
					LinesContainer = linesContainer
				};
			}
			ChartsInfo<StackedAreaCharts> CreateAreaCharts(string name, ChartsLegend.ItemDescription[] items) {
				var chartsGroup = new StackedAreaCharts(GetParameters(items.Length));
				var linesContainer = new LinesContainer(5, new StackedAreaChartsScaleProvider(chartsGroup));
				var chartsLegend = new ChartsLegend(new [] { chartsGroup }, items);
				chartsGroup.SliceSelected += OnSliceSelected;
				return new ChartsInfo<StackedAreaCharts> {
					ChartsGroup = chartsGroup,
					ChartsLegend = chartsLegend,
					ChartsContainer = new ChartsContainer(new Widget[] { linesContainer, chartsGroup }) {
						BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
					},
					LegendWrapper = CreateLegendWrapper(name, chartsLegend),
					LinesContainer = linesContainer
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
				float greenFpsHeight = Logarithm(1000f / 60f);
				linesContainer.Lines[2] = new Line {
					Color = Color4.Green,
					Start = new Vector2(float.Epsilon, greenFpsHeight),
					End = new Vector2(finalSize.X - float.Epsilon, greenFpsHeight)
				};
				float yellowFpsHeight = Logarithm(1000f / 30f);
				linesContainer.Lines[3] = new Line {
					Color = Color4.Yellow,
					Start = new Vector2(float.Epsilon, yellowFpsHeight),
					End = new Vector2(finalSize.X - float.Epsilon, yellowFpsHeight)
				};
				float redFpsHeight = Logarithm(1000f / 15f);
				linesContainer.Lines[4] = new Line {
					Label = null,
					Color = Color4.Red,
					Start = new Vector2(float.Epsilon, redFpsHeight),
					End = new Vector2(finalSize.X - float.Epsilon, redFpsHeight)
				};
			}
			updateCharts = CreateAreaCharts("Main Thread", new[] {
				CreateFloatLegendItem("Update"),
				CreateFloatLegendItem("Body"),
				CreateFloatLegendItem("Selected")
			});
			renderCharts = CreateAreaCharts("Render Thread",new[] {
				CreateFloatLegendItem("Render"),
				CreateFloatLegendItem("Body"),
				CreateFloatLegendItem("Wait GPU"),
				CreateFloatLegendItem("Selected")
			});
			gpuCharts = CreateAreaCharts("GPU",new[] {
				CreateFloatLegendItem("Drawing"),
				CreateFloatLegendItem("Selected")
			});
			var geometryChartsDescriptions = new [] {
				CreateIntLegendItem("Batching"),
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
				chartsInfo.ChartsLegend.ValuesContainer.MinMaxHeight = value;
				chartsInfo.ChartsLegend.ValuesContainer.Padding = new Thickness(0, 0, 21, 0);
				chartsInfo.ChartsContainer.MinMaxHeight = value;
				chartsInfo.ChartsGroup.MinMaxHeight = value;
			}
			const float chartWidth = ControlPointsSpacing * (HistorySize - 1);
			SetMinMaxHeight(64, updateCharts);
			InitLines(updateCharts.LinesContainer, new Vector2(chartWidth, 64));
			SetMinMaxHeight(78, renderCharts);
			InitLines(renderCharts.LinesContainer, new Vector2(chartWidth, 78));
			SetMinMaxHeight(64, gpuCharts);
			InitLines(gpuCharts.LinesContainer, new Vector2(chartWidth, 64));
			SetMinMaxHeight(78, sceneGeometryCharts);
			InitSliceIndicator(sceneGeometryCharts.LinesContainer, new Vector2(chartWidth, 78));
			SetMinMaxHeight(78, fullGeometryCharts);
			InitSliceIndicator(fullGeometryCharts.LinesContainer, new Vector2(chartWidth, 78));
			SetMinMaxHeight(78, updateGcCharts);
			InitSliceIndicator(updateGcCharts.LinesContainer, new Vector2(chartWidth, 78));
			SetMinMaxHeight(78, renderGcCharts);
			InitSliceIndicator(renderGcCharts.LinesContainer, new Vector2(chartWidth, 78));
			Nodes.Add(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					new Widget {
						Layout = new HBoxLayout { Spacing = 0 },
						Padding = new Thickness(4, 0, 0, 0),
						Nodes = {
							new Widget {
								Layout = new VBoxLayout { Spacing = 0 },
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
								Layout = new VBoxLayout { Spacing = 0 },
								MinMaxWidth = 64,
								Nodes = {
									updateCharts.ChartsLegend.ValuesContainer,
									renderCharts.ChartsLegend.ValuesContainer,
									gpuCharts.ChartsLegend.ValuesContainer,
									sceneGeometryCharts.ChartsLegend.ValuesContainer,
									fullGeometryCharts.ChartsLegend.ValuesContainer,
									updateGcCharts.ChartsLegend.ValuesContainer,
									renderGcCharts.ChartsLegend.ValuesContainer
								}
							}
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
				chartsInfo.ChartsLegend.ValuesContainer.Visible = value;
			}
			visibilityControllers = new ChartVisibilityControllers {
				MainThreadChartsSetVisible = value => SetVisible(value, updateCharts),
				RenderThreadChartsSetVisible = value => SetVisible(value, renderCharts),
				GpuChartsSetVisible = value => SetVisible(value, gpuCharts),
				SceneGeometryChartsSetVisible = value => SetVisible(value, sceneGeometryCharts),
				FullGeometryChartsSetVisible = value => SetVisible(value, fullGeometryCharts),
				UpdateGcChartsSetVisible = value => SetVisible(value, updateGcCharts),
				RenderGcChartsSetVisible = value => SetVisible(value, renderGcCharts)
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

			void MoveSlice(Line[] lines) {
				lines[0].Start.X -= ControlPointsSpacing;
				lines[0].End.X -= ControlPointsSpacing;
			}
			
			float logarithmizedAll = Logarithm(frame.UpdateThreadElapsedTicks.TicksToMilliseconds());
			float bodyPercent = frame.UpdateBodyElapsedTicks / Math.Max(float.Epsilon, frame.UpdateThreadElapsedTicks);
			var charts = updateCharts.ChartsGroup.Charts;
			charts[0].Enqueue(logarithmizedAll * (1 - bodyPercent));
			charts[1].Enqueue(logarithmizedAll * bodyPercent);
			charts[2].Enqueue(0);
			MoveSlice(updateCharts.LinesContainer.Lines);

			logarithmizedAll = Logarithm(frame.RenderThreadElapsedTicks.TicksToMilliseconds());
			bodyPercent = frame.RenderBodyElapsedTicks / Math.Max(float.Epsilon, frame.RenderThreadElapsedTicks);
			float waitPercent = bodyPercent * frame.WaitForAcquiringSwapchainBuffer / Math.Max(float.Epsilon, frame.RenderBodyElapsedTicks);
			charts = renderCharts.ChartsGroup.Charts;
			charts[0].Enqueue(logarithmizedAll * (1 - bodyPercent));
			charts[1].Enqueue(logarithmizedAll * bodyPercent * (1 - waitPercent));
			charts[2].Enqueue(logarithmizedAll * bodyPercent * waitPercent);
			charts[3].Enqueue(0);
			MoveSlice(renderCharts.LinesContainer.Lines);

			charts = gpuCharts.ChartsGroup.Charts;
			charts[0].Enqueue(Logarithm(frame.GpuElapsedTime.TicksToMilliseconds()));
			charts[1].Enqueue(0);
			MoveSlice(gpuCharts.LinesContainer.Lines);
			
			charts = sceneGeometryCharts.ChartsGroup.Charts;
			charts[0].Enqueue(frame.SceneSavedByBatching);
			charts[1].Enqueue(frame.SceneDrawCallCount);
			charts[2].Enqueue(frame.SceneVerticesCount);
			charts[3].Enqueue(frame.SceneTrianglesCount);
			MoveSlice(sceneGeometryCharts.LinesContainer.Lines);

			charts = fullGeometryCharts.ChartsGroup.Charts;
			charts[0].Enqueue(frame.FullSavedByBatching);
			charts[1].Enqueue(frame.FullDrawCallCount);
			charts[2].Enqueue(frame.FullVerticesCount);
			charts[3].Enqueue(frame.FullTrianglesCount);
			MoveSlice(fullGeometryCharts.LinesContainer.Lines);

			charts = updateGcCharts.ChartsGroup.Charts;
			charts[0].Enqueue(frame.EndOfUpdateMemory);
			for (int i = 0; i < charts.Count - 1; i++) {
				var garbageCollections = frame.UpdateThreadGarbageCollections;
				if (i < garbageCollections.Length) {
					int value = garbageCollections[i];
					garbageCollections[i] -= previousUpdateGC[i];
					charts[i + 1].Enqueue(value);
					previousUpdateGC[i] = value;
				} else {
					charts[i + 1].Enqueue(0);
				}
			}
			MoveSlice(updateGcCharts.LinesContainer.Lines);
			
			charts = renderGcCharts.ChartsGroup.Charts;
			charts[0].Enqueue(frame.EndOfRenderMemory);
			for (int i = 0; i < charts.Count - 1; i++) {
				var garbageCollections = frame.RenderThreadGarbageCollections;
				if (garbageCollections != null && i < garbageCollections.Length) {
					int value = garbageCollections[i];
					garbageCollections[i] -= previousRenderGC[i];
					charts[i + 1].Enqueue(value);
					previousRenderGC[i] = value;
				} else {
					charts[i + 1].Enqueue(0);
				}
			}
			MoveSlice(renderGcCharts.LinesContainer.Lines);
			
			if (sliceIndex < 0) {
				SetFrameValuesToLegend(frame);
			}
		}

		public void SetSelectedAreaInTimeCharts(ObjectsSummaryResponse response)
		{
			void SetValues(StackedAreaCharts chartsGroup, int sliceIndex, float all, float selected) {
				// TODO 
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
					selectedTime = response.RenderTimeForEachFrame[j];
					SetValues(updateCharts.ChartsGroup, i, allTime, selectedTime);
					
					allTime = frame.GpuElapsedTime / 1000f;
					selectedTime = response.DrawTimeForEachFrame[j];
					SetValues(updateCharts.ChartsGroup, i, allTime, selectedTime);
					
					++j;
				}
			}
		}
		
		private void SetFrameValuesToLegend(ProfiledFrame frame)
		{
			var legend = updateCharts.ChartsLegend;
			legend.SetValue(frame.UpdateThreadElapsedTicks.TicksToMilliseconds(), 0);
			legend.SetValue(frame.UpdateBodyElapsedTicks.TicksToMilliseconds(), 1);
			legend.SetValue(0, 2);
			
			legend = renderCharts.ChartsLegend;
			legend.SetValue(frame.RenderThreadElapsedTicks.TicksToMilliseconds(), 0);
			legend.SetValue(frame.RenderBodyElapsedTicks.TicksToMilliseconds(), 1);
			legend.SetValue(frame.WaitForAcquiringSwapchainBuffer.TicksToMilliseconds(), 2);
			legend.SetValue(0, 3);

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
				var garbageCollections = frame.UpdateThreadGarbageCollections;
				int gcCount = i < garbageCollections.Length ? garbageCollections[i] : 0;
				legend.SetValue(gcCount, i + 1);
			}

			legend = renderGcCharts.ChartsLegend;
			legend.SetValue(frame.EndOfRenderMemory, 0);
			for (int i = 0; i < renderGcCharts.ChartsGroup.Charts.Count - 1; i++) {
				var garbageCollections = frame.RenderThreadGarbageCollections;
				int gcCount = i < (garbageCollections?.Length ?? -1) ? garbageCollections[i] : 0;
				legend.SetValue(gcCount, i + 1);
			}
		}

		private void OnSliceSelected(VerticalSlice slice)
		{
			sliceIndex = slice.Index;
			var frame = history.GetItem(slice.Index);
			SetFrameValuesToLegend(frame);
			float position = sliceIndex * ControlPointsSpacing;
			void SetSlicePosition(Line[] lines) {
				lines[0].Start.X = position;
				lines[0].End.X = position;
			}
			SetSlicePosition(updateCharts.LinesContainer.Lines);
			SetSlicePosition(renderCharts.LinesContainer.Lines);
			SetSlicePosition(gpuCharts.LinesContainer.Lines);
			SetSlicePosition(sceneGeometryCharts.LinesContainer.Lines);
			SetSlicePosition(fullGeometryCharts.LinesContainer.Lines);
			SetSlicePosition(updateGcCharts.LinesContainer.Lines);
			SetSlicePosition(renderGcCharts.LinesContainer.Lines);
			SliceSelected?.Invoke(frame.Identifier);
		}
		
		private static float Logarithm(float value) => value <= 33.3f ? value :
			33.3f + (float)(Math.Log((value - 33.3) / 8.0 + 2.0, 2) - 1) * 8;

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