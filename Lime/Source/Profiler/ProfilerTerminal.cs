#if PROFILER

using System;
using Lime.Profiler.Contexts;

namespace Lime.Profiler
{
	/// <summary>
	/// Use to access profiler data.
	/// </summary>
	public static class ProfilerTerminal
	{
		static ProfilerTerminal()
		{
			Context = new NativeContext();
		}
		
		/// <summary>
		/// Used for context switching.
		/// </summary>
		public static IProfilerContext Context
		{
			get { return ProfilerDatabase.Context; }
			set {
				ProfilerDatabase.Context = value;
				value.FrameProfilingFinished += f => FrameProfilingFinished?.Invoke(f);
				value.ProfilerOptionsReceived += WatchProfilerOptions;
			}
		}

		public static void GetFrame(long frameIdentifier, AsyncResponseProcessor<FrameDataResponse> processor) =>
			Context.RunRequest(new FrameDataRequest(frameIdentifier, processor));

		public static void SelectTime(string filter, AsyncResponseProcessor<ObjectsSummaryResponse> processor) => 
			Context.RunRequest(new ObjectsSummaryRequest(filter, processor));

		private static bool cachedProfilerEnabled;

		/// <summary>
		/// Allows you to enable or disable the profiler.
		/// </summary>
		public static bool ProfilerEnabled
		{
			get => cachedProfilerEnabled;
			set => Context?.RunRequest(new ChangeProfilerEnabled(value));
		}

		private static bool cachedBatchBreakReasons;

		/// <summary>
		/// If requested, the profiler will collect batch break reasons.
		/// </summary>
		public static bool BatchBreakReasonsRequired
		{
			get => cachedBatchBreakReasons;
			set => Context?.RunRequest(new ChangeBatchBreakReasonsRequired(value));
		}

		/// <summary>
		/// Invoked when profiling of a frame is completed.
		/// </summary>
		/// <remarks>
		/// Only the main application window is involved in profiling.<para/>
		/// For each returned frame <see cref="ProfiledFrame.Identifier"/> >
		/// previous returned frame <see cref="ProfiledFrame.Identifier"/>.<para/>
		/// The order will be out of order when the context changes.<para/>
		/// </remarks>
		public static event Action<ProfiledFrame> FrameProfilingFinished;

		/// <summary>
		/// Indicates whether the scene update is frozen.
		/// </summary>
		public static bool IsSceneUpdateFrozen { get; private set; }

		/// <summary>
		/// Use to change <see cref="IsSceneUpdateFrozen"/>.
		/// </summary>
		public static void SetSceneUpdateFrozen(UpdateSkipOptions options) =>
			Context?.RunRequest(new ChangeSceneUpdateFrozen(options));

		private static bool cachedOverdrawEnabled;
		
		/// <summary>
		/// Allows you to enable or disable the Overdraw mode.
		/// </summary>
		public static bool OverdrawEnabled
		{
			get => cachedOverdrawEnabled;
			set => Context?.RunRequest(new ChangeOverdrawEnabled(value));
		}
		
		private static void WatchProfilerOptions(ProfilerOptions options)
		{
			cachedProfilerEnabled = options.ProfilerEnabled;
			cachedBatchBreakReasons = options.BatchBreakReasonsRequired;
			IsSceneUpdateFrozen = options.IsSceneUpdateFrozen;
			cachedOverdrawEnabled = options.OverdrawModeEnabled;
		}
	}
}

#endif // PROFILER
