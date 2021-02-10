#if PROFILER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	using SpacingParameters = PeriodPositions.SpacingParameters;
	using OwnersPool = RingPool<ReferenceTable.RowIndex>;
	
	internal class CpuTimelineContent : TimelineContent<CpuUsage, CpuTimelineContent.ItemLabel>
	{
		private readonly AsyncContentBuilder contentBuilder;

		public CpuTimelineContent(SpacingParameters spacingParameters) : base(spacingParameters)
		{
			contentBuilder = new AsyncContentBuilder(this);
		}

		public override IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod) =>
			contentBuilder.GetRectangles(timePeriod);

		public override IEnumerable<ItemLabel> GetVisibleLabels(TimePeriod timePeriod) =>
			contentBuilder.GetVisibleLabels(timePeriod);
		
		public override IEnumerable<TimelineHitTest.ItemInfo> GetHitTestTargets() =>
			contentBuilder.GetHitTestTargets();

		protected override IAsyncContentBuilder<CpuUsage> GetContentBuilder() => contentBuilder;
		
		public Task RebuildAsync(Task waitingTask, TimelineMode mode)
		{
			var task = NewestContentModificationTask.ContinueWith(async (t) => {
				 await waitingTask;
				 contentBuilder.Mode = mode;
			});
			return RebuildAsync(LastRequestedFrame, task, LastRequestedFilter, LastRequestedSpacingParameters);
		}

		private class AsyncContentBuilder : IAsyncContentBuilder<CpuUsage>
		{
			private const int BreakReasonPreservedBitsCount = 
				CpuTimelineColorSet.BreakReasonPreservedBitsCount;
			
			private const int BreakReasonGrayColorBit = 1;
			private const int UnselectedItemColorIndex = 0;

			private readonly TimelineContent<CpuUsage, ItemLabel> timelineContent;
			private readonly List<Rectangle> cachedRectangles;
			private readonly uint[] colorsPackRasterizationTarget;
			private readonly uint[] usageReasonToColors;
			private readonly Func<List<float>> freeSpaceOfLinesGetter;
			
			private Item[] items;
			private int itemsCount;

			public TimelineMode Mode { get; set; } = TimelineMode.Default;
			
			public AsyncContentBuilder(TimelineContent<CpuUsage, ItemLabel> timelineContent)
			{
				this.timelineContent = timelineContent;
				items = new Item[1];
				cachedRectangles = new List<Rectangle>();
				colorsPackRasterizationTarget = new uint[ColorIndicesPack.MaxColorsCount];
				usageReasonToColors = CpuTimelineColorSet.GetUsageReasonColorIndices();
				var freeSpaceOfLines = new List<float>();
				freeSpaceOfLinesGetter = () => freeSpaceOfLines;
			}

			public IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod)
			{
				float microsecondsPerPixel = 
					timelineContent.AsyncSpacingParameters.MicrosecondsPerPixel;
				float minX = timePeriod.StartTime / microsecondsPerPixel;
				float maxX = timePeriod.FinishTime / microsecondsPerPixel;
				foreach (var rectangle in cachedRectangles) {
					float left = rectangle.Position.X;
					float right = left + rectangle.Size.X;
					if (minX <= right && left <= maxX) {
						yield return rectangle;
					}
				}
			}

			public IEnumerable<ItemLabel> GetVisibleLabels(TimePeriod timePeriod)
			{
				float pixelsPerMicrosecond = 
					1f / timelineContent.AsyncSpacingParameters.MicrosecondsPerPixel;
				for (int i = 0; i < itemsCount; i++) {
					var label = items[i].CreateItemLabel();
					if (
						timePeriod.StartTime <= label.Period.FinishTime &&
						timePeriod.FinishTime >= label.Period.StartTime &&
						label.Period.Duration * pixelsPerMicrosecond >= label.Width
						) 
					{
						yield return label;
					}
				}
			}
			
			public IEnumerable<TimelineHitTest.ItemInfo> GetHitTestTargets()
			{
				for (int i = 0; i < itemsCount; i++) {
					var item = items[i];
					yield return new TimelineHitTest.ItemInfo {
						TimePeriod = item.TimePeriod, 
						VerticalLocation = item.VerticalLocation
					};
				}
			}

			public (float, Color4[]) RebuildAsync(FrameDataResponse frameData, Filter<CpuUsage> filter)
			{
				itemsCount = 0;
				cachedRectangles.Clear();
				if (!frameData.IsSucceed) {
					return (0, null);
				}
				var clipboard = frameData.Clipboard;
				
				var usages = GetUsages(clipboard.UpdateCpuUsages, clipboard.RenderCpuUsages) ;
				var spacingParameters = timelineContent.AsyncSpacingParameters;
				
				long stopwatchFrequency = frameData.ProfiledFrame.StopwatchFrequency;
				long updateThreadStartTime = frameData.ProfiledFrame.UpdateThreadStartTime;
				
				var periods = GetPeriods(usages, updateThreadStartTime, stopwatchFrequency);
				var positions = new PeriodPositions(periods, spacingParameters, freeSpaceOfLinesGetter);
				
				float ticksPerMicrosecond = stopwatchFrequency / 1_000_000f;
				TimePeriod UsageToTimePeriod(CpuUsage usage) => new TimePeriod(
					startTime: (usage.StartTime - updateThreadStartTime) / ticksPerMicrosecond,
					finishTime: (usage.FinishTime - updateThreadStartTime) / ticksPerMicrosecond);
				ColorIndicesPack CreateColorsPack(CpuUsage usage, OwnersPool pool) {
					bool isFilterPassed = filter(usage, pool, frameData.Clipboard);
					if (Mode == TimelineMode.BatchBreakReasons) {
						var reasons = usage.Reason;
						if (isFilterPassed && (reasons & CpuUsage.Reasons.BatchRender) != 0) {
							uint data = (uint)usage.Reason & CpuUsage.BatchBreakReasons.BitMask;
							data >>= CpuUsage.BatchBreakReasons.StartBitIndex - BreakReasonPreservedBitsCount;
							return ColorIndicesPack.EachBitAsColor((byte)data);
						}
						return ColorIndicesPack.EachBitAsColor(BreakReasonGrayColorBit);
					} else {
						if (isFilterPassed) {
							uint reasonIndex = (uint)usage.Reason & CpuUsage.ReasonsBitMask;
							return ColorIndicesPack.SingleColor((byte)usageReasonToColors[reasonIndex]);
						}
						return ColorIndicesPack.SingleColor(UnselectedItemColorIndex);
					}
				}
				void CreateItem(CpuUsage usage, OwnersPool pool, Vector2 position) {
					var period = UsageToTimePeriod(usage);
					var item = new Item {
						TimePeriod = period,
						VerticalLocation = new Range {
							A = position.Y, 
							B = position.Y + spacingParameters.TimePeriodHeight
						},
						ColorIndicesPack = CreateColorsPack(usage, pool),
						UsageReason = usage.Reason
					};
					if (itemsCount >= items.Length) {
						Array.Resize(ref items, 2 * items.Length);
					}
					items[itemsCount++] = item;
					CreateRectanglesFor(item, position);
				}
				foreach (var (usage, position) in clipboard.UpdateCpuUsages.Zip(positions, Tuple.Create)) {
					CreateItem(usage, clipboard.UpdateOwnersPool, position);
				}
				foreach (var (usage, position) in clipboard.RenderCpuUsages.Zip(positions, Tuple.Create)) {
					CreateItem(usage, clipboard.RenderOwnersPool, position);
				}
				return (
					PeriodPositions.GetContentHeight(spacingParameters, freeSpaceOfLinesGetter()), 
					Mode == TimelineMode.Default ? 
						CpuTimelineColorSet.GetDefaultModeColors() : 
						CpuTimelineColorSet.GetBatchBreakReasonsColors());
			}

			private IEnumerable<CpuUsage> GetUsages(
				IEnumerable<CpuUsage> firstItems, 
				IEnumerable<CpuUsage> secondItems)
			{
				foreach (var usage in firstItems) {
					yield return usage;
				}
				foreach (var usage in secondItems) {
					yield return usage;
				}
			}

			public float RescaleItemsAsync()
			{
				cachedRectangles.Clear();
				var spacingParameters = timelineContent.AsyncSpacingParameters;
				var positions = new PeriodPositions(
					GetCachedPeriods(), 
					spacingParameters, 
					freeSpaceOfLinesGetter).GetEnumerator();
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
				return PeriodPositions.GetContentHeight(spacingParameters, freeSpaceOfLinesGetter());
			}

			private void CreateRectanglesFor(Item item, Vector2 position)
			{
				var spacingParameters = timelineContent.AsyncSpacingParameters;
				uint colorsCount = item.ColorIndicesPack.RasterizeTo(colorsPackRasterizationTarget);
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

			private IEnumerable<TimePeriod> GetPeriods(IEnumerable<CpuUsage> usages, long frameTimestamp, long frequency)
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
				public Range VerticalLocation;
				public ColorIndicesPack ColorIndicesPack;
				public CpuUsage.Reasons UsageReason;

				public ItemLabel CreateItemLabel() =>
					new ItemLabel(UsageReason, TimePeriod, VerticalLocation);
			}
		}

		internal struct ItemLabel : ITimelineItemLabel
		{
			private static readonly LabelInfo[] usageReasonNames;
			
			private readonly uint labelInfoIndex;
			
			public string Text => usageReasonNames[labelInfoIndex].Text;
		
			public float Width { get; }
		
			public TimePeriod Period { get; }
		
			public Range VerticalLocation { get; }

			static ItemLabel()
			{
				usageReasonNames = new LabelInfo[(uint)CpuUsage.Reasons.MaxReasonIndex + 1];
				var defaultFont = FontPool.Instance.DefaultFont;
				foreach (var reasonObject in Enum.GetValues(typeof(CpuUsage.Reasons))) {
					uint reason = (uint)reasonObject & CpuUsage.ReasonsBitMask;
					var text = ((CpuUsage.Reasons)reason).ToString();
					float textWidth = defaultFont.MeasureTextLine(text, TimelineLabel.FontHeight, 0.0f).X;
					usageReasonNames[reason] = new LabelInfo { Text = text, Width = textWidth };
				}
			}

			public ItemLabel(CpuUsage.Reasons usageReason, TimePeriod timePeriod, Range verticalLocation)
			{
				labelInfoIndex = (uint)usageReason & CpuUsage.ReasonsBitMask;
				Width = usageReasonNames[labelInfoIndex].Width;
				Period = timePeriod;
				VerticalLocation = verticalLocation;
			}
			
			private struct LabelInfo
			{
				public string Text;
				public float Width;
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
