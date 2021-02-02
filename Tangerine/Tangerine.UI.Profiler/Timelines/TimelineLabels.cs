#if PROFILER

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lime;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;

	internal static class TimelineLabel
	{
		public const float FontHeight = 20;
	}
	
	internal class TimelineLabels<TLabel> where TLabel : struct, ITimelineItemLabel
	{
		private readonly List<TLabel>[] labels;

		private int readTargetIndex;
		private int writeTargetIndex;
		private long newestTaskId;
		private Task<RebuildResponse> newestTask;
		private TaskCompletionSource<bool> taskCompletionSource;
		private RebuildRequest pendingRequest;

		public TimelineLabels()
		{
			labels = new List<TLabel>[2];
			for (int i = 0; i < labels.Length; i++) {
				labels[i] = new List<TLabel>();
			}
			readTargetIndex = 0;
			writeTargetIndex = 1;
			newestTask = Task.FromResult(new RebuildResponse {
				IsCanceled = true,
				WriteTargetIndex = writeTargetIndex
			});
		}
		
		public Task RebuildAsync(IEnumerable<TLabel> labels)
		{
			taskCompletionSource?.SetResult(true);
			taskCompletionSource = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			pendingRequest = new RebuildRequest {
				CurrentTaskId = Interlocked.Increment(ref newestTaskId),
				LabelsForCopy = labels,
				TaskCompletionSource = taskCompletionSource
			};
			return taskCompletionSource.Task;
		}
		
		public RenderObject GetRenderObject(TimePeriod visibleTimePeriod, float pixelsPerMicrosecond)
		{
			var ro = new RenderObject(this, visibleTimePeriod, pixelsPerMicrosecond);
			taskCompletionSource = null;
			return ro;
		}

		private struct RebuildRequest
		{
			public long CurrentTaskId;
			public IEnumerable<TLabel> LabelsForCopy;
			public TaskCompletionSource<bool> TaskCompletionSource;
		}

		private struct RebuildResponse
		{
			public bool IsCanceled;
			public int WriteTargetIndex;
		}
		
		public struct RenderObject
		{
			private readonly TimelineLabels<TLabel> container;
			private readonly TimePeriod visibleTimePeriod;
			private readonly float pixelsPerMicrosecond;
			private readonly RebuildRequest rebuildRequest;
			private readonly bool hasRequest;
			
			public RenderObject(
				TimelineLabels<TLabel> container,
				TimePeriod visibleTimePeriod,
				float pixelsPerMicrosecond)
			{
				this.container = container;
				this.visibleTimePeriod = visibleTimePeriod;
				this.pixelsPerMicrosecond = pixelsPerMicrosecond;
				hasRequest = container.taskCompletionSource != null;
				rebuildRequest = container.pendingRequest;
			}

			public void Render()
			{
				var newestTask = container.newestTask;
				if (
					newestTask != null && 
					newestTask.IsCompleted &&
					!newestTask.Result.IsCanceled
					) 
				{
					container.writeTargetIndex = container.readTargetIndex;
					container.readTargetIndex = newestTask.Result.WriteTargetIndex;
				}
				if (hasRequest) {
					var containerCopy = container;
					var request = rebuildRequest;
					container.newestTask = newestTask.ContinueWith(t => {
						int writeTargetIndex = t.Result.IsCanceled ? 
							t.Result.WriteTargetIndex : 1 - t.Result.WriteTargetIndex;
						bool isCanceled = 
							request.CurrentTaskId != Interlocked.Read(ref containerCopy.newestTaskId);
						if (!isCanceled) {
							var labels = containerCopy.labels[writeTargetIndex];
							labels.Clear();
							labels.AddRange(request.LabelsForCopy);
						}
						request.TaskCompletionSource.SetResult(true);
						return new RebuildResponse {
							IsCanceled = isCanceled,
							WriteTargetIndex = writeTargetIndex
						};
					});
				}
				foreach (var label in container.labels[container.readTargetIndex]) {
					var clampedPeriod = new TimePeriod {
						StartTime = Math.Max(label.Period.StartTime, visibleTimePeriod.StartTime),
						FinishTime = Math.Min(label.Period.FinishTime, visibleTimePeriod.FinishTime)
					};
					float timelineItemWidth = clampedPeriod.Duration * pixelsPerMicrosecond;
					if (timelineItemWidth > label.Width) {
						var position = new Vector2(
							x: clampedPeriod.StartTime * pixelsPerMicrosecond + 
							   0.5f * (timelineItemWidth - label.Width),
							y: label.VerticalLocation.A);
						Renderer.DrawTextLine(position, label.Text, TimelineLabel.FontHeight, Color4.White, 0);
					}
				}
			}
		}
	}
}

#endif // PROFILER