using GpuProfiler = Lime.Graphics.Platform.PlatformProfiler;

namespace Lime.Profilers.Contexts
{
	/// <summary>
	/// Used to profile the current instance of the engine.
	/// </summary>
	public class LocalContext : Context
	{
		public override bool IsProfilingEnabled
		{
			get => GpuProfiler.Instance.IsEnabled;
			set => GpuProfiler.Instance.IsEnabled = value;
		}

		public override bool IsDrawCallsRenderTimeEnabled
		{
			get => GpuProfiler.Instance.IsDeepProfiling;
			set => GpuProfiler.Instance.IsDeepProfiling = value;
		}

		public override bool IsSceneOnlyDrawCallsRenderTime
		{
			get => GpuProfiler.Instance.IsSceneOnlyDeepProfiling;
			set => GpuProfiler.Instance.IsSceneOnlyDeepProfiling = value;
		}

		public override void LocalDeviceFrameRenderCompleted() { }
	}
}
