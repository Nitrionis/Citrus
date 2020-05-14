using System;
using Yuzu;
using GpuCallInfo = Lime.Graphics.Platform.Profiling.GpuCallInfo;

namespace Lime
{
	public interface IMesh
	{
		IMesh ShallowClone();
#if !LIME_PROFILER
		void Draw(int startVertex, int vertexCount);
		void DrawIndexed(int startIndex, int indexCount, int baseVertex = 0);
#else
		void Draw(int startVertex, int vertexCount, GpuCallInfo profilingInfo);
		void DrawIndexed(int startIndex, int indexCount, int baseVertex, GpuCallInfo profilingInfo);
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
