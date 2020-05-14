using Lime.Graphics.Platform.Profiling;

namespace Lime.Graphics.Platform.OpenGL
{
	/// <summary>
	/// RenderGpuProfiler implementation for OpenGL.
	/// </summary>
	internal class RenderGpuProfiler : Profiling.RenderGpuProfiler
	{
		public override bool IsDeepProfiling { get => false; set { } }

		public void DrawCall(GpuCallInfo profilingInfo, int vertexCount, PrimitiveTopology topology)
		{
			if (isProfilingEnabled) {
				resultsBuffer.FullVerticesCount += vertexCount;
				int trianglesCount = CalculateTrianglesCount(vertexCount, topology);
				resultsBuffer.FullTrianglesCount += trianglesCount;
				if (profilingInfo.IsPartOfScene) {
					resultsBuffer.SceneDrawCallCount++;
					resultsBuffer.SceneVerticesCount += vertexCount;
					resultsBuffer.SceneTrianglesCount += trianglesCount;
				}
				resultsBuffer.FullDrawCallCount++;
			}
			profilingInfo.Free();
		}

		internal override void FrameRenderFinished()
		{
			resultsBuffer.IsDeepProfilingEnabled = false;
			resultsBuffer.IsCompleted = true;
			base.FrameRenderFinished();
		}
	}
}
