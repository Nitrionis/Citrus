
namespace Lime.Graphics.Platform.OpenGL
{
	/// <summary>
	/// RenderGpuProfiler implementation for OpenGL.
	/// </summary>
	/// <remarks>
	/// Unable to get render call detail for OpenGL ES 2.0.
	/// </remarks>
	internal class RenderGpuProfiler : Platform.RenderGpuProfiler
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
			base.FrameRenderFinished();
		}
	}
}
