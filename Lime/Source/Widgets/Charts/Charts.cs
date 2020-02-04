using System;
using Lime.Graphics.Platform;

namespace Lime.Widgets.Charts
{
	/// <summary>
	/// Contains several сharts.
	/// </summary>
	public abstract class Charts : Widget
	{
		protected readonly ChartMaterial material;
		protected readonly Mesh<Vector3> mesh;
		protected readonly Vector3[] vertices;
		protected readonly Vector4[] colors;
		protected readonly int controlPointsSpacing;
		protected readonly int controlPointsCount;
		protected readonly int submeshVerticesCount;
		protected readonly int chartsMaxHeight;
		protected readonly int chartsVerticesOffset;

		protected bool isMeshUpdateRequired = false;

		/// <summary>
		/// Represents a user defined line.
		/// </summary>
		private struct Line
		{
			public const int VerticesCount = 6;

			/// <summary>
			/// Index of color in <see cref="colors"/>.
			/// </summary>
			public int ColorIndex;
			public Vector2 Start;
			public Vector2 End;

			public Line(Vector2 start, Vector2 end, int colorIndex)
			{
				Start = start;
				End = end;
				ColorIndex = colorIndex;
			}
		}

		private readonly Line[] userLines;

		/// <summary>
		/// Contains data of one chart.
		/// </summary>
		public class Chart
		{
			public bool IsVisible = false;

			/// <summary>
			/// Index of color in <see cref="colors"/>.
			/// </summary>
			public int ColorIndex;

			public float[] Points = null;
		}

		protected readonly Chart[] charts;

		/// <summary>
		/// Represents vertical slice of charts.
		/// </summary>
		public class Slice
		{
			/// <summary>
			/// Index in <see cref="Chart.Points"/>.
			/// </summary>
			public int Index;

			/// <summary>
			/// Value by Index for each chart.
			/// </summary>
			public float[] Points;
		}

		/// <summary>
		/// Called when you click on the charts.
		/// </summary>
		public Action<Slice> OnSliceSelected;

		public Color4 BackgroundColor = Color4.Black;

		/// <summary>
		/// Charts constructor parameters.
		/// </summary>
		public class Parameters
		{
			public int ChartsCount = 1;
			/// <summary>
			/// The horizontal distance between the control points in pixels.
			/// </summary>
			public int ControlPointsSpacing = 5;
			public int ControlPointsCount;
			public int UserLinesCount = 0;
			public int ChartHeight = 100;
			public Vector4[] Colors;
			public Action<Slice> OnSliceSelected;

			public Parameters(int controlPointsCount, Vector4[] colors)
			{
				ControlPointsCount = controlPointsCount;
				Colors = colors;
			}
		}

		public Charts(Parameters parameters)
		{
			chartsMaxHeight = parameters.ChartHeight;
			controlPointsCount = parameters.ControlPointsCount;
			controlPointsSpacing = parameters.ControlPointsSpacing;
			submeshVerticesCount = CalculateSubmeshVerticesCount(controlPointsCount);
			chartsVerticesOffset = Line.VerticesCount * parameters.UserLinesCount;
			userLines = new Line[parameters.UserLinesCount];

			Presenter = new ChartsPresenter(this);

			HitTestTarget = true;
			HitTestMethod = HitTestMethod.BoundingRect;
			Clicked += SendSlice;
			OnSliceSelected = parameters.OnSliceSelected;
			MinMaxSize = new Vector2((controlPointsCount - 1) * controlPointsSpacing, chartsMaxHeight);

			charts = new Chart[parameters.ChartsCount];
			vertices = new Vector3[chartsVerticesOffset + charts.Length * submeshVerticesCount];
			for (int j = chartsVerticesOffset, i = 0; i < charts.Length; i++) {
				var chart = new Chart();
				charts[i] = chart;
				chart.IsVisible = true;
				chart.ColorIndex = i + 1;
				chart.Points = new float[controlPointsCount];
				for (int k = 0; k < submeshVerticesCount; k++, j++) {
					int x = (k / 2) * controlPointsSpacing;
					vertices[j] = new Vector3(x, 0, chart.ColorIndex);
				}
			}
			material = new ChartMaterial() { Colors = colors };
			mesh = new Mesh<Vector3> {
				Indices = new ushort[] { 0 },
				Vertices = vertices,
				AttributeLocations = ChartMaterial.ShaderProgram.MeshAttribLocations
			};
		}

		private void SendSlice()
		{
			int index = Math.Min(controlPointsCount - 1,
				((int)LocalMousePosition().X + controlPointsSpacing / 2) / controlPointsSpacing);
			OnSliceSelected?.Invoke(GetSlice(index));
		}

