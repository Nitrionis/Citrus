#if PROFILER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	using SpacingParameters = PeriodPositions.SpacingParameters;

	internal class CpuTimelineContent : TimelineContent<CpuUsage>
	{
		private static Color4[] defaultModeColors;
		private static Color4[] breakReasonsModeColors;
		
		private readonly AsyncContentBuilder contentBuilder;

		public CpuTimelineContent(SpacingParameters spacingParameters) : base(spacingParameters)
		{
			contentBuilder = new AsyncContentBuilder(this);
		}

		public static Color4[] GetDefaultModeColors() => 
			(defaultModeColors = defaultModeColors ?? new[] {
				ColorTheme.Current.Profiler.TimelineUnselectedTasks, 
				ColorTheme.Current.Profiler.TimelineAnimationTasks,
				ColorTheme.Current.Profiler.TimelineUpdateTasks, 
				ColorTheme.Current.Profiler.TimelineGestureTasks,
				ColorTheme.Current.Profiler.TimelineRenderPreparationTasks,
				ColorTheme.Current.Profiler.TimelineNodeRenderTasks, 
				ColorTheme.Current.Profiler.TimelineBatchRenderTasks,
				ColorTheme.Current.Profiler.TimelineWaitTasks,
				ColorTheme.Current.Profiler.TimelineAudioSystemTasks,
				ColorTheme.Current.Profiler.TimelineDeserializationTasks,
				ColorTheme.Current.Profiler.TimelineRunPendingActionsTasks,
				Color4.Red
			});

		public static Color4[] GetBatchBreakReasonsColors() => 
			(breakReasonsModeColors = breakReasonsModeColors ?? new[] {
				ColorTheme.Current.Profiler.TimelineUnselectedTasks,
				ColorTheme.Current.Profiler.TimelineBatchBreakNullLastBatch,
				ColorTheme.Current.Profiler.TimelineBatchBreakDifferentMaterials,
				ColorTheme.Current.Profiler.TimelineBatchBreakMaterialPassCount,
				ColorTheme.Current.Profiler.TimelineBatchBreakVertexBufferOverflow,
				ColorTheme.Current.Profiler.TimelineBatchBreakIndexBufferOverflow,
				ColorTheme.Current.Profiler.TimelineBatchBreakDifferentAtlasOne,
				ColorTheme.Current.Profiler.TimelineBatchBreakDifferentAtlasTwo
			});

		public override IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod) =>
			contentBuilder.GetRectangles(timePeriod);

		public override IEnumerable<TimelineHitTest.ItemInfo> GetHitTestTargets() =>
			contentBuilder.GetHitTestTargets();

		protected override IAsyncContentBuilder<CpuUsage> GetContentBuilder() => contentBuilder;

		public Task RebuildAsync(Task waitingTask, TimelineMode mode)
		{
			contentBuilder.RequestedMode = mode;
			return RebuildAsync(LastRequestedFrame, waitingTask, LastRequestedFilter);
		}

		private class AsyncContentBuilder : IAsyncContentBuilder<CpuUsage>
		{
			private const int PreservedBitsCount = 1;
			private const int GrayColorBit = 1;

			private const int GrayColorIndex = 0;
			
			private readonly TimelineContent<CpuUsage> timelineContent;
			private readonly List<Rectangle> cachedRectangles;
			private readonly int[] colorsPackRasterizationTarget;
			private readonly uint[] usageReasonToColors;
			private Item[] items;
			private int itemsCount;

			public TimelineMode RequestedMode { get; set; } = TimelineMode.Default;
			
			public AsyncContentBuilder(TimelineContent<CpuUsage> timelineContent)
			{
				this.timelineContent = timelineContent;
				items = new Item[1];
				cachedRectangles = new List<Rectangle>();
				colorsPackRasterizationTarget = new int[ColorsPack.MaxColorsCount];
				usageReasonToColors = new uint[(int) CpuUsage.Reasons.MaxReasonIndex + 1];
				for (int i = 0; i < usageReasonToColors.Length; i++) {
					usageReasonToColors[i] = RedColorBit;
				}

				// todo get colors and converters from outside
				var colors = usageReasonToColors;
				colors[(int)CpuUsage.Reasons.FullUpdate] = GrayColorBit;
				colors[(int)CpuUsage.Reasons.SyncBodyExecution] = GrayColorBit;
				colors[(int)CpuUsage.Reasons.FullRender] = GrayColorBit;
				colors[(int)CpuUsage.Reasons.NodeAnimation] = PreservedBitsCount + 1;
				colors[(int)CpuUsage.Reasons.NodeUpdate] = PreservedBitsCount + 1;
				colors[(int)CpuUsage.Reasons.NodeProcessor] = PreservedBitsCount + 1;
				colors[(int)CpuUsage.Reasons.BehaviorComponentUpdate] = PreservedBitsCount + 1;
				colors[(int)CpuUsage.Reasons.NodeDeserialization] = PreservedBitsCount + 3;
				colors[(int)CpuUsage.Reasons.LoadExternalScenes] = PreservedBitsCount + 3;
				colors[(int)CpuUsage.Reasons.NodeRenderPreparation] = PreservedBitsCount +;
				colors[(int)CpuUsage.Reasons.NodeRender] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.BatchRender] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.WaitForPreviousRendering] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.WaitForAcquiringSwapchainBuffer] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.ReferenceTableGarbageCollection] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.ProfilerDatabaseResizing] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.RunScheduledActions] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.RunPendingActionsOnRendering] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.AudioSystemUpdate] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.IssueCommands] = PreservedBitsCount + 0;
				colors[(int)CpuUsage.Reasons.ProcessCommands] = PreservedBitsCount + 0;
			}

			public IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod)
			{
				float microsecondsPerPixel = timelineContent.SpacingParameters.MicrosecondsPerPixel;
				float minX = timePeriod.StartTime / microsecondsPerPixel;
				float maxX = timePeriod.FinishTime / microsecondsPerPixel;
				foreach (var rectangle in cachedRectangles) {
					var x = rectangle.Position.X;
					if (minX <= x && x <= maxX) {
						yield return rectangle;
					}
				}
			}

			public IEnumerable<TimelineHitTest.ItemInfo> GetHitTestTargets()
			{
				foreach (var item in items) {
					yield return new TimelineHitTest.ItemInfo {
						TimePeriod = item.TimePeriod, 
						VerticalLocation = item.VerticalLocation
					};
				}
			}

			public void RebuildAsync(FrameDataResponse frameData, Filter<CpuUsage> filter)
			{
				if (!frameData.IsSucceed) {
					return;
				}
				
				itemsCount = 0;
				cachedRectangles.Clear();
				var usages = frameData.Clipboard.UpdateCpuUsages;
				var spacingParameters = timelineContent.SpacingParameters;
				
				long stopwatchFrequency = frameData.ProfiledFrame.StopwatchFrequency;
				long updateThreadStartTime = frameData.ProfiledFrame.UpdateThreadStartTime;
				
				var periods = GetPeriods(usages, updateThreadStartTime, stopwatchFrequency);
				var positions = new PeriodPositions(periods, spacingParameters);
				float ticksPerMicrosecond = stopwatchFrequency / 1000f;

				TimePeriod UsageToTimePeriod(CpuUsage usage) => new TimePeriod(
					startTime: (usage.StartTime - updateThreadStartTime) / ticksPerMicrosecond,
					finishTime: (usage.FinishTime - updateThreadStartTime) / ticksPerMicrosecond);

				ColorsPack CreateColorsPack(CpuUsage usage) {
					uint data = 0;
					bool isFilterPassed = filter(usage, frameData.Clipboard);
					if (RequestedMode == TimelineMode.BatchBreakReasons) {
						var reasons = usage.Reason;
						if (isFilterPassed && (reasons & CpuUsage.Reasons.BatchRender) != 0) {
							data = (uint) (usage.Reason & CpuUsage.Reasons.BatchBreakReasonsBitMask);
							data <<= (int) CpuUsage.Reasons.BatchBreakReasonsStartBit - PreservedBitsCount;
						} else {
							data = GrayColorBit;
						}
					} else {
						data = isFilterPassed ? (uint)GrayColorIndex :
							usageReasonToColors[(uint)(usage.Reason & CpuUsage.Reasons.ReasonsBitMask)];
					}
					return new ColorsPack { Data = (byte)data };
				}
				
				foreach (var (usage, position) in usages.Zip(positions, Tuple.Create)) {
					var period = UsageToTimePeriod(usage);
					var item = new Item {
						TimePeriod = period,
						VerticalLocation = new Range {
							A = position.Y, 
							B = position.Y + spacingParameters.TimePeriodHeight
						},
						ColorsPack = CreateColorsPack(usage)
					};
					if (itemsCount >= items.Length) {
						Array.Resize(ref items, 2 * items.Length);
					}
					items[itemsCount++] = item;
					CreateRectanglesFor(item, position);
				}
			}

			public void RescaleItemsAsync()
			{
				cachedRectangles.Clear();
				var spacingParameters = timelineContent.SpacingParameters;
				var positions = new PeriodPositions(GetCachedPeriods(), spacingParameters).GetEnumerator();
				positions.MoveNext();
				for (int i = 0; i < itemsCount; i++, positions.MoveNext()) {
					var position = positions.Current;
					var item = items[i];
					item.VerticalLocation = new Range {
						A = position.Y, 
						B = position.Y + spacingParameters.TimePeriodHeight
					};
					items[i] = item;
					CreateRectanglesFor(item, position);
				}
			}

			private void CreateRectanglesFor(Item item, Vector2 position)
			{
				var spacingParameters = timelineContent.SpacingParameters;
				int colorsCount;
				if (RequestedMode == TimelineMode.BatchBreakReasons) {
					colorsCount = item.ColorsPack.RasterizeTo(colorsPackRasterizationTarget);
				} else {
					colorsPackRasterizationTarget[0] = item.ColorsPack.AsSingleColor;
					colorsCount = 1;
				}
				var rectangleSize = new Vector2(
					x: item.TimePeriod.Duration / spacingParameters.MicrosecondsPerPixel,
					y: spacingParameters.TimePeriodHeight / colorsCount);
				for (int j = 0; j < colorsCount; j++) {
					cachedRectangles.Add(new Rectangle {
						Position = new Vector2(position.X, position.Y + j * rectangleSize.Y),
						Size = rectangleSize,
						ColorIndex = (byte)colorsPackRasterizationTarget[j]
					});
				}
			}

			private IEnumerable<TimePeriod> GetPeriods(List<CpuUsage> usages, long frameTimestamp, long frequency)
			{
				float ticksPerMicrosecond = frequency / 1000f;
				foreach (var usage in usages) {
					yield return new TimePeriod(
						startTime: (usage.StartTime - frameTimestamp) / ticksPerMicrosecond,
						finishTime: (usage.FinishTime - frameTimestamp) / ticksPerMicrosecond);
				}
			}

			private IEnumerable<TimePeriod> GetCachedPeriods()
			{
				for (int i = 0; i < itemsCount; i++) {
					yield return items[i].TimePeriod;
				}
			}

			private struct Item
			{
				public TimePeriod TimePeriod;
				public ColorsPack ColorsPack;
				public Range VerticalLocation;
			}

			private struct ColorsPack
			{
				public const int MaxColorsCount = 8;

				public byte Data;

				/// <summary>
				/// Interprets a pack of colors as a single color.
				/// </summary>
				public byte AsSingleColor => Data;
				
				/// <summary>
				/// Interprets structure data as a set of colors.
				/// </summary>
				/// <param name="rasterizationTarget">
				/// An array into which the color numbers will be written.
				/// </param>
				/// <returns>
				/// Number of colors in the pack.
				/// </returns>
				public int RasterizeTo(int[] rasterizationTarget)
				{
					int colorSlotIndex = 0;
					for (int i = 0; i < MaxColorsCount; i++, Data <<= 1) {
						int bit = Data & 1;
						rasterizationTarget[colorSlotIndex] = i * bit;
						colorSlotIndex += bit;
					}
					return colorSlotIndex;
				}
			}
		}

		public enum TimelineMode
		{
			Default,
			BatchBreakReasons
		}
	}
}

#endif // PROFILER
