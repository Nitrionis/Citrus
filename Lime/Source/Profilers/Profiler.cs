using System;
using System.Collections.Generic;
using IPEndPoint = System.Net.IPEndPoint;
using Lime.Graphics.Platform;
using Yuzu;

namespace Lime.Profilers
{
	public class Profiler
	{
		private static Profiler instance;

		public enum OperatingMode
		{
			/// <summary>
			/// Used to profile the current instance of the engine.
			/// </summary>
			ThisInstance,

			/// <summary>
			/// The client is a data source for profiling.
			/// The client can be an instance of the engine or a game.
			/// Compiled game can be instance is always client.
			/// </summary>
			Client,

			/// <summary>
			/// The server is the receiver and data visualizer.
			/// The server can only be an instance of the engine.
			/// </summary>
			Server
		}

		public OperatingMode Mode { get; private set; }

		public bool IsRemoteProfilerConnected { get; private set; }

		private class Frame : Network.Item
		{
			[YuzuRequired]
			public bool IsEmpty { get; set; }

			[YuzuRequired]
			public bool IsCloseRequested { get; set; }

			[YuzuRequired]
			public ProfilerHistory.Item DrawingInfo;
		}

		/// <summary>
		/// Local or remote GPU profiler history.
		/// </summary>
		public static ProfilerHistory GpuHistory { get; private set; }

		/// <summary>
		/// If remote profiler connected the event invoked when frame received,
		/// otherwise it is invoked when frame render completed in this application instance.
		/// </summary>
		public static Action OnFrameCompleted;

		/// <summary>
		/// Invoked when the profiling mode changes as a result of connecting or disconnecting the remote profiler.
		/// </summary>
		public static Action OnProfilingModeChanged;

		private readonly Queue<ProfilerHistory.Item> unfinishedFrames;

		public Profiler()
		{
			if (instance != null) {
				throw new InvalidOperationException();
			}
			instance = this;
			unfinishedFrames = new Queue<ProfilerHistory.Item>();
			GpuHistory = PlatformProfiler.Instance;
			PlatformProfiler.Instance.OnFrameRenderCompleted = LocalDeviceFrameRenderCompleted;
		}

		private void LocalDeviceFrameRenderCompleted()
		{
			if (Mode == OperatingMode.ThisInstance) {
				OnFrameCompleted?.Invoke();
			} else if (Mode == OperatingMode.Client && IsRemoteProfilerConnected) {
				unfinishedFrames.Enqueue(GpuHistory.LastFrame);
				if (unfinishedFrames.Peek().IsDeepProfilingCompleted) {
					SerializeAndSendFrame(unfinishedFrames.Dequeue());
				}
			}
		}

		private void RemoteDeviceFrameRenderReceived()
		{
			if (Mode == OperatingMode.Server) {
				// todo do something
				OnFrameCompleted?.Invoke();
			}
		}

		private void ProfilingModeChanged()
		{
			unfinishedFrames.Clear();
			OnProfilingModeChanged?.Invoke();
		}

		private void SerializeAndSendFrame(ProfilerHistory.Item frame)
		{

		}
	}
}
