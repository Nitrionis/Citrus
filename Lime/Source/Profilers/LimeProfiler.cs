using System;
using System.Threading;
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
			if (instance == null) {
				instance = new LimeProfiler();
			}
		}

		private static ProfilerContext nextContext;
		private static ProfilerContext currentContext;

		public static Action ContextChanged;
		public static Action LocalDeviceUpdateStarted;
		public static Action LocalDeviceFrameRenderCompleted;

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
			currentContext = new LocalContext();
			SetContext(currentContext);
			CpuProfiler.Instance.Updating = OnLocalDeviceUpdateStarted;
			GpuProfiler.Instance.FrameRenderCompleted = OnLocalDeviceFrameRenderCompleted;
		}

		public void OnLocalDeviceUpdateStarted()
		{
			if (nextContext != null) {
				currentContext?.Completed();
				currentContext = Interlocked.Exchange(ref nextContext, null);
				currentContext.Activated();
				ContextChanged?.Invoke();
			}
			currentContext.LocalDeviceUpdateStarted();
			LocalDeviceUpdateStarted?.Invoke();
		}

		public void OnLocalDeviceFrameRenderCompleted()
		{
			currentContext.LocalDeviceFrameRenderCompleted();
			LocalDeviceFrameRenderCompleted?.Invoke();
		}

		public static void SetContext(ProfilerContext context) => nextContext = context;
	}
}
