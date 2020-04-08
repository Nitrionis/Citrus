using System;
using System.Threading;
using Lime.Profilers.Contexts;
using ProfilerContext = Lime.Profilers.Contexts.Context;
using GpuHistory = Lime.Graphics.Platform.GpuHistory;
using GpuProfiler = Lime.Graphics.Platform.RenderGpuProfiler;

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
		public static ProfilerContext CurrentContext { get; private set; }

		public static Action ContextChanged;
		public static Action LocalDeviceUpdateStarted;
		public static Action LocalDeviceFrameRenderCompleted;

		public static GpuHistory GpuHistory { get => CurrentContext.GpuHistory; }
		public static CpuHistory CpuHistory { get => CurrentContext.CpuHistory; }

		public static bool IsProfilingEnabled
		{
			get => CurrentContext.IsProfilingEnabled;
			set => CurrentContext.IsProfilingEnabled = value;
		}

		public static bool IsDrawCallsRenderTimeEnabled
		{
			get => CurrentContext.IsDrawCallsRenderTimeEnabled;
			set => CurrentContext.IsDrawCallsRenderTimeEnabled = value;
		}

		public static bool IsSceneOnlyDrawCallsRenderTime
		{
			get => CurrentContext.IsSceneOnlyDrawCallsRenderTime;
			set => CurrentContext.IsSceneOnlyDrawCallsRenderTime = value;
		}

		private LimeProfiler()
		{
			CurrentContext = new LocalContext();
			SetContext(CurrentContext);
			CpuProfiler.Instance.Updating = OnLocalDeviceUpdateStarted;
			GpuProfiler.Instance.FrameRenderCompleted += OnLocalDeviceFrameRenderCompleted;
		}

		public void OnLocalDeviceUpdateStarted()
		{
			if (nextContext != null) {
				CurrentContext?.Completed();
				CurrentContext = Interlocked.Exchange(ref nextContext, null);
				CurrentContext.Activated();
				ContextChanged?.Invoke();
			}
			CurrentContext.LocalDeviceUpdateStarted();
			LocalDeviceUpdateStarted?.Invoke();
		}

		public void OnLocalDeviceFrameRenderCompleted()
		{
			CurrentContext.LocalDeviceFrameRenderCompleted();
			LocalDeviceFrameRenderCompleted?.Invoke();
		}

		public static void SetContext(ProfilerContext context) => nextContext = context;
	}
}
