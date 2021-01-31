#if PROFILER

using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	using SpacingParameters = PeriodPositions.SpacingParameters;

	internal class CpuTimelineContent : TimelineContent<CpuUsage>
	{
		private readonly AsyncContentBuilder contentBuilder;

		public CpuTimelineContent(SpacingParameters spacingParameters) : base(spacingParameters)
		{
			contentBuilder = new AsyncContentBuilder(this);
		}

		public override IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod) =>
			contentBuilder.GetRectangles(timePeriod);

		public override IEnumerable<ItemLabel> GetLabels(TimePeriod timePeriod) =>
			contentBuilder.GetLabels(timePeriod);
		
		public override IEnumerable<TimelineHitTest.ItemInfo> GetHitTestTargets() =>
			contentBuilder.GetHitTestTargets();

		protected override IAsyncContentBuilder<CpuUsage> GetContentBuilder() => contentBuilder;
		
		public Task RebuildAsync(Task waitingTask, TimelineMode mode)
		{
			var task = NewestContentModificationTask.ContinueWith(async (t) => {
				 await waitingTask;
				 contentBuilder.Mode = mode;
			});
			return RebuildAsync(LastRequestedFrame, task, LastRequestedFilter);
		}

		private class AsyncContentBuilder : IAsyncContentBuilder<CpuUsage>
		{
			private const int BreakReasonPreservedBitsCount = 1;
			private const int BreakReasonGrayColorBit = 1;
			private const int UnselectedItemColorIndex = 0;
		
			private static ColorTheme.ProfilerColors ProfilerColors;
		
			private static Color4[] defaultModeColors;
			private static Color4[] breakReasonsModeColors;
			private static LabelInfo[] usageReasonNames;
			
			private readonly TimelineContent<CpuUsage> timelineContent;
			private readonly List<Rectangle> cachedRectangles;
			private readonly uint[] colorsPackRasterizationTarget;
			private readonly uint[] usageReasonToColors;
			
			private Item[] items;
			private int itemsCount;
			private LabelInfo[] labels;
			private int labelsCount;

			public TimelineMode Mode { get; set; } = TimelineMode.Default;
			
			public AsyncContentBuilder(TimelineContent<CpuUsage> timelineContent)
			{
				this.timelineContent = timelineContent;
				ProfilerColors = ColorTheme.Current.Profiler;
				items = new Item[1];
				cachedRectangles = new List<Rectangle>();
				colorsPackRasterizationTarget = new uint[ColorsPack.MaxColorsCount];
				usageReasonToColors = new uint[(int) CpuUsage.Reasons.MaxReasonIndex + 1];
				FillUsageReasonToColors();
				usageReasonNames = new LabelInfo[(uint)CpuUsage.Reasons.MaxReasonIndex + 1];
				FillUsageReasonNames();
			}

			public IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod)
			{
				// todo optimize
				float microsecondsPerPixel = 
					timelineContent.SpacingParameters.MicrosecondsPerPixel;
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

			public IEnumerable<ItemLabel> GetLabels(TimePeriod timePeriod)
			{
				// todo optimize
				float microsecondsPerPixel = 
					timelineContent.SpacingParameters.MicrosecondsPerPixel;
				for (int i = 0; i < labelsCount; i++) {
					var labelInfo = labels[i];
					if (
						timePeriod.StartTime <= labelInfo.TimePeriod.FinishTime &&
						labelInfo.TimePeriod.StartTime <= timePeriod.FinishTime
						) 
					{
						var duration = labelInfo.TimePeriod.Duration;
						yield return new ItemLabel {
							Text = labelInfo.Text,
							TextWidth = labelInfo.TextWidth,
							ItemWidth = duration / microsecondsPerPixel,
							ItemCentralTimestamp = labelInfo.TimePeriod.StartTime + duration
						};
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

			public Color4[] RebuildAsync(FrameDataResponse frameData, Filter<CpuUsage> filter)
			{
				if (!frameData.IsSucceed) {
					return null;
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
					bool isFilterPassed = filter(usage, frameData.Clipboard);
					if (Mode == TimelineMode.BatchBreakReasons) {
						var reasons = usage.Reason;
						if (isFilterPassed && (reasons & CpuUsage.Reasons.BatchRender) != 0) {
							uint data = (uint)usage.Reason & CpuUsage.BatchBreakReasons.BitMask;
							data >>= CpuUsage.BatchBreakReasons.StartBitIndex - BreakReasonPreservedBitsCount;
							return ColorsPack.EachBitAsColor((byte)data);
						}
						return ColorsPack.EachBitAsColor(BreakReasonGrayColorBit);
					} else {
						if (isFilterPassed) {
							uint reasonIndex = (uint)(usage.Reason & CpuUsage.Reasons.ReasonsBitMask);
							return ColorsPack.SingleColor((byte)usageReasonToColors[reasonIndex]);
						}
						return ColorsPack.SingleColor(UnselectedItemColorIndex);
					}
				}
				
				foreach (var (usage, position) in usages.Zip(positions, Tuple.Create)) {
					var period = UsageToTimePeriod(usage);
					var item = new Item {
						TimePeriod = period,
						VerticalLocation = new Range {
							A = position.Y, 
							B = position.Y + spacingParameters.TimePeriodHeight
						},
						ColorsPack = CreateColorsPack(usage),
						UsageReason = usage.Reason
					};
					if (itemsCount >= items.Length) {
						Array.Resize(ref items, 2 * items.Length);
					}
					items[itemsCount++] = item;
					CreateRectanglesFor(item, position);
				}
				return Mode == TimelineMode.Default ? 
					GetDefaultModeColors() : GetBatchBreakReasonsColors();
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
				uint colorsCount = item.ColorsPack.RasterizeTo(colorsPackRasterizationTarget);
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

			private void FillUsageReasonNames()
			{
				foreach (var reasonObject in Enum.GetValues(typeof(CpuUsage.Reasons))) {
					uint reason = (uint)reasonObject & (uint)CpuUsage.Reasons.ReasonsBitMask;
					var text = ((CpuUsage.Reasons)reason).ToString();
					float textWidth = FontPool.Instance.DefaultFont.MeasureTextLine(text, 20, 0.0f).X;
					usageReasonNames[reason] = new LabelInfo { Text = text, TextWidth = textWidth };
				}
			}
			
			private void FillUsageReasonToColors()
			{
				var colors = GetDefaultModeColors();
				uint IndexOf(Color4 color) {
					int index = Array.IndexOf(colors, color);
					return (uint)(index != -1 ? index : throw new System.Exception("Profiler: Color not found!"));
				}
				for (int i = 0; i < usageReasonToColors.Length; i++) {
					usageReasonToColors[i] = IndexOf(Color4.Red);
				}
				usageReasonToColors[(int)CpuUsage.Reasons.FullUpdate] = 
					IndexOf(ProfilerColors.TimelineUnselectedTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.SyncBodyExecution] = 
					IndexOf(ProfilerColors.TimelineUnselectedTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.FullRender] = 
					IndexOf(ProfilerColors.TimelineUnselectedTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.NodeAnimation] = 
					IndexOf(ProfilerColors.TimelineAnimationTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.NodeUpdate] = 
					IndexOf(ProfilerColors.TimelineUpdateTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.NodeProcessor] = 
					IndexOf(ProfilerColors.TimelineUpdateTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.BehaviorComponentUpdate] = 
					IndexOf(ProfilerColors.TimelineUpdateTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.NodeDeserialization] = 
					IndexOf(ProfilerColors.TimelineDeserializationTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.LoadExternalScenes] = 
					IndexOf(ProfilerColors.TimelineDeserializationTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.NodeRenderPreparation] = 
					IndexOf(ProfilerColors.TimelineRenderPreparationTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.NodeRender] = 
					IndexOf(ProfilerColors.TimelineNodeRenderTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.BatchRender] = 
					IndexOf(ProfilerColors.TimelineBatchRenderTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.WaitForPreviousRendering] = 
					IndexOf(ProfilerColors.TimelineWaitTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.WaitForAcquiringSwapchainBuffer] = 
					IndexOf(ProfilerColors.TimelineWaitTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.ReferenceTableGarbageCollection] = 
					IndexOf(Color4.Red);
				usageReasonToColors[(int)CpuUsage.Reasons.ProfilerDatabaseResizing] = 
					IndexOf(Color4.Red);
				usageReasonToColors[(int)CpuUsage.Reasons.RunScheduledActions] = 
					IndexOf(ProfilerColors.TimelineRunPendingActionsTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.RunPendingActionsOnRendering] = 
					IndexOf(ProfilerColors.TimelineRunPendingActionsTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.AudioSystemUpdate] = 
					IndexOf(ProfilerColors.TimelineAudioSystemTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.IssueCommands] = 
					IndexOf(ProfilerColors.TimelineGestureTasks);
				usageReasonToColors[(int)CpuUsage.Reasons.ProcessCommands] = 
					IndexOf(ProfilerColors.TimelineGestureTasks);
			}
			
			private static Color4[] GetDefaultModeColors() => 
				(defaultModeColors = defaultModeColors ?? new[] {
					ProfilerColors.TimelineUnselectedTasks, 
					ProfilerColors.TimelineAnimationTasks,
					ProfilerColors.TimelineUpdateTasks, 
					ProfilerColors.TimelineGestureTasks,
					ProfilerColors.TimelineRenderPreparationTasks,
					ProfilerColors.TimelineNodeRenderTasks, 
					ProfilerColors.TimelineBatchRenderTasks,
					ProfilerColors.TimelineWaitTasks,
					ProfilerColors.TimelineAudioSystemTasks,
					ProfilerColors.TimelineDeserializationTasks,
					ProfilerColors.TimelineRunPendingActionsTasks,
					Color4.Red
				});
		
			private static Color4[] GetBatchBreakReasonsColors()
			{
				if (breakReasonsModeColors == null) {
					var colors = new Color4[ColorsPack.MaxColorsCount];
					colors[0] = ProfilerColors.TimelineUnselectedTasks;
					uint GetBitIndex(CpuUsage.Reasons reason) {
						uint data = (uint)reason & CpuUsage.BatchBreakReasons.BitMask;
						data >>= CpuUsage.BatchBreakReasons.StartBitIndex - BreakReasonPreservedBitsCount;
						return data;
					}
					colors[0] = ProfilerColors.TimelineUnselectedTasks;
					colors[GetBitIndex(CpuUsage.Reasons.BatchBreakNullLastBatch)] = 
						ProfilerColors.TimelineBatchBreakNullLastBatch;
					colors[GetBitIndex(CpuUsage.Reasons.BatchBreakDifferentMaterials)] = 
						ProfilerColors.TimelineBatchBreakDifferentMaterials;
					colors[GetBitIndex(CpuUsage.Reasons.BatchBreakMaterialPassCount)] = 
						ProfilerColors.TimelineBatchBreakMaterialPassCount;
					colors[GetBitIndex(CpuUsage.Reasons.BatchBreakVertexBufferOverflow)] = 
						ProfilerColors.TimelineBatchBreakVertexBufferOverflow;
					colors[GetBitIndex(CpuUsage.Reasons.BatchBreakIndexBufferOverflow)] = 
						ProfilerColors.TimelineBatchBreakIndexBufferOverflow;
					colors[GetBitIndex(CpuUsage.Reasons.BatchBreakDifferentAtlasOne)] = 
						ProfilerColors.TimelineBatchBreakDifferentAtlasOne;
					colors[GetBitIndex(CpuUsage.Reasons.BatchBreakDifferentAtlasTwo)] = 
						ProfilerColors.TimelineBatchBreakDifferentAtlasTwo;
					breakReasonsModeColors = colors;
				}
				return breakReasonsModeColors;
			}
			
			private struct Item
			{
				public TimePeriod TimePeriod;
				public ColorsPack ColorsPack;
				public Range VerticalLocation;
				public CpuUsage.Reasons UsageReason;
			}

			public struct ColorsPack
			{
				public const int MaxColorsCount = 8;

				private const uint BehaviorBitMask = 1u << MaxColorsCount;
			
				private uint InternalData;

				/// <summary>
				/// Interprets structure data as a set of colors.
				/// </summary>
				/// <param name="rasterizationTarget">
				/// An array into which the color indices will be written.
				/// </param>
				/// <returns>
				/// Number of colors in the pack.
				/// </returns>
				public uint RasterizeTo(uint[] rasterizationTarget)
				{
					uint colorSlotIndex = 0;
					if ((InternalData & BehaviorBitMask) != 0) {
						uint dataCopy = InternalData;
						for (uint i = 0; i < MaxColorsCount; i++, dataCopy <<= 1) {
							uint bit = dataCopy & 1u;
							rasterizationTarget[colorSlotIndex] = i * bit;
							colorSlotIndex += bit;
						}
						return colorSlotIndex;
					} else {
						return InternalData;
					}
				}

				public static ColorsPack SingleColor(byte colorIndex) => 
					new ColorsPack { InternalData = colorIndex };
			
				public static ColorsPack EachBitAsColor(byte colors)
				{
					var pack = new ColorsPack();
					pack.InternalData |= colors;
					pack.InternalData |= BehaviorBitMask;
					return pack;
				}
			}
			
			private struct LabelInfo
			{
				public string Text;
				public float TextWidth;
				public TimePeriod TimePeriod;
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
