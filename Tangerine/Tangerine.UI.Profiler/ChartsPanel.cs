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

		private readonly FixedCapacityQueue<ExtendedFrame> history;

		private readonly int[] previousUpdateGC = new int[3];
		private readonly int[] previousRenderGC = new int[3];

		private readonly ChartsInfo<StackedAreaCharts> updateCharts;
		private readonly ChartsInfo<StackedAreaCharts> renderCharts;
		private readonly ChartsInfo<StackedAreaCharts> gpuCharts;
		
		private readonly ChartsInfo<LineCharts> sceneGeometryCharts;
		private readonly ChartsInfo<LineCharts> fullGeometryCharts;
		private readonly ChartsInfo<LineCharts> updateGcCharts;
		private readonly ChartsInfo<LineCharts> renderGcCharts;

		private int currentSliceIndex = -1;

		public event Action<long> FrameSelected; 
		
		public ChartsPanel(out ChartVisibilityControllers visibilityControllers)
		{
			history = new FixedCapacityQueue<ExtendedFrame>(HistorySize);
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
					ValueFormat = "{0:00.00}"
				};
			LegendItemDescription CreateIntLegendItem(string name) =>
				new LegendItemDescription {
					Label = name,
					ValueFormat = "{0:0.}"
				};
			Widget CreateChartsBackground(Widget widget) {
				const float maxChartsWidth = (HistorySize - 1) * ControlPointsSpacing;
				widget.MaxWidth = maxChartsWidth;
				return new Widget {
					Layout = new HBoxLayout(),
					Presenter = new WidgetFlatFillPresenter(Theme.Colors.ControlBorder),
					Anchors = Anchors.LeftRight,
					MaxWidth = float.PositiveInfinity,
					Nodes = {widget, Spacer.HStretch()}
				};
			}
			ChartsInfo<LineCharts> CreateLineCharts(string name, ChartsLegend.ItemDescription[] items) {
				var chartsGroup = new LineCharts(GetParameters(items.Length)) { IndependentScaling = true };
				var linesContainer = new LinesContainer(1, new EmptyScaleProvider());
				var chartsLegend = new ChartsLegend(new [] { chartsGroup }, items);
				var chartsContainer = new ChartsContainer(new Widget[] { linesContainer, chartsGroup }) {
					BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
				};
				chartsGroup.SliceSelected += OnSliceSelected;
				return new ChartsInfo<LineCharts> {
					ChartsGroup = chartsGroup,
					ChartsLegend = chartsLegend,
					ChartsContainer = CreateChartsBackground(chartsContainer),
					LegendWrapper = CreateLegendWrapper(name, chartsLegend),
					LinesContainer = linesContainer
				};
			}
			ChartsInfo<StackedAreaCharts> CreateAreaCharts(string name, ChartsLegend.ItemDescription[] items) {
				var chartsGroup = new StackedAreaCharts(GetParameters(items.Length));
				var linesContainer = new LinesContainer(5, new StackedAreaChartsScaleProvider(chartsGroup));
				var chartsLegend = new ChartsLegend(new [] { chartsGroup }, items);
				var chartsContainer = new ChartsContainer(new Widget[] { linesContainer, chartsGroup }) {
					BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
				};
				chartsGroup.SliceSelected += OnSliceSelected;
				return new ChartsInfo<StackedAreaCharts> {
					ChartsGroup = chartsGroup,
					ChartsLegend = chartsLegend,
					ChartsContainer = CreateChartsBackground(chartsContainer),
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
				CreateFloatLegendItem("Selected"),
				CreateFloatLegendItem("Wait GPU")
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
				CreateIntLegendItem("Memory"),
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
				Application.MainWindow.Invalidate();
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
			history.Enqueue(new ExtendedFrame {
				Frame = frame,
				SelectedData = new SelectedData()
			});
			currentSliceIndex = currentSliceIndex >= 0 ? currentSliceIndex - 1 : -1;
				
			RemoteStopwatchExtension.Frequency = frame.StopwatchFrequency;

			void MoveSlice(Line[] lines) {
				lines[0].Start.X -= ControlPointsSpacing;
				lines[0].End.X -= ControlPointsSpacing;
			}
			{ // CPU Update thread charts
				long fullTicks = frame.UpdateThreadElapsedTicks;
				long bodyTicks = frame.UpdateBodyElapsedTicks;
				float logarithmizedFull = Logarithm(fullTicks.TicksToMilliseconds());
				float bodyPercent = bodyTicks / Math.Max(float.Epsilon, fullTicks);
				var charts = updateCharts.ChartsGroup.Charts;
				charts[0].Enqueue(logarithmizedFull * (1 - bodyPercent));
				charts[1].Enqueue(logarithmizedFull * bodyPercent);
				charts[2].Enqueue(0);
				MoveSlice(updateCharts.LinesContainer.Lines);
			}
			{ // CPU Render thread charts
				long fullTicks = frame.RenderThreadElapsedTicks;
				long bodyTicks = frame.RenderBodyElapsedTicks;
				long waitTicks = frame.WaitForAcquiringSwapchainBuffer;
				float logarithmizedFull = Logarithm(fullTicks.TicksToMilliseconds());
				float bodyPercent = bodyTicks / Math.Max(float.Epsilon, fullTicks);
				float waitPercent = waitTicks / Math.Max(float.Epsilon, bodyTicks);
				var charts = renderCharts.ChartsGroup.Charts;
				charts[0].Enqueue(logarithmizedFull * (1 - bodyPercent));
				charts[1].Enqueue(logarithmizedFull * bodyPercent * (1 - waitPercent));
				charts[2].Enqueue(0);
				charts[3].Enqueue(logarithmizedFull * bodyPercent * waitPercent);
				MoveSlice(renderCharts.LinesContainer.Lines);
			}
			{ // GPU Drawing charts
				var charts = gpuCharts.ChartsGroup.Charts;
				charts[0].Enqueue(Logarithm(frame.GpuElapsedTime / 1000f));
				charts[1].Enqueue(0);
				MoveSlice(gpuCharts.LinesContainer.Lines);
			}
			{ // Scene geometry data charts
				var charts = sceneGeometryCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.SceneSavedByBatching);
				charts[1].Enqueue(frame.SceneDrawCallCount);
				charts[2].Enqueue(frame.SceneVerticesCount);
				charts[3].Enqueue(frame.SceneTrianglesCount);
				MoveSlice(sceneGeometryCharts.LinesContainer.Lines);
			}
			{ // Full geometry data charts
				var charts = fullGeometryCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.FullSavedByBatching);
				charts[1].Enqueue(frame.FullDrawCallCount);
				charts[2].Enqueue(frame.FullVerticesCount);
				charts[3].Enqueue(frame.FullTrianglesCount);
				MoveSlice(fullGeometryCharts.LinesContainer.Lines);
			}
			{ // Update thread garbage collection charts
				var charts = updateGcCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.EndOfUpdateMemory);
				for (int i = 0; i < charts.Count - 1; i++) {
					var garbageCollections = frame.UpdateThreadGarbageCollections;
					if (i < garbageCollections.Length) {
						int value = garbageCollections[i];
						garbageCollections[i] -= Math.Min(garbageCollections[i], previousUpdateGC[i]);
						charts[i + 1].Enqueue(garbageCollections[i]);
						previousUpdateGC[i] = value;
					} else {
						charts[i + 1].Enqueue(0);
					}
				}
				MoveSlice(updateGcCharts.LinesContainer.Lines);
			}
			{ // Render thread garbage collection charts
				var charts = renderGcCharts.ChartsGroup.Charts;
				charts[0].Enqueue(frame.EndOfRenderMemory);
				for (int i = 0; i < charts.Count - 1; i++) {
					var garbageCollections = frame.RenderThreadGarbageCollections;
					if (garbageCollections != null && i < garbageCollections.Length) {
						int value = garbageCollections[i];
						garbageCollections[i] -= Math.Min(garbageCollections[i], previousRenderGC[i]);
						charts[i + 1].Enqueue(garbageCollections[i]);
						previousRenderGC[i] = value;
					} else {
						charts[i + 1].Enqueue(0);
					}
				}
				MoveSlice(renderGcCharts.LinesContainer.Lines);
			}
			if (currentSliceIndex < 0) {
				SetFrameValuesToLegend(frame, new SelectedData());
			}
		}

		public void SetSelectedAreaInTimeCharts(ObjectsSummaryResponse response)
		{
			for (int i = 0, j = 0; i < HistorySize; i++) {
				var frame = history.GetItem(i).Frame;
				bool isNoDataSelected = 
					response == null || 
					!response.IsSucceed ||
					frame.Identifier < response.FirstFrameIdentifier ||
					frame.Identifier > response.LastFrameIdentifier;
				history[history.GetInternalIndex(i)] = new ExtendedFrame {
					Frame = frame,
					SelectedData = new SelectedData {
						UpdateTime = isNoDataSelected ? 0 : response.UpdateTimeForEachFrame[j],
						RenderTime = isNoDataSelected ? 0 : response.RenderTimeForEachFrame[j],
						GpuTime = isNoDataSelected ? 0 : response.DrawTimeForEachFrame[j]
					}
				};
				j += isNoDataSelected ? 0 : 1;
			}
			for (int i = 0; i < HistorySize; i++) {
				var data = history.GetItem(i);
				var frame = data.Frame;
				var selected = data.SelectedData;
				{ // CPU Update thread charts
					long fullTicks = frame.UpdateThreadElapsedTicks;
					long bodyTicks = frame.UpdateBodyElapsedTicks;
					float logarithmizedFull = Logarithm(fullTicks.TicksToMilliseconds());
					float bodyPercent = bodyTicks / Math.Max(float.Epsilon, fullTicks);
					float selectedPercent = 
						selected.UpdateTime / Math.Max(float.Epsilon, bodyTicks.TicksToMilliseconds());
					var charts = updateCharts.ChartsGroup.Charts;
					charts[1].Heights[i] = logarithmizedFull * bodyPercent * (1 - selectedPercent);
					charts[2].Heights[i] = logarithmizedFull * bodyPercent * selectedPercent;
				}
				{ // CPU Render thread charts
					long fullTicks = frame.RenderThreadElapsedTicks;
					long bodyTicks = frame.RenderBodyElapsedTicks;
					long waitTicks = frame.WaitForAcquiringSwapchainBuffer;
					float logarithmizedFull = Logarithm(fullTicks.TicksToMilliseconds());
					float bodyPercent = bodyTicks / Math.Max(float.Epsilon, fullTicks);
					float waitPercent = waitTicks / Math.Max(float.Epsilon, bodyTicks);
					float selectedPercent = 
						selected.RenderTime / Math.Max(float.Epsilon, bodyTicks.TicksToMilliseconds());
					var charts = renderCharts.ChartsGroup.Charts;
					charts[1].Heights[i] = logarithmizedFull * bodyPercent * (1 - waitPercent - selectedPercent);
					charts[2].Heights[i] = logarithmizedFull * bodyPercent * selectedPercent;
				}
				{ // GPU Drawing charts
					float fullTime = frame.GpuElapsedTime / 1000f;
					float logarithmizedFull = Logarithm(fullTime);
					float selectedPercent = selected.GpuTime / Math.Max(float.Epsilon, fullTime);
					var charts = gpuCharts.ChartsGroup.Charts;
					charts[0].Heights[i] = logarithmizedFull * (1 - selectedPercent);
					charts[1].Heights[i] = logarithmizedFull * selectedPercent;
				}
			}
		}
		
		private void SetFrameValuesToLegend(ProfiledFrame frame, SelectedData selectedData)
		{
			var legend = updateCharts.ChartsLegend;
			legend.SetValue(frame.UpdateThreadElapsedTicks.TicksToMilliseconds(), 0);
			legend.SetValue(frame.UpdateBodyElapsedTicks.TicksToMilliseconds(), 1);
			legend.SetValue(selectedData.UpdateTime, 2);
			
			legend = renderCharts.ChartsLegend;
			legend.SetValue(frame.RenderThreadElapsedTicks.TicksToMilliseconds(), 0);
			legend.SetValue(frame.RenderBodyElapsedTicks.TicksToMilliseconds(), 1);
			legend.SetValue(selectedData.RenderTime, 2);
			legend.SetValue(frame.WaitForAcquiringSwapchainBuffer.TicksToMilliseconds(), 3);

			legend = gpuCharts.ChartsLegend;
			legend.SetValue(frame.GpuElapsedTime.TicksToMilliseconds(), 0);
			legend.SetValue(selectedData.GpuTime, 1);

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
			currentSliceIndex = slice.Index;
			var data = history.GetItem(slice.Index);
			SetFrameValuesToLegend(data.Frame, data.SelectedData);
			float position = currentSliceIndex * ControlPointsSpacing;
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
			FrameSelected?.Invoke(data.Frame.Identifier);
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
			public Widget ChartsContainer;
		}
		
		private struct ExtendedFrame
		{
			public ProfiledFrame Frame;
			public SelectedData SelectedData;
		}
		
		private struct SelectedData
		{
			public float UpdateTime;
			public float RenderTime;
			public float GpuTime;
		}
	}
}

#endif // PROFILER
