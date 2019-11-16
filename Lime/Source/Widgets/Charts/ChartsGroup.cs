using System;

namespace Lime
{
	public class ChartsGroupCreateInfo
	{
		public delegate void SliceSelectedAction(ChartsGroup.Slice slice);
		public SliceSelectedAction SliceSelected;

		public Vector4[] Colors = DefaultColors;

		public int NonChartsLinesCount = 0;
		public int ChartHeight = 100;
		public int ControlPointsCount;
		public int ChartsCount;

		public ChartsGroupCreateInfo(int chartsCount, int controlPointsCount, SliceSelectedAction sliceSelected)
		{
			ChartsCount = chartsCount;
			ControlPointsCount = controlPointsCount;
			SliceSelected = sliceSelected;
		}

		public static readonly Vector4[] DefaultColors = new Vector4[13] {
			Color4.White.ToVector4(),
			new Color4(76, 176, 80).ToVector4(), new Color4(33, 150, 243).ToVector4(), new Color4(254, 87, 34).ToVector4(),
			new Color4(255, 235, 60).ToVector4(), new Color4(0, 150, 136).ToVector4(), new Color4(165, 208, 97).ToVector4(),
			new Color4(62, 115, 181).ToVector4(), new Color4(234, 30, 99).ToVector4(), new Color4(96, 105, 139).ToVector4(),
			new Color4(121, 85, 71).ToVector4(), new Color4(0, 255, 0).ToVector4(), new Color4(255, 0, 0).ToVector4()
		};
	}

	public abstract class ChartsGroup : Widget
	{
		public const int ControlPointsSpacing = 5;

		public readonly ChartMaterial Material;
		public readonly Mesh<Vector3> Mesh;
		public readonly Vector3[] Vertices;
		public readonly Vector4[] Colors;

		public readonly int NonChartsLinesCount;
		public readonly int NonChartsLinesVerticesCount;

		protected readonly int chartMaxHeight;
		protected readonly int submeshVerticesCount;
		protected readonly int controlPointsCount;

		protected float chartMaxValue = 0;
		public float MaxValue { get => chartMaxValue; }

		private struct Line
		{
			public Vector2 start, end;
			public int colorIndex;
			public Line(Vector2 start, Vector2 end, int colorIndex)
			{
				this.start = start; this.end = end;
				this.colorIndex = colorIndex;
			}
		}
		private readonly Line[] nonChartsLines;

		public class Submesh
		{
			public bool Enable = false;
			public int ColorIndex = -1;
			public float[] Points = null;
		}
		protected readonly Submesh[] submeshes;

		public class Slice
		{
			public int Index;
			public float[] Values;
		}
		public readonly ChartsGroupCreateInfo.SliceSelectedAction SliceSelected;

		public ChartsGroup(ChartsGroupCreateInfo createInfo)
		{
			NonChartsLinesCount = createInfo.NonChartsLinesCount;
			NonChartsLinesVerticesCount = NonChartsLinesCount * 6;
			nonChartsLines = new Line[NonChartsLinesCount];

			chartMaxHeight = createInfo.ChartHeight;
			controlPointsCount = createInfo.ControlPointsCount;
			submeshVerticesCount = GetSubmeshVerticesCount(controlPointsCount);

			Layout = new VBoxLayout();
			Presenter = new ChartsPresenter(this);
			HitTestTarget = true;
			HitTestMethod = HitTestMethod.BoundingRect;
			MinMaxSize = new Vector2(controlPointsCount * ControlPointsSpacing, createInfo.ChartHeight);
			Clicked += SendSlice;
			SliceSelected = createInfo.SliceSelected;
			Vertices = new Vector3[NonChartsLinesVerticesCount + submeshVerticesCount * createInfo.ChartsCount];
			Colors = createInfo.Colors;
			Material = new ChartMaterial() { Colors = Colors };
			Mesh = new Mesh<Vector3> {
				Vertices = Vertices,
				AttributeLocations = ChartMaterial.ShaderProgram.MeshAttribLocations,
				DirtyFlags = MeshDirtyFlags.Vertices | MeshDirtyFlags.AttributeLocations
			};
			submeshes = new Submesh[createInfo.ChartsCount];
			for (int j = NonChartsLinesVerticesCount, i = 0; i < submeshes.Length; i++) {
				var submesh = new Submesh();
				submeshes[i] = submesh;
				submesh.Enable = true;
				submesh.ColorIndex = i + 1;
				submesh.Points = new float[controlPointsCount];
				for (int k = 0; k < submeshVerticesCount; k++, j++) {
					Vertices[j] = new Vector3(
						/* position X */ (k / 2) * ControlPointsSpacing,
						/* position Y */ (k % 2) * 0,
						/* color index */ i + 1);
				}
			}
		}

