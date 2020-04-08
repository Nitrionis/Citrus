using System;
using Lime.Graphics.Platform;

namespace Lime.Widgets.Charts
{
	public abstract class ChartsContainer : Widget
	{
		protected readonly ChartMaterial material;
		protected readonly Mesh<Vector3> mesh;
		protected readonly Vector4[] colors;
		protected readonly int chartVerticesCount;
		protected readonly int chartsHeight;
		protected readonly int chartsVerticesOffset;
		public readonly int ControlPointsSpacing;
		public readonly int ControlPointsCount;

		protected bool isMeshUpdateRequired = false;
		protected float chartsMaxValue;

		public float ChartsMaxValue => chartsMaxValue;

		public Color4 BackgroundColor { get; set; }

		protected struct Line
		{
			public const int VerticesCount = 6;

			/// <summary>
			/// Index of color in <see cref="colors"/>.
			/// </summary>
			public int ColorIndex;
			public string Caption;
			public Vector2 Start;
			public Vector2 End;
			public Vector2 FinalStartPosition;

			public Line(Vector2 start, Vector2 end, int colorIndex, string caption)
			{
				Start = start;
				End = end;
				ColorIndex = colorIndex;
				Caption = caption;
				FinalStartPosition = Vector2.Zero;
			}
		}

		/// <remarks>
		/// Represents a user defined line.
		/// </remarks>
		protected readonly Line[] userLines;

		public class Chart
		{
			/// <summary>
			/// Index of color in <see cref="colors"/>.
			/// </summary>
			public int ColorIndex;
			public float[] Points = null;
			public bool IsVisible = false;
		}

		public readonly Chart[] Charts;

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
			public int Height = 100;
			public Color4[] Colors;

			/// <summary>
			/// Called when you click on the charts.
			/// </summary>
			public Action<Slice> SliceSelected;

			public Parameters(int controlPointsCount, Color4[] colors)
			{
				ControlPointsCount = controlPointsCount;
				Colors = colors;
			}
		}

		public ChartsContainer(Parameters parameters)
		{
			ControlPointsCount = parameters.ControlPointsCount;
			ControlPointsSpacing = parameters.ControlPointsSpacing;

			chartsHeight = parameters.Height;
			chartVerticesCount = CalculateSubmeshVerticesCount(ControlPointsCount);
			chartsVerticesOffset = Line.VerticesCount * parameters.UserLinesCount;

			Presenter = new ChartsPresenter(this);

			HitTestTarget = true;
			HitTestMethod = HitTestMethod.BoundingRect;
			Clicked += () => {
				int index = Math.Min(
					ControlPointsCount - 1,
					((int)LocalMousePosition().X + ControlPointsSpacing / 2) / ControlPointsSpacing);
				parameters.SliceSelected?.Invoke(GetSlice(index));
			};
			var size = new Vector2((ControlPointsCount - 1) * ControlPointsSpacing, chartsHeight);
			Size = size;
			MinMaxSize = size;

			colors = new Vector4[parameters.Colors.Length];
			for (int i = 0; i < colors.Length; i++) {
				colors[i] = parameters.Colors[i].ToVector4();
			}
			material = new ChartMaterial() { Colors = colors };
			Charts = new Chart[parameters.ChartsCount];
			mesh = new Mesh<Vector3> {
				Indices = new ushort[] { 0 },
				Vertices = new Vector3[chartsVerticesOffset + Charts.Length * chartVerticesCount],
				AttributeLocations = ChartMaterial.ShaderProgram.MeshAttribLocations
			};
			for (int j = chartsVerticesOffset, i = 0; i < Charts.Length; i++) {
				var chart = new Chart();
				Charts[i] = chart;
				chart.IsVisible = true;
				chart.ColorIndex = i;
				chart.Points = new float[ControlPointsCount];
				for (int k = 0; k < chartVerticesCount; k++, j++) {
					int x = (k / 2) * ControlPointsSpacing;
					mesh.Vertices[j] = new Vector3(x, 0, chart.ColorIndex);
				}
			}
			userLines = new Line[parameters.UserLinesCount];
		}

		/// <summary>
		/// Enables or disables a specific chart.
		/// </summary>
		public void SetActive(int chartIndex, bool value)
		{
			isMeshUpdateRequired = true;
			Charts[chartIndex].IsVisible = value;
		}

		public Slice GetSlice(int index)
		{
			var values = new float[Charts.Length];
			for (int i = 0; i < Charts.Length; i++) {
				values[i] = Charts[i].Points[index];
			}
			return new Slice { Points = values, Index = index };
		}

