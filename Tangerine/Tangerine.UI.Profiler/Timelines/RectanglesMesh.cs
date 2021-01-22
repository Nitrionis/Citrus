#if PROFILER

using System.Collections.Generic;
using Lime;
using Tangerine.UI.Charts;

namespace Tangerine.UI.Timelines
{
	internal class RectanglesMesh
	{
		private List<Mesh<Vector3>> meshes = new List<Mesh<Vector3>>();
		private List<Chunk> chunks = new List<Chunk>();

		public List<Chunk> Chunks
		{
			get { return chunks; }
			set {
				while (meshes.Count < value.Count) {
					meshes.Add(new Mesh<Vector3> {
						Indices = null,
						Vertices = null,
						Topology = PrimitiveTopology.TriangleList,
						AttributeLocations = ChartsMaterial.ShaderProgram.MeshAttribLocations,
					});
				}
				for (int i = 0; i < value.Count; i++) {
					var chunk = value[i];
					chunk.MeshDirtyFlags |= MeshDirtyFlags.VerticesIndices;
					chunks[i] = chunk;
				}
			}
		}

		public struct Chunk
		{
			public const int MaxVerticesCount = (65536 / Rectangle.VertexCount) * Rectangle.VertexCount;
			public const int MaxIndicesCount = (65536 / Rectangle.VertexCount) * Rectangle.IndexCount;

			public Vector3[] Vertices;
			public ushort[] Indices;
			public int VisibleRectanglesCount;
			public MeshDirtyFlags MeshDirtyFlags;
		}

		public void Draw()
		{
			for (int i = 0; i < chunks.Count; i++) {
				var chunk = chunks[i];
				var mesh = meshes[i];
				mesh.DirtyFlags = chunk.MeshDirtyFlags;
				mesh.Vertices = chunk.Vertices;
				mesh.Indices = chunk.Indices;
				mesh.DrawIndexed(0, Rectangle.IndexCount * chunk.VisibleRectanglesCount);
			}
		}
	}
}

#endif // PROFILER