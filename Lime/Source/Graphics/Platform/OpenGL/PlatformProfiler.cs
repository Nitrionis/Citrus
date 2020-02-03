
namespace Lime.Graphics.Platform.OpenGL
{
	/// <summary>
	/// PlatformProfiler implementation for OpenGL.
	/// </summary>
	/// <remarks>
	/// Unable to get render call detail for OpenGL ES 2.0.
	/// </remarks>
	internal class PlatformProfiler : Platform.PlatformProfiler
	{
		public override bool IsDeepProfiling { get => false; set { } }

		public void DrawCall(ProfilingInfo profilingInfo, int vertexCount, PrimitiveTopology topology)
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
		}

		internal override void FrameRenderFinished()
		{
			if (isProfilingEnabled) {
				resultsBuffer.IsDeepProfilingEnabled = false;
			}
			base.FrameRenderFinished();
		}
	}
}
