using Lime;

namespace Tangerine.UI.Charts
{
	internal static class ChartsCommon
	{
		public class SwappableVertexStorage
		{
			private int currentBufferIndex;
			private Vector3[][] buffers;

			public Vector3[] Vertices { get; private set; }

			public SwappableVertexStorage(int buffersCapacity, int buffersCount = 2)
			{
				currentBufferIndex = 0;
				buffers = new Vector3[buffersCount][];
				for (int i = 0; i < buffersCount; i++) {
					buffers[i] = new Vector3[buffersCapacity];
				}
				Vertices = buffers[currentBufferIndex];
			}

			public void Swap()
			{
				currentBufferIndex = (currentBufferIndex + 1) % buffers.Length;
				Vertices = buffers[currentBufferIndex];
			}
		}

		public class Presenter : IPresenter
		{
			private readonly Mesh<Vector3> mesh;
			private readonly ChartsMaterial material;
			private readonly IChartsGroup charts;
			private readonly IChartsGroupMeshBuilder meshBuilder;

			public Presenter(IChartsGroup charts, IChartsGroupMeshBuilder meshBuilder)
			{
				this.charts = charts;
				this.meshBuilder = meshBuilder;
				mesh = new Mesh<Vector3> {
					Indices = new ushort[0],
					Vertices = null, // because the mesh is rebuilt in the update
					Topology = PrimitiveTopology.TriangleStrip,
					AttributeLocations = ChartsMaterial.ShaderProgram.MeshAttribLocations,
				};
				mesh.DirtyFlags &= ~MeshDirtyFlags.Indices;
				material = new ChartsMaterial();
				for (int i = 0; i < charts.Colors.Count; i++) {
					material.Colors[i] = charts.Colors[i].ToVector4();
				}
			}

			public RenderObject GetRenderObject(Node node)
			{
				if (meshBuilder.IsRebuildRequired) {
					meshBuilder.Rebuild();
				}
				var ro = RenderObjectPool<ChartsRenderObject>.Acquire();
				ro.CaptureRenderState(charts.Container);
				ro.Material = material;
				ro.Mesh = mesh;
				ro.Vertices = meshBuilder.Vertices;
				ro.FirstVisibleVertex = meshBuilder.FirstVisibleVertex;
				ro.VisibleVerticesCount = meshBuilder.VisibleVertexCount;
				ro.MeshDirtyFlags = meshBuilder.MeshDirtyFlags;
				ro.ExtraTransform = meshBuilder.ExtraTransform;
				meshBuilder.RenderObjectAcquired(ro);
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class ChartsRenderObject : WidgetRenderObject
			{
				public ChartsMaterial Material;
				public Mesh<Vector3> Mesh;
				public Vector3[] Vertices;
				public int FirstVisibleVertex;
				public int VisibleVerticesCount;
				public MeshDirtyFlags MeshDirtyFlags;
				public Matrix44 ExtraTransform;

				public override void Render()
				{
					Renderer.MainRenderList.Flush();
					PrepareRenderState();
					Material.Matrix = Renderer.FixupWVP(
						ExtraTransform * (Matrix44)LocalToWorldTransform * Renderer.ViewProjection);
					Material.Apply(0);
					Mesh.Vertices = Vertices;
					Mesh.DirtyFlags |= MeshDirtyFlags;
					Mesh.Draw(0, VisibleVerticesCount);
				}
			}
		}
	}
}
