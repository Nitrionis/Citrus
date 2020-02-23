using System;
using Lime.Profilers.Contexts;
using ProfilerContext = Lime.Profilers.Contexts.Context;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;
using GpuProfiler = Lime.Graphics.Platform.PlatformProfiler;

namespace Lime.Profilers
{
	public class LimeProfiler
	{
		private static LimeProfiler instance;

		public static void Initialize()
		{
			if (instance != null) {
				instance = new LimeProfiler();
			}
		}

		private static ProfilerContext nextContext;
		private static ProfilerContext currentContext;

		public static Action OnLocalDeviceFrameRenderCompleted;

		public static GpuHistory GpuHistory { get => currentContext.GpuHistory; }
		public static CpuHistory CpuHistory { get => currentContext.CpuHistory; }

		public static bool IsProfilingEnabled
		{
			get => currentContext.IsProfilingEnabled;
			set => currentContext.IsProfilingEnabled = value;
		}

		public static bool IsDrawCallsRenderTimeEnabled
		{
			get => currentContext.IsDrawCallsRenderTimeEnabled;
			set => currentContext.IsDrawCallsRenderTimeEnabled = value;
		}

		public static bool IsSceneOnlyDrawCallsRenderTime
		{
			get => currentContext.IsSceneOnlyDrawCallsRenderTime;
			set => currentContext.IsSceneOnlyDrawCallsRenderTime = value;
		}

		private LimeProfiler()
		{
			SetContext(new LocalContext());
			GpuProfiler.Instance.OnFrameRenderCompleted = LocalDeviceFrameRenderCompleted;
		}

		public void LocalDeviceFrameRenderCompleted()
		{
			if (nextContext != null) {
				currentContext?.Completed();
				currentContext = nextContext;
				nextContext = null;
				currentContext.Activated();
			}
			currentContext.LocalDeviceFrameRenderCompleted();
			OnLocalDeviceFrameRenderCompleted?.Invoke();
		}

		public static void SetContext(ProfilerContext context) => nextContext = context;
	}
}
