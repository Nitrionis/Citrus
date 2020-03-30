using System;
using System.Net;

namespace Lime.Profilers.Contexts
{
	/// <summary>
	/// The server is the receiver and data visualizer.
	/// The server can only be an instance of the engine.
	/// </summary>
	public class ServerContext : NetworkContext
	{
		private readonly Network.Server server;

		public IPEndPoint LocalEndpoint => (IPEndPoint)server.Listener.LocalEndpoint;

		private class SparsedGpuHistory : Graphics.Platform.ProfilerHistory
		{
			public void Enqueue(Item item)
			{
				long index = item.FrameIndex % items.Length;
				items[index].Reset();
				items[index] = item;
				if (ProfiledFramesCount < item.FrameIndex + 1) {
					ProfiledFramesCount = item.FrameIndex + 1;
					LastFrame = item;
				}
			}
		}

		private SparsedGpuHistory sparsedGpuHistory;

		private class SparsedCpuHistory : CpuHistory
		{
			public void Enqueue(Item item)
			{
				long index = item.UpdateIndex % items.Length;
				items[index].Reset();
				items[index] = item;
				if (ProfiledUpdatesCount < item.UpdateIndex + 1) {
					ProfiledUpdatesCount = item.UpdateIndex + 1;
					LastUpdate = item;
				}
			}
		}

		private SparsedCpuHistory sparsedCpuHistory;

		public Action<Response> OnResponseReceived;

		private bool isProfilingEnabled;

		public override bool IsProfilingEnabled
		{
			get { return isProfilingEnabled; }
			set {
				if (IsConnected) {
					SendRequest(new ProfilerOptions {
						ProfilingEnabled = ProfilerOptions.StateOf(value)
					});
				}
			}
		}

		private bool isDrawCallsRenderTimeEnabled;

		public override bool IsDrawCallsRenderTimeEnabled
		{
			get { return isDrawCallsRenderTimeEnabled; }
			set {
				if (IsConnected) {
					SendRequest(new ProfilerOptions {
						DrawCallsRenderTimeEnabled = ProfilerOptions.StateOf(value)
					});
				}
			}
		}

		private bool isSceneOnlyDrawCallsRenderTime;

		public override bool IsSceneOnlyDrawCallsRenderTime
		{
			get { return isSceneOnlyDrawCallsRenderTime; }
			set {
				if (IsConnected) {
					SendRequest(new ProfilerOptions {
						SceneOnlyDrawCallsRenderTime = ProfilerOptions.StateOf(value)
					});
				}
			}
		}

		public ServerContext()
		{
			sparsedGpuHistory = new SparsedGpuHistory();
			sparsedCpuHistory = new SparsedCpuHistory();
			GpuHistory = sparsedGpuHistory;
			CpuHistory = sparsedCpuHistory;
			networkMember = server = new Network.Server();
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint) => server.TryLaunch(ipEndPoint);

		public override void LocalDeviceUpdateStarted()
		{
			Network.IItem item;
			while (!server.Received.IsEmpty && server.Received.TryDequeue(out item)) {
				if (item is Statistics statistics) {
					if (statistics.Frame != null) {
						sparsedGpuHistory.Enqueue(statistics.Frame);
					}
					if (statistics.Update != null) {
						sparsedCpuHistory.Enqueue(statistics.Update);
					}
					UpdateProfilerOptions(statistics);
					if (statistics.Response != null) {
						OnResponseReceived?.Invoke(statistics.Response);
					}
					Application.MainWindow.Invalidate();
				}
			}
			server.TryReceive();
		}

		public override void LocalDeviceFrameRenderCompleted()
		{
			
		}

		private void UpdateProfilerOptions(Statistics statistics)
		{
			if (statistics.Frame != null) {
				isProfilingEnabled = true;
				isDrawCallsRenderTimeEnabled = statistics.Frame.IsDeepProfilingEnabled;
				isSceneOnlyDrawCallsRenderTime = statistics.Frame.IsSceneOnlyDeepProfiling;
			}
			if (statistics.Options != null) {
				isProfilingEnabled =
					ProfilerOptions.StateToBool(statistics.Options.ProfilingEnabled);
				isDrawCallsRenderTimeEnabled =
					ProfilerOptions.StateToBool(statistics.Options.DrawCallsRenderTimeEnabled);
				isSceneOnlyDrawCallsRenderTime =
					ProfilerOptions.StateToBool(statistics.Options.SceneOnlyDrawCallsRenderTime);
			}
		}

		public override void Completed()
		{
			server.RequestClose();
			base.Completed();
		}

		private void SendRequest(ProfilerOptions options) =>
			server.SerializeAndSend(new Request { Options = options });
	}
}
