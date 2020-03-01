using System;
using Yuzu;
using ProfilingInfo = Lime.Graphics.Platform.ProfilingInfo;

namespace Lime
{
	public interface IMesh
	{
		IMesh ShallowClone();
#if !PROFILER_GPU
		void Draw(int startVertex, int vertexCount);
		void DrawIndexed(int startIndex, int indexCount, int baseVertex = 0);
#else
		void Draw(int startVertex, int vertexCount, ProfilingInfo profilingInfo);
		void DrawIndexed(int startIndex, int indexCount, int baseVertex, ProfilingInfo profilingInfo);
#endif
	}

	public enum MeshDirtyFlags
	{
		None = 0,
		Vertices = 1 << 0,
		Indices = 1 << 1,
		AttributeLocations = 1 << 2,
		VerticesIndices = Vertices | Indices,
		All = Vertices | Indices | AttributeLocations,
	}
}