		private void SendSlice()
		{
			int controlPointIndex = Math.Min(controlPointsCount - 1,
				((int)LocalMousePosition().X + 2) / ControlPointsSpacing);
			float[] values = new float[submeshes.Length];
			for (int i = 0; i < submeshes.Length; i++) {
				values[i] = submeshes[i].Points[controlPointIndex];
			}
			SliceSelected(new Slice { Values = values, Index = controlPointIndex });
		}

		public Slice GetSlice(int index)
		{
			float[] values = new float[submeshes.Length];
			for (int i = 0; i < submeshes.Length; i++) {
				values[i] = submeshes[i].Points[index];
			}
			return new Slice { Values = values, Index = index };
		}

		public void PushControlPoints(float[] points)
		{
			int submeshIndex = 0;
			foreach (var submesh in submeshes) {
				for (int i = 0; i < controlPointsCount - 1; i++) {
					submesh.Points[i] = submesh.Points[i + 1];
				}
				float p = points[submeshIndex];
				submesh.Points[controlPointsCount - 1] = p;
				submeshIndex++;
			}
		}

		public void SetLinePos(int lineIndex, Vector2 start, Vector2 end, int colorIndex = 0)
		{
			nonChartsLines[lineIndex] = new Line(start, end, colorIndex);
		}

		public void UpdateNonChartsLines(float coef)
		{
			for (int i = 0; i < nonChartsLines.Length; i++) {
				int offset = i * 6;
				var s = nonChartsLines[i].start;
				var e = nonChartsLines[i].end;
				int ci = nonChartsLines[i].colorIndex;
				s.Y *= coef;
				e.Y *= coef;
				Vertices[offset + 0] = new Vector3(s.X, chartMaxHeight - s.Y, ci);
				Vertices[offset + 1] = new Vector3(s.X, chartMaxHeight - e.Y - 1, ci);
				Vertices[offset + 2] = new Vector3(e.X + 1, chartMaxHeight - e.Y - 1, ci);
				Vertices[offset + 3] = new Vector3(s.X, chartMaxHeight - s.Y, ci);
				Vertices[offset + 4] = new Vector3(e.X + 1, chartMaxHeight - e.Y - 1, ci);
				Vertices[offset + 5] = new Vector3(e.X + 1, chartMaxHeight - s.Y, ci);
			}
		}

		public void SetActive(int chartIndex, bool value) => submeshes[chartIndex].Enable = value;

		public int GetActiveChartsCount()
		{
			int activeCount = 0;
			foreach (var element in submeshes) {
				activeCount += element.Enable ? 1 : 0;
			}
			return activeCount;
		}

		public int GetActiveVerticesCount() => NonChartsLinesVerticesCount + submeshVerticesCount * GetActiveChartsCount();

		public abstract void UpdateVertices();
		protected abstract int GetSubmeshVerticesCount(int controlPointsCount);

		private class ChartsPresenter : IPresenter
		{
			private ChartsGroup chartsGroup;

			public ChartsPresenter(ChartsGroup chartsGroup)
			{
				this.chartsGroup = chartsGroup;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => node.PartialHitTest(ref args);

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var widget = (Widget)node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(widget);
				ro.chartsGroup = chartsGroup;
				return ro;
			}

			public IPresenter Clone() => (IPresenter)MemberwiseClone();

			private class RenderObject : WidgetRenderObject
			{
				public ChartsGroup chartsGroup;

				public override void Render()
				{
					Renderer.MainRenderList.Flush();

					var material = chartsGroup.Material;
					material.Matrix = Renderer.FixupWVP((Matrix44)LocalToWorldTransform * Renderer.ViewProjection);
					material.Apply(0);

					var mesh = chartsGroup.Mesh;
					mesh.Topology = PrimitiveTopology.TriangleStrip;
					mesh.Draw(chartsGroup.NonChartsLinesVerticesCount, chartsGroup.GetActiveChartsCount() * chartsGroup.submeshVerticesCount);
					mesh.Topology = PrimitiveTopology.TriangleList;
					mesh.Draw(0, chartsGroup.NonChartsLinesVerticesCount);
				}
			}
		}
	}
}
