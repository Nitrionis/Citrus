#if PROFILER

using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	using SpacingParameters = PeriodPositions.SpacingParameters;
	using OwnersPool = RingPool<ReferenceTable.RowIndex>;

	internal struct EmptyData {}

	internal struct ContentChanges<T>
	{
		public bool IsTaskSkipped;
		public SpacingParameters SpacingParameters;
		public float ContentHeight;
		public T Value;
	}
	
	internal class TimelineContent
	{
		public delegate bool Filter<TUsage>(TUsage usage, OwnersPool pool, FrameClipboard frameClipboard);
	}
	
	internal interface IAsyncContentBuilder<TItem> where TItem : struct
	{
		/// <summary>
		/// Rebuilds content as part of some asynchronous task.
		/// </summary>
		/// <param name="frameData">
		/// Frame data that should not be accessed outside of
		/// this method. Otherwise we get undefined behavior.
		/// </param>
		/// <returns>
		/// Content height and colors for timeline material.
		/// </returns>
		/// <remarks>
		/// This method will not be called in parallel.
		/// Returns null if rebuilding was canceled.
		/// </remarks>
		(float, Color4[]) RebuildAsync(FrameDataResponse frameData, TimelineContent.Filter<TItem> filter);

		/// <summary>
		/// Updates elements locations.
		/// </summary>
		/// <returns>
		/// Content height.
		/// </returns>
		/// <remarks>
		/// This method will not be called in parallel.
		/// </remarks>
		float RescaleItemsAsync();
	}

	internal abstract class TimelineContent<TUsage, TLabel> : TimelineContent
		where TUsage : struct
		where TLabel : struct, ITimelineItemLabel
	{
		private long newestRebuildTaskId;
		private long newestRescaleTaskId;
		private readonly Func<long> actualRebuildIdGetter;
		private readonly Func<long> actualHitTestIdGetter;
		
		public Task NewestContentModificationTask { get; private set; }
		
		protected long LastRequestedFrame { get; private set; }
		
		protected Filter<TUsage> LastRequestedFilter { get; private set; }

		protected SpacingParameters LastRequestedSpacingParameters { get; private set; }
		
		private readonly Action<SpacingParameters> SpacingParametersSetter;
		
		/// <remarks>Data for asynchronous code.</remarks>
		public SpacingParameters AsyncSpacingParameters { get; private set; }
		
		protected TimelineContent(SpacingParameters spacingParameters)
		{
			AsyncSpacingParameters = spacingParameters;
			actualRebuildIdGetter = () => Interlocked.Read(ref newestRebuildTaskId);
			actualHitTestIdGetter = () => Interlocked.Read(ref newestRescaleTaskId);
			NewestContentModificationTask = Task.CompletedTask;
			SpacingParametersSetter = s => AsyncSpacingParameters = s;
		}
		
		public abstract IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod);

		public abstract IEnumerable<TLabel> GetVisibleLabels(TimePeriod timePeriod);
		
		public abstract IEnumerable<TimelineHitTest.ItemInfo> GetHitTestTargets();

		public Task<ContentChanges<Color4[]>> RebuildAsync(
			long frameIndex, 
			Task waitingTask, 
			Filter<TUsage> filter,
			SpacingParameters spacingParameters)
		{
			LastRequestedFrame = frameIndex;
			LastRequestedFilter = filter;
			LastRequestedSpacingParameters = spacingParameters;
			long currentTaskId = Interlocked.Increment(ref newestRebuildTaskId);
			var contentBuilder = GetContentBuilder();
			var frameProcessor = new AsyncFrameProcessor(
				Task.WhenAll(waitingTask, NewestContentModificationTask),
				new TaskIdInfo {
					SelfRebuildId = currentTaskId,
					ActualRebuildIdGetter = actualRebuildIdGetter
				},
				contentBuilder,
				filter,
				new SpacingInfo {
					SpacingParameters = spacingParameters,
					Setter = SpacingParametersSetter
				});
			ProfilerTerminal.GetFrame(frameIndex, frameProcessor);
			NewestContentModificationTask = frameProcessor.Completed;
			return frameProcessor.Completed;
		}

		public Task<ContentChanges<EmptyData>> SetSpacingParametersAsync(
			Task waitingTask, 
			SpacingParameters spacingParameters)
		{
			LastRequestedSpacingParameters = spacingParameters;
			long currentRebuildId = newestRebuildTaskId;
			long currentTaskId = Interlocked.Increment(ref newestRescaleTaskId);
			var previousContentModificationTask = NewestContentModificationTask;
			var contentBuilder = GetContentBuilder();
			var task = Task.Run(async () => {
				await waitingTask;
				await previousContentModificationTask;
				if (
					currentTaskId == actualHitTestIdGetter() &&
					currentRebuildId == actualRebuildIdGetter()
					) 
				{
					AsyncSpacingParameters = spacingParameters;
					contentBuilder.RescaleItemsAsync();
					return new ContentChanges<EmptyData> {
						IsTaskSkipped = false, 
						SpacingParameters = spacingParameters
					};
				}
				return new ContentChanges<EmptyData> {
					IsTaskSkipped = true
				};
			});
			NewestContentModificationTask = task;
			return task;
		}
		
		protected abstract IAsyncContentBuilder<TUsage> GetContentBuilder();
		
		private class AsyncFrameProcessor : AsyncResponseProcessor<FrameDataResponse>
		{
			private readonly Task waitingTask;
			private readonly TaskIdInfo idInfo;
			private readonly IAsyncContentBuilder<TUsage> contentBuilder;
			private readonly TimelineContent.Filter<TUsage> filter;
			private readonly SpacingInfo spacingInfo;
			private readonly TaskCompletionSource<ContentChanges<Color4[]>> taskCompletionSource;
			
			public Task<ContentChanges<Color4[]>> Completed => taskCompletionSource.Task;

			public AsyncFrameProcessor(
				Task waitingTask,
				TaskIdInfo idInfo,
				IAsyncContentBuilder<TUsage> contentBuilder,
				TimelineContent.Filter<TUsage> filter,
				SpacingInfo spacingInfo)
			{
				this.waitingTask = waitingTask;
				this.contentBuilder = contentBuilder;
				this.idInfo = idInfo;
				this.filter = filter;
				this.spacingInfo = spacingInfo;
				taskCompletionSource = new TaskCompletionSource<ContentChanges<Color4[]>>(
					TaskCreationOptions.RunContinuationsAsynchronously);
			}
			
			protected sealed override async void ProcessResponseAsync(FrameDataResponse response)
			{
				if (waitingTask != null) {
					await waitingTask;
				}
				spacingInfo.Setter(spacingInfo.SpacingParameters);
				float contentHeight = 0;
				Color4[] colors = null;
				if (idInfo.SelfRebuildId == idInfo.ActualRebuildIdGetter()) {
					(contentHeight, colors) = contentBuilder.RebuildAsync(response, filter);
				}
				taskCompletionSource.SetResult(new ContentChanges<Color4[]> {
					IsTaskSkipped = colors == null,
					SpacingParameters = spacingInfo.SpacingParameters,
					ContentHeight = contentHeight,
					Value = colors
				});
			}
		}
		
		private struct TaskIdInfo
		{
			public long SelfRebuildId;
			public Func<long> ActualRebuildIdGetter;
		}
		
		private struct SpacingInfo
		{
			public SpacingParameters SpacingParameters;
			public Action<SpacingParameters> Setter;
		}
	}
}

#endif // PROFILER