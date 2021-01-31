#if PROFILER

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	using SpacingParameters = PeriodPositions.SpacingParameters;
	
	internal class TimelineContent
	{
		public delegate bool Filter<TUsage>(TUsage usage, FrameClipboard frameClipboard);
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
		/// Colors for timeline material. 
		/// </returns>
		/// <remarks>
		/// This method will not be called in parallel.
		/// Returns null if rebuilding was canceled.
		/// </remarks>
		Color4[] RebuildAsync(FrameDataResponse frameData, TimelineContent.Filter<TItem> filter);

		/// <summary>
		/// Updates elements locations.
		/// </summary>
		/// <remarks>
		/// This method will not be called in parallel.
		/// </remarks>
		void RescaleItemsAsync();
	}

	internal interface ITimelineItemLabel
	{
		/// <summary>
		/// Label caption.
		/// </summary>
		string Text { get; }
		
		/// <summary>
		/// Label width in pixels.
		/// </summary>
		float Width { get; }
		
		/// <summary>
		///  Time period of a timeline item.
		/// </summary>
		TimePeriod Period { get; }
		
		/// <summary>
		/// Defines the vertical location of an item, where a <= b.
		/// </summary>
		Range VerticalLocation { get; }
	}
	
	internal abstract class TimelineContent<TUsage, TLabel> : TimelineContent
		where TUsage : struct
		where TLabel : struct, ITimelineItemLabel
	{
		private long newestRebuildTaskId;
		private long newestRescaleTaskId;
		private readonly Func<long> actualRebuildIdGetter;
		private readonly Func<long> actualHitTestIdGetter;
		
		protected Task NewestContentModificationTask { get; private set; }
		
		protected long LastRequestedFrame { get; private set; }
		
		protected Filter<TUsage> LastRequestedFilter { get; private set; }
		
		public SpacingParameters SpacingParameters { get; private set; }
		
		protected TimelineContent(SpacingParameters spacingParameters)
		{
			SpacingParameters = spacingParameters;
			actualRebuildIdGetter = () => Interlocked.Read(ref newestRebuildTaskId);
			actualHitTestIdGetter = () => Interlocked.Read(ref newestRescaleTaskId);
			NewestContentModificationTask = Task.CompletedTask;
		}
		
		public abstract IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod);

		public abstract IEnumerable<TLabel> GetVisibleLabels(TimePeriod timePeriod);
		
		public abstract IEnumerable<TimelineHitTest.ItemInfo> GetHitTestTargets();

		public Task<Color4[]> RebuildAsync(long frameIndex, Task waitingTask, Filter<TUsage> filter)
		{
			LastRequestedFrame = frameIndex;
			LastRequestedFilter = filter;
			long currentTaskId = Interlocked.Increment(ref newestRebuildTaskId);
			var contentBuilder = GetContentBuilder();
			var frameProcessor = new AsyncFrameProcessor(
				Task.WhenAll(waitingTask, NewestContentModificationTask),
				currentTaskId,
				actualRebuildIdGetter,
				contentBuilder,
				filter);
			ProfilerTerminal.GetFrame(frameIndex, frameProcessor);
			NewestContentModificationTask = frameProcessor.Completed;
			return frameProcessor.Completed;
		}

		public Task SetSpacingParametersAsync(Task waitingTask, SpacingParameters spacingParameters)
		{
			long currentTaskId = Interlocked.Increment(ref newestRescaleTaskId);
			var previousContentModificationTask = NewestContentModificationTask;
			var contentBuilder = GetContentBuilder();
			NewestContentModificationTask = Task.Run(async () => {
				await waitingTask;
				await previousContentModificationTask;
				if (currentTaskId == actualHitTestIdGetter()) {
					SpacingParameters = spacingParameters;
					contentBuilder.RescaleItemsAsync();
				}
			});
			return NewestContentModificationTask;
		}
		
		protected abstract IAsyncContentBuilder<TUsage> GetContentBuilder();
		
		private class AsyncFrameProcessor : AsyncResponseProcessor<FrameDataResponse>
		{
			private readonly Task waitingTask;
			private readonly long selfRebuildId;
			private readonly Func<long> actualRebuildIdGetter;
			private readonly IAsyncContentBuilder<TUsage> contentBuilder;
			private readonly TaskCompletionSource<Color4[]> taskCompletionSource;
			private readonly TimelineContent.Filter<TUsage> filter;
			
			public Task<Color4[]> Completed => taskCompletionSource.Task;

			public AsyncFrameProcessor(
				Task waitingTask,
				long selfRebuildId,
				Func<long> actualRebuildIdGetter,
				IAsyncContentBuilder<TUsage> contentBuilder,
				TimelineContent.Filter<TUsage> filter)
			{
				this.waitingTask = waitingTask;
				this.contentBuilder = contentBuilder;
				this.selfRebuildId = selfRebuildId;
				this.actualRebuildIdGetter = actualRebuildIdGetter;
				this.filter = filter;
				taskCompletionSource = new TaskCompletionSource<Color4[]>(
					TaskCreationOptions.RunContinuationsAsynchronously);
			}
			
			protected sealed override async void ProcessResponseAsync(FrameDataResponse response)
			{
				if (waitingTask != null) {
					await waitingTask;
				}
				Color4[] colors = null;
				if (selfRebuildId == actualRebuildIdGetter()) {
					colors = contentBuilder.RebuildAsync(response, filter);
				}
				taskCompletionSource.SetResult(colors);
			}
		}
	}
}

#endif // PROFILER