		public Slice GetSlice(int index)
		{
			var values = new float[charts.Length];
			for (int i = 0; i < charts.Length; i++) {
				values[i] = charts[i].Points[index];
			}
			return new Slice { Points = values, Index = index };
		}

		public virtual void PushSlice(float[] points)
		{
			isMeshUpdateRequired = true;
			if (charts.Length != points.Length) {
				throw new InvalidOperationException("Wrong points count.");
			}
			int submeshIndex = 0;
			foreach (var submesh in charts) {
				Array.Copy(submesh.Points, 0, submesh.Points, 1, submesh.Points.Length - 1);
				submesh.Points[controlPointsCount - 1] = points[submeshIndex++];
			}
		}

		protected abstract void RecalculateVertices();

		protected abstract int CalculateSubmeshVerticesCount(int controlPointsCount);

		protected int GetActiveVerticesCount() =>
			chartsVerticesOffset + submeshVerticesCount * GetActiveChartsCount();

		protected int GetActiveChartsCount()
		{
			int activeCount = 0;
			foreach (var chart in charts) {
				activeCount += chart.IsVisible ? 1 : 0;
			}
			return activeCount;
		}

		/// <summary>
		/// Add custom horizontal or vertical line.
		/// </summary>
		/// <param name="lineIndex">Line index. The number of lines is set in the ChartGroup constructor.</param>
		/// <param name="colorIndex">Color index in <see cref="colors"/>.</param>
		public void SetLinePos(int lineIndex, Vector2 start, Vector2 end, int colorIndex = 0)
		{
			isMeshUpdateRequired = true;
			userLines[lineIndex] = new Line(start, end, colorIndex);
		}

		protected void UpdateUserLines(float scalingFactor)
		{
			for (int i = 0; i < userLines.Length; i++) {
				int offset = i * Line.VerticesCount;
				var s = userLines[i].Start;
				var e = userLines[i].End;
				int colorIndex = userLines[i].ColorIndex;
				s.Y *= scalingFactor;
				e.Y *= scalingFactor;
				vertices[offset + 0] = new Vector3(s.X,     chartsMaxHeight - s.Y,     colorIndex);
				vertices[offset + 1] = new Vector3(s.X,     chartsMaxHeight - e.Y - 1, colorIndex);
				vertices[offset + 2] = new Vector3(e.X + 1, chartsMaxHeight - e.Y - 1, colorIndex);
				vertices[offset + 3] = new Vector3(s.X,     chartsMaxHeight - s.Y,     colorIndex);
				vertices[offset + 4] = new Vector3(e.X + 1, chartsMaxHeight - e.Y - 1, colorIndex);
				vertices[offset + 5] = new Vector3(e.X + 1, chartsMaxHeight - s.Y,     colorIndex);
			}
		}

		/// <summary>
		/// Enables or disables a specific chart.
		/// </summary>
		public void SetActive(int chartIndex, bool value)
		{
			isMeshUpdateRequired = true;
			charts[chartIndex].IsVisible = value;
		}

		private class ChartsPresenter : IPresenter
		{
			private Charts charts;

			public ChartsPresenter(Charts charts) => this.charts = charts;

			public bool PartialHitTest(Node node, ref HitTestArgs args) => node.PartialHitTest(ref args);

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState((Widget)node);
				ro.Charts = charts;
				return ro;
			}

			public IPresenter Clone() => (IPresenter)MemberwiseClone();

			private class RenderObject : WidgetRenderObject
			{
				public Charts Charts;

				public override void Render()
				{
					PrepareRenderState();
					Renderer.DrawRect(Vector2.Zero, Charts.Size, Charts.BackgroundColor);
#if PROFILER_GPU
					Renderer.MainRenderList.Flush();
					if (Charts.isMeshUpdateRequired) {
						Charts.RecalculateVertices();
					}
					var material = Charts.material;
					material.Matrix = Renderer.FixupWVP((Matrix44)LocalToWorldTransform * Renderer.ViewProjection);
					material.Apply(0);
					var profilingInfo = ProfilingInfo.Acquire(material, 0);
					var mesh = Charts.mesh;
					mesh.Topology = PrimitiveTopology.TriangleStrip;
					mesh.Draw(
						startVertex: Line.VerticesCount * Charts.userLines.Length,
						vertexCount: Charts.GetActiveChartsCount() * Charts.submeshVerticesCount,
						profilingInfo);
					mesh.Topology = PrimitiveTopology.TriangleList;
					mesh.Draw(0, Line.VerticesCount * Charts.userLines.Length, profilingInfo);
#endif
				}
			}
		}
	}
}
