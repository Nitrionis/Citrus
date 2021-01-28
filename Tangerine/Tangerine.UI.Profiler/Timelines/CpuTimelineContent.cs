#if PROFILER

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using SpacingParameters = PeriodPositions.SpacingParameters;
	
	internal class CpuTimelineContent : TimelineContent<CpuUsage>
	{
		private readonly AsyncContentBuilder contentBuilder;
		private readonly int[] colorIndices;
		
		/// <summary>
		/// Changes the timeline rebuild mode for the next mesh rebuild requests.
		/// </summary>
		public TimelineMode Mode
		{
			get => contentBuilder.Mode; 
			set => contentBuilder.Mode = value;
		}
		
		public CpuTimelineContent(SpacingParameters spacingParameters) : base(spacingParameters)
		{
			contentBuilder = new AsyncContentBuilder(this);
		}

		public static Color4[] GetColors() => new [] {
			ColorTheme.Current.Profiler.TimelineUnselectedTasks,
			ColorTheme.Current.Profiler.TimelineAnimationTasks,
			ColorTheme.Current.Profiler.TimelineUpdateTasks,
			ColorTheme.Current.Profiler.TimelineGestureTasks,
			ColorTheme.Current.Profiler.TimelineRenderPreparationTasks,
			ColorTheme.Current.Profiler.TimelineNodeRenderTasks,
			ColorTheme.Current.Profiler.TimelineBatchRenderTasks,
			Color4.Red
		};
		
		public override IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod) => 
			contentBuilder.GetRectangles(timePeriod);

		public override IEnumerable<TimelineHitTest.ItemInfo> GetHitTestTargets() => 
			contentBuilder.GetHitTestTargets();
		
		protected override IAsyncContentBuilder<CpuUsage> GetContentBuilder() => contentBuilder;
		
		private class AsyncContentBuilder : IAsyncContentBuilder<CpuUsage>
		{
			private const int PreservedColorsCount = 2;
			private const int GrayColorBit = 1;
			private const int RedColorBit = 2;
			
			private readonly TimelineContent<CpuUsage> timelineContent;
			private readonly List<Rectangle> cachedRectangles;
			private readonly int[] colorsPackRasterizationTarget;
			private readonly uint[] usageReasonToColors;
			private Item[] items;
			private int itemsCount;
			
			public TimelineMode Mode { get; set; } = TimelineMode.Default;
			
			public AsyncContentBuilder(TimelineContent<CpuUsage> timelineContent)
			{
				this.timelineContent = timelineContent;
				items = new Item[1];
				cachedRectangles = new List<Rectangle>();
				colorsPackRasterizationTarget = new int[ColorsPack.MaxColorsCount];
				usageReasonToColors = new uint[(int)CpuUsage.Reasons.MaxReasonIndex + 1];
				for (int i = 0; i < usageReasonToColors.Length; i++) {
					usageReasonToColors[i] = RedColorBit;
				}
				// todo get colors and converters from outside
				var colors = usageReasonToColors;
				colors[(int)CpuUsage.Reasons.FullUpdate] = PreservedColorsCount + 1;
				colors[(int)CpuUsage.Reasons.FullRender] = PreservedColorsCount + 2;
				colors[(int)CpuUsage.Reasons.NodeAnimation] = PreservedColorsCount + 1;
				colors[(int)CpuUsage.Reasons.NodeUpdate] = PreservedColorsCount + 1;
				colors[(int)CpuUsage.Reasons.NodeProcessor] = PreservedColorsCount + 1;
				colors[(int)CpuUsage.Reasons.BehaviorComponentUpdate] = PreservedColorsCount + 1;
				colors[(int)CpuUsage.Reasons.NodeDeserialization] = PreservedColorsCount + 3;
				colors[(int)CpuUsage.Reasons.LoadExternalScenes] = PreservedColorsCount + 3;
				colors[(int)CpuUsage.Reasons.NodeRenderPreparation] = PreservedColorsCount + ;
				colors[(int)CpuUsage.Reasons.NodeRender] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.BatchRender] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.WaitForPreviousRendering] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.WaitForAcquiringSwapchainBuffer] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.ReferenceTableGarbageCollection] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.ProfilerDatabaseResizing] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.SyncBodyExecution] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.RunScheduledActions] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.RunPendingActionsOnRendering] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.AudioSystemUpdate] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.IssueCommands] = PreservedColorsCount + 0;
				colors[(int)CpuUsage.Reasons.ProcessCommands] = PreservedColorsCount + 0;
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
				if (frameData.IsSucceed) {
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
						if (filter(usage, frameData.Clipboard)) {
							if (Mode == TimelineMode.BatchBreakReasons) {
								var reasons = usage.Reason;
								if ((reasons & CpuUsage.Reasons.BatchRender) != 0) {
									data = (uint)(usage.Reason & CpuUsage.Reasons.BatchBreakReasonsBitMask);
									data <<= (int)CpuUsage.Reasons.BatchBreakReasonsStartBit - PreservedColorsCount;
								} else {
									data = GrayColorBit;
								}
							} else {
								if (filter(usage, frameData.Clipboard)) {
									data = usageReasonToColors[(uint)(usage.Reason & CpuUsage.Reasons.ReasonsBitMask)];
								} else {
									data = GrayColorBit;
								}
							}
						} else {
							data = GrayColorBit;
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
						CreateRectanglesFor(item, position);
					}
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
				int colorsCount = RasterizeColorsPack(item.ColorsPack);
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

			private int RasterizeColorsPack(ColorsPack colorsPack)
			{
				var data = colorsPack.Data;
				var colors = colorsPackRasterizationTarget;
				int colorSlotIndex = 0;
				for (int i = 0; i < ColorsPack.MaxColorsCount; i++, data <<= 1) {
					int bit = data & 1;
					colors[colorSlotIndex] = i * bit;
					colorSlotIndex += bit;
				}
				return colorSlotIndex;
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