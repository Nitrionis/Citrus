using System;
using Lime;
using ProtoBuf;

namespace Lime
{
	/// <summary>
	/// This interface must implements every node which can be used as a source for ImageCombiner.
	/// </summary>
	public interface IImageCombinerArg
	{
		/// <summary>
		/// Called by ImageCombiner in update cycle.
		/// It notifies that widget will be used in combining, and
		/// must not be drawn on render pass.
		/// </summary>
		void SkipRender();

		ITexture GetTexture();

		Vector2 Size { get; }

		Color4 Color { get; }

		Matrix32 CalcLocalToParentTransform();
	
		bool GloballyVisible { get; }

		Blending Blending { get; }

		ShaderId Shader { get; }

		Matrix32 UVTransform { get; }
	}

	[ProtoContract]
	public class ImageCombiner : Node
	{
		[ProtoMember(1)]
		public bool Enabled { get; set; }

		[ProtoMember(2)]
		public Blending Blending { get; set; }

		[ProtoMember(3)]
		public ShaderId Shader { get; set; }

		public Material Material { get; set; }

		public ImageCombiner()
		{
			Enabled = true;
			Blending = Blending.Inherited;
			Shader = ShaderId.Diffuse;
		}

		static bool AreVectorsClockwiseOrdered(Vector2 u, Vector2 v, Vector2 w)
		{
			return (v.Y - u.Y) * (w.X - v.X) > (v.X - u.X) * (w.Y - v.Y);
		}

		bool GetArgs(out IImageCombinerArg arg1, out IImageCombinerArg arg2)
		{
			if (Parent != null) {
				int index = Parent.Nodes.IndexOf(this);
				if (index < Parent.Nodes.Count - 2) {
					arg1 = Parent.Nodes[index + 1] as IImageCombinerArg;
					arg2 = Parent.Nodes[index + 2] as IImageCombinerArg;
					if (arg1 != null & arg2 != null)
						return true;
				}
			}
			arg1 = arg2 = null;
			return false;
		}

		protected override void SelfLateUpdate(float delta)
		{
			IImageCombinerArg arg1, arg2;
			if (Enabled && GetArgs(out arg1, out arg2)) {
				arg1.SkipRender();
				arg2.SkipRender();
			}
		}

		public override void AddToRenderChain(RenderChain chain)
		{
			if (Enabled) {
				chain.Add(this, Layer);
			}
		}

		static readonly Vector2[] outVertices = new Vector2[64];

		private void ClipPolygonByLine(Vector2[] vertices, ref int numVertices, Vector2 a, Vector2 b)
		{
			const float Eps = 1e-5f;
			int numOutVertices = 0;
			for (int i = 0; i < numVertices; i++) {
				int j = (i < numVertices - 1) ? i + 1 : 0;
				Vector2 u = vertices[i];
				Vector2 v = vertices[j];

				float d1 = (u.Y - a.Y) * (b.X - a.X) - (u.X - a.X) * (b.Y - a.Y);
				float d2 = (v.Y - a.Y) * (b.X - a.X) - (v.X - a.X) * (b.Y - a.Y);

				int s1 = Math.Abs(d1) < Eps ? 0 : ((d1 < 0) ? -1 : 1);
				int s2 = Math.Abs(d2) < Eps ? 0 : ((d2 < 0) ? -1 : 1);

				// if the first point lies inside visible half-plane or on the line, then include it into list.
				if (s1 >= 0)
					outVertices[numOutVertices++] = u;
				// the line crosses the edge.
				if (s1 > 0 && s2 < 0 || s1 < 0 && s2 > 0) {
					float z = (v.X - u.X) * (b.Y - a.Y) - (v.Y - u.Y) * (b.X - a.X);
					float t = d1 / z;
					Vector2 p;
					p.X = u.X + (v.X - u.X) * t;
					p.Y = u.Y + (v.Y - u.Y) * t;
					outVertices[numOutVertices++] = p;
				}
			}
			if (numOutVertices < 3)
				numVertices = 0;
			else {
				for (int i = 0; i < numOutVertices; i++)
					vertices[i] = outVertices[i];
				numVertices = numOutVertices;
			}
		}

		static readonly Vector2[] coords = new Vector2[8];
		static readonly Vector2[] stencil = new Vector2[4];
		static readonly Vertex[] vertices = new Vertex[8];
		static readonly Vector2[] rect = new Vector2[4] { 
			new Vector2(0, 0), 
			new Vector2(1, 0), 
			new Vector2(1, 1), 
			new Vector2(0, 1) 
		};

		private void RenderHelper(IImageCombinerArg arg1, IImageCombinerArg arg2)
		{
			Matrix32 transform1 = Matrix32.Scaling(arg1.Size) * arg1.CalcLocalToParentTransform();
			Matrix32 transform2 = Matrix32.Scaling(arg2.Size) * arg2.CalcLocalToParentTransform();
			// source rectangle
			int numCoords = 4;
			for (int i = 0; i < 4; i++)
				coords[i] = rect[i] * transform1;
			for (int i = 0; i < 4; i++)
				stencil[i] = rect[i] * transform2;
			bool clockwiseOrder = AreVectorsClockwiseOrdered(stencil[0], stencil[1], stencil[2]);
			// clip invisible parts
			for (int i = 0; i < 4; i++) {
				int j = (i < 3) ? i + 1 : 0;
				Vector2 v1 = clockwiseOrder ? stencil[j] : stencil[i];
				Vector2 v2 = clockwiseOrder ? stencil[i] : stencil[j];
				ClipPolygonByLine(coords, ref numCoords, v1, v2);
			}
			if (numCoords < 3)
				return;
			// Эти матрицы переводят координаты вершин изображения в текстурные координаты.
			Matrix32 uvTransform1 = transform1.CalcInversed();
			Matrix32 uvTransform2 = transform2.CalcInversed();
			ITexture texture1 = arg1.GetTexture();
			ITexture texture2 = arg2.GetTexture();
			Color4 color = arg1.Color * arg2.Color * Parent.AsWidget.GlobalColor;
			for (int i = 0; i < numCoords; i++) {
				vertices[i].Pos = coords[i];
				vertices[i].Color = color;
				vertices[i].UV1 = coords[i] * uvTransform1 * arg1.UVTransform;
				vertices[i].UV2 = coords[i] * uvTransform2 * arg2.UVTransform;
			}
			Renderer.DrawTriangleFan(texture1, texture2, vertices, numCoords);
		}

		public override void Render()
		{
			if (Parent.AsWidget == null) {
				return;
			}
			IImageCombinerArg arg1, arg2;
			if (!GetArgs(out arg1, out arg2)) {
				return;
			}
			if (!arg1.GloballyVisible || !arg2.GloballyVisible) {
				return;
			}
			if (arg1.GetTexture() == null || arg2.GetTexture() == null) {
				return;
			}
			Renderer.Transform1 = Parent.AsWidget.LocalToWorldTransform;
			Renderer.Blending = Blending == Blending.Inherited ? Parent.AsWidget.GlobalBlending : Blending;
			if (arg2.Shader == ShaderId.Silhuette) {
				Renderer.Material = StandardMaterial.Silhouette;
			} else if (arg1.Shader == ShaderId.Silhuette) {
				Renderer.Material = StandardMaterial.Silhouette;
				Toolbox.Swap(ref arg1, ref arg2);
			} else {
				Renderer.Material = Material.FromShaderId(Shader, ifInherited: Parent.AsWidget.GlobalMaterial, ifCustom: Material);
			}
			RenderHelper(arg1, arg2);
		}
	}
}

