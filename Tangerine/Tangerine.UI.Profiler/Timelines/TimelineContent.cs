#if PROFILER

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	using SpacingParameters = PeriodPositions.SpacingParameters;
	
	internal interface IAsyncContentBuilder<UsageType> where UsageType : struct
	{
		/// <summary>
		/// Rebuilds content as part of some asynchronous task.
		/// </summary>
		/// <param name="frameData">
		/// Frame data that should not be accessed outside of
		/// this method. Otherwise we get undefined behavior.
		/// </param>
		/// <remarks>
		/// This method will not be called in parallel.
		/// </remarks>
		void RebuildAsync(FrameDataResponse frameData);

		/// <summary>
		/// Updates elements locations.
		/// </summary>
		/// <remarks>
		/// This method will not be called in parallel.
		/// </remarks>
		void RescaleItemsAsync();
	}

	internal abstract class TimelineContent
	{
		private Task newestContentModificationTask = Task.CompletedTask;
		private long newestRebuildTaskId;
		private long newestRescaleTaskId;
		private readonly Func<long> actualRebuildIdGetter;
		private readonly Func<long> actualHitTestIdGetter;
		
		public SpacingParameters SpacingParameters { get; private set; }
		
		protected TimelineContent(SpacingParameters spacingParameters)
		{
			SpacingParameters = spacingParameters;
			actualRebuildIdGetter = () => Interlocked.Read(ref newestRebuildTaskId);
			actualHitTestIdGetter = () => Interlocked.Read(ref newestRescaleTaskId);
		}
		
		public abstract IEnumerable<Rectangle> GetRectangles(TimePeriod timePeriod);
		
		public abstract IEnumerable<TimelineHitTest.ItemInfo> GetHitTestTargets();

		public Task RebuildAsync<TItem>(long frameIndex, Task waitingTask, Func<TItem, bool> filter) where TItem : struct
		{
			long currentTaskId = Interlocked.Increment(ref newestRebuildTaskId);
			var contentBuilder = GetContentBuilder();
			var frameProcessor = new AsyncFrameProcessor(
				Task.WhenAll(waitingTask, newestContentModificationTask),
				currentTaskId,
				actualRebuildIdGetter,
				contentBuilder);
			ProfilerTerminal.GetFrame(frameIndex, frameProcessor);
			newestContentModificationTask = frameProcessor.Completed;
			return newestContentModificationTask;
		}

		public Task SetSpacingParametersAsync(Task waitingTask, SpacingParameters spacingParameters)
		{
			long currentTaskId = Interlocked.Increment(ref newestRescaleTaskId);
			var previousContentModificationTask = newestContentModificationTask;
			var contentBuilder = GetContentBuilder();
			newestContentModificationTask = Task.Run(async () => {
				await waitingTask;
				await previousContentModificationTask;
				if (currentTaskId == actualHitTestIdGetter()) {
					SpacingParameters = spacingParameters;
					contentBuilder.RescaleItemsAsync();
				}
			});
			return newestContentModificationTask;
		}
		
		protected abstract IAsyncContentBuilder GetContentBuilder();
		
		private class AsyncFrameProcessor : AsyncResponseProcessor<FrameDataResponse>
		{
			private readonly Task waitingTask;
			private readonly long selfRebuildId;
			private readonly Func<long> actualRebuildIdGetter;
			private readonly IAsyncContentBuilder contentBuilder;
			private readonly TaskCompletionSource<bool> taskCompletionSource;

			public Task Completed => taskCompletionSource.Task;

			public AsyncFrameProcessor(
				Task waitingTask,
				long selfRebuildId,
				Func<long> actualRebuildIdGetter,
				IAsyncContentBuilder contentBuilder)
			{
				this.waitingTask = waitingTask;
				this.contentBuilder = contentBuilder;
				this.selfRebuildId = selfRebuildId;
				this.actualRebuildIdGetter = actualRebuildIdGetter;
				taskCompletionSource = new TaskCompletionSource<bool>(
					TaskCreationOptions.RunContinuationsAsynchronously);
			}
			
			protected sealed override async void ProcessResponseAsync(FrameDataResponse response)
			{
				if (waitingTask != null) {
					await waitingTask;
				}
				if (selfRebuildId == actualRebuildIdGetter()) {
					contentBuilder.RebuildAsync(response);
				}
				taskCompletionSource.SetResult(true);
			}
		}
	}
}

#endif // PROFILER