		public virtual void PushSlice(float[] points)
		{
			isMeshUpdateRequired = true;
			if (Charts.Length != points.Length) {
				throw new InvalidOperationException("Wrong points count.");
			}
			int submeshIndex = 0;
			foreach (var submesh in Charts) {
				Array.Copy(submesh.Points, 1, submesh.Points, 0, submesh.Points.Length - 1);
				submesh.Points[ControlPointsCount - 1] = points[submeshIndex++];
			}
		}

		public virtual void Reset()
		{
			foreach (var chart in Charts) {
				for (int i = 0; i < chart.Points.Length; i++) {
					chart.Points[i] = 0;
				}
			}
		}

		protected abstract void RecalculateVertices();

		protected abstract int CalculateSubmeshVerticesCount(int controlPointsCount);

		protected int GetActiveVerticesCount() =>
			chartsVerticesOffset + chartVerticesCount * GetActiveChartsCount();

		protected int GetActiveChartsCount()
		{
			int activeCount = 0;
			foreach (var chart in Charts) {
				activeCount += chart.IsVisible ? 1 : 0;
			}
			return activeCount;
		}

		/// <summary>
		/// Add custom horizontal or vertical line.
		/// </summary>
		/// <param name="lineIndex">Line index. The number of lines is set in the ChartGroup constructor.</param>
		/// <param name="colorIndex">Color index in <see cref="colors"/>.</param>
		public void SetLine(int lineIndex, Vector2 start, Vector2 end, int colorIndex = 0, string caption = null)
		{
			isMeshUpdateRequired = true;
			userLines[lineIndex] = new Line(start, end, colorIndex, caption);
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
				if (s.Y > chartsHeight && e.Y > chartsHeight) {
					s = Vector2.Zero;
					e = Vector2.Zero;
				}
				mesh.Vertices[offset + 0] = new Vector3(s.X,     chartsHeight - s.Y,     colorIndex);
				mesh.Vertices[offset + 1] = new Vector3(s.X,     chartsHeight - e.Y - 1, colorIndex);
				mesh.Vertices[offset + 2] = new Vector3(e.X + 1, chartsHeight - e.Y - 1, colorIndex);
				mesh.Vertices[offset + 3] = new Vector3(s.X,     chartsHeight - s.Y,     colorIndex);
				mesh.Vertices[offset + 4] = new Vector3(e.X + 1, chartsHeight - e.Y - 1, colorIndex);
				mesh.Vertices[offset + 5] = new Vector3(e.X + 1, chartsHeight - s.Y,     colorIndex);
				userLines[i].FinalStartPosition = s;
			}
		}

		private class ChartsPresenter : IPresenter
		{
			private ChartsContainer charts;

			public ChartsPresenter(ChartsContainer charts) => this.charts = charts;

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
				public ChartsContainer Charts;

				public override void Render()
				{
					PrepareRenderState();
					Renderer.DrawRect(Vector2.Zero, Charts.Size, Charts.BackgroundColor);
					Renderer.MainRenderList.Flush();
					if (Charts.isMeshUpdateRequired) {
						Charts.RecalculateVertices();
					}
					var material = Charts.material;
					material.Matrix = Renderer.FixupWVP((Matrix44)LocalToWorldTransform * Renderer.ViewProjection);
					material.Apply(0);
					var profilingInfo = GpuCallInfo.Acquire(material, 0);
					var mesh = Charts.mesh;
					mesh.Topology = PrimitiveTopology.TriangleStrip;
#if LIME_PROFILER
					mesh.Draw(
						startVertex: Line.VerticesCount * Charts.userLines.Length,
						vertexCount: Charts.GetActiveChartsCount() * Charts.chartVerticesCount,
						profilingInfo);
					mesh.Topology = PrimitiveTopology.TriangleList;
					mesh.Draw(0, Line.VerticesCount * Charts.userLines.Length, profilingInfo);
#else
					mesh.Draw(
						startVertex: Line.VerticesCount * Charts.userLines.Length,
						vertexCount: Charts.GetActiveChartsCount() * Charts.chartVerticesCount);
					mesh.Topology = PrimitiveTopology.TriangleList;
					mesh.Draw(0, Line.VerticesCount * Charts.userLines.Length);
#endif
					const float fontHeight = 14;
					foreach (var line in Charts.userLines) {
						if (line.Caption != null && line.FinalStartPosition.Y > fontHeight) {
							var startPosition = line.FinalStartPosition;
							startPosition.Y = Charts.Height - startPosition.Y;
							var color = Charts.colors[line.ColorIndex];
							Renderer.DrawTextLine(
								startPosition,
								line.Caption,
								fontHeight,
								color: Color4.FromFloats(color.X, color.Y, color.Z),
								letterSpacing: 0);
						}
					}
				}
			}
		}
	}
}
