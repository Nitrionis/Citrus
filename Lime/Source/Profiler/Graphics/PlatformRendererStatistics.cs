#if PROFILER
namespace Lime.Profiler.Graphics
{
	internal struct PlatformRendererStatistics
	{
		public static PlatformRendererStatistics Instance;
		
		public int FullDrawCallsCount;
		public int SceneDrawCallsCount;
		public int FullVertexCount;
		public int SceneVertexCount;
		public int FullTrianglesCount;
		public int SceneTrianglesCount;
		
		public void UpdateFrameStatistics(PrimitiveTopology topology, int vertexCount)
		{
			int trianglesCount =
				vertexCount < 3 ? 0 : topology == PrimitiveTopology.TriangleStrip ? vertexCount - 2 : vertexCount / 3;
			FullDrawCallsCount += 1;
			FullVertexCount += vertexCount;
			FullTrianglesCount += trianglesCount;
			if (SceneRenderScope.IsInside) {
				SceneDrawCallsCount += 1;
				SceneVertexCount += vertexCount;
				SceneTrianglesCount += trianglesCount;
			}
		}
	}
}
#endif // PROFILER