using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;

namespace Lime.Profilers.Contexts
{
	public abstract class Context
	{
		public bool IsActiveContext { get; private set; }

		public GpuHistory GpuHistory { get; protected set; }
		public CpuHistory CpuHistory { get; protected set; }

		public abstract bool IsProfilingEnabled { get; set; }
		public abstract bool IsDrawCallsRenderTimeEnabled { get; set; }
		public abstract bool IsSceneOnlyDrawCallsRenderTime { get; set; }

		public abstract void LocalDeviceUpdateStarted();
		public abstract void LocalDeviceFrameRenderCompleted();

		/// <summary>
		/// Called when the context connected to the profiler.
		/// </summary>
		public virtual void Activated() => IsActiveContext = true;

		/// <summary>
		/// Called when the context is disconnected from the profiler.
		/// </summary>
		public virtual void Completed() => IsActiveContext = false;
	}
}
