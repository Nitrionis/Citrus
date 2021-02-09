﻿#if PROFILER

using System.Threading.Tasks;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	internal class TimelineFramePreloader
	{
		private Task<FrameDataResponse> newestTask;

		private static ProfiledFrame InvalidFrame => new ProfiledFrame { Identifier = -1 };
		
		public bool IsAttemptCompleted
		{
			get
			{
				if (newestTask != null && newestTask.IsCompleted) {
					var data = newestTask.Result;
					if (data.IsSucceed) {
						Frame = newestTask.Result.ProfiledFrame;
					} else {
						Frame = InvalidFrame;
					}
					return true;
				}
				return false;
			}
		}

		public ProfiledFrame Frame { get; private set; } = InvalidFrame;

		public void Load(long frameIdentifier)
		{
			var processor = new AsyncFrameProcessor();
			ProfilerTerminal.GetFrame(frameIdentifier, processor);
			newestTask = processor.Completed;
		}

		private class AsyncFrameProcessor : AsyncResponseProcessor<FrameDataResponse>
		{
			private readonly TaskCompletionSource<FrameDataResponse> taskCompletionSource;
			
			public Task<FrameDataResponse> Completed => taskCompletionSource.Task;
			
			public AsyncFrameProcessor()
			{
				taskCompletionSource = new TaskCompletionSource<FrameDataResponse>(
					TaskCreationOptions.RunContinuationsAsynchronously);
			}
			
			protected sealed override void ProcessResponseAsync(FrameDataResponse response)
			{
				taskCompletionSource.SetResult(response);
			}
		}
	}
}

#endif // PROFILER