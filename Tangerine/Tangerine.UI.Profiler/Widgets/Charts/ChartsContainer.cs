using System;
using Lime;
using Lime.Graphics.Platform.Profiling;

namespace Tangerine.UI.Charts
{
	internal abstract class ChartsContainer : Widget
	{
		protected readonly int chartVerticesCount;
		protected readonly int chartsVerticesOffset;
		protected readonly int controlPointsCount;
		public readonly int ControlPointsSpacing;

		public int ControlPointsCount => controlPointsCount - 1;

		protected bool isMeshUpdateRequired = false;
		protected bool hasPreviousUpdate = false;
		protected float chartsMaxValue;

		/// <summary>
		/// The maximum value of the charts calculated during the previous update.
		/// </summary>
		public float ChartsMaxValue => chartsMaxValue;

		/// <summary>
		/// To calculate the scaling factor, the charts are based on the results of the previous frame.
		/// If AutoInvalidate disabled updating of data in the charts will not cause a redraw of the window.
		/// If rendering stops, we will see scaling from the previous frame.
		/// </summary>
		public bool AutoInvalidate { get; set; } = true;

		/// <summary>
		/// Background color for charts.
		/// </summary>
		public Color4 BackgroundColor { get; set; }

		public class Line
		{
			public const int VerticesCount = 6;

			/// <summary>
			/// Index of color in <see cref="colors"/>.
			/// </summary>
			public readonly int ColorIndex;
			public readonly Vector2 Start;
			public readonly Vector2 End;
			public readonly string Caption;

			/// <summary>
			/// Indicates whether the line will scale along with the chart.
			/// </summary>
			public bool IsScalable { get; set; } = true;

			/// <summary>
			/// Start position after applying scaling. Calculated automatically.
			/// </summary>
			public Vector2 CaptionPosition = Vector2.Zero;

			public Line(Vector2 start, Vector2 end, int colorIndex, string caption = null)
			{
				Start       = start;
				End         = end;
				ColorIndex  = colorIndex;
				Caption     = caption;
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
			public bool IsVisible = false;
			public FixedCapacityQueue<float> Points = null;
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
			/// <summary>
			/// The horizontal distance between the control points in pixels.
			/// </summary>
			public int ControlPointsSpacing = 5;
			public int ControlPointsCount;
			public int ChartsCount = 1;
			public int UserLinesCount = 0;
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
			controlPointsCount = 1 + parameters.ControlPointsCount;
			ControlPointsSpacing = parameters.ControlPointsSpacing;

			chartVerticesCount = CalculateSubmeshVerticesCount(controlPointsCount - 1);
			chartsVerticesOffset = Line.VerticesCount * parameters.UserLinesCount;

			float width = (controlPointsCount - 2) * ControlPointsSpacing;
			Width = width;
			MinMaxWidth = width;

			Charts = new Chart[parameters.ChartsCount];
			for (int i = 0; i < Charts.Length; i++) {
				var chart = new Chart();
				Charts[i] = chart;
				chart.IsVisible = true;
				chart.ColorIndex = i;
				chart.Points = new FixedCapacityQueue<float>(controlPointsCount);
			}
			userLines = new Line[parameters.UserLinesCount];
			for (int i = 0; i < userLines.Length; i++) {
				userLines[i] = new Line(Vector2.Zero, Vector2.Zero, 0, null);
			}

			Presenter = new ChartsPresenter(parameters.Colors, this);

			AddNode(new Widget {
				Anchors = Anchors.LeftRightTopBottom,
				HitTestTarget = true,
				HitTestMethod = HitTestMethod.BoundingRect,
				Width = width,
				MinMaxWidth = width,
				Clicked = () => {
					int index = Math.Min(
						controlPointsCount - 2,
						((int)LocalMousePosition().X + ControlPointsSpacing / 2) / ControlPointsSpacing);
					parameters.SliceSelected?.Invoke(GetSlice(index));
				}
			});
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
				values[i] = Charts[i].Points.GetItem(index + 1);
			}
			return new Slice { Points = values, Index = index };
		}

		public virtual void PushSlice(float[] points)
		{
			isMeshUpdateRequired = true;
			int submeshIndex = 0;
			foreach (var submesh in Charts) {
				submesh.Points.Enqueue(points[submeshIndex++]);
			}
		}

		public virtual void Reset()
		{
			foreach (var chart in Charts) {
				for (int i = 0; i < chart.Points.Capacity; i++) {
					chart.Points[i] = 0;
				}
			}
		}

		protected abstract void RebuildMesh(Mesh<Vector3> mesh);

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
		public void SetLine(int lineIndex, Line line)
		{
			isMeshUpdateRequired = true;
			userLines[lineIndex] = line;
		}

		protected void RecalculateUserLines(Mesh<Vector3> mesh, float scalingFactor)
		{
			for (int i = 0; i < userLines.Length; i++) {
				int offset = i * Line.VerticesCount;
				var line = userLines[i];
				var s = line.Start;
				var e = line.End;
				int colorIndex = line.ColorIndex;
				scalingFactor = line.IsScalable ? scalingFactor : 1;
				s.Y *= scalingFactor;
				e.Y *= scalingFactor;
				if (s.Y > Height && e.Y > Height) {
					s = Vector2.Zero;
					e = Vector2.Zero;
				}
				mesh.Vertices[offset + 0] = new Vector3(s.X, Height - s.Y, colorIndex);
				mesh.Vertices[offset + 1] = new Vector3(s.X, Height - e.Y - 1, colorIndex);
				mesh.Vertices[offset + 2] = new Vector3(e.X + 1, Height - e.Y - 1, colorIndex);
				mesh.Vertices[offset + 3] = new Vector3(s.X, Height - s.Y, colorIndex);
				mesh.Vertices[offset + 4] = new Vector3(e.X + 1, Height - e.Y - 1, colorIndex);
				mesh.Vertices[offset + 5] = new Vector3(e.X + 1, Height - s.Y, colorIndex);
				line.CaptionPosition = s;
			}
		}

		private class ChartsPresenter : IPresenter
		{
			private readonly Color4[] colors;
			private readonly Mesh<Vector3> mesh;
			private readonly ChartMaterial material;
			private readonly ChartsContainer container;

			public ChartsPresenter(Color4[] colors, ChartsContainer container)
			{
				this.colors = colors;
				this.container = container;
				var chartsColors = new Vector4[colors.Length];
				for (int i = 0; i < colors.Length; i++) {
					chartsColors[i] = colors[i].ToVector4();
				}
				material = new ChartMaterial() { Colors = chartsColors };
				mesh = new Mesh<Vector3> {
					Indices = new ushort[] { 0 },
					Vertices = new Vector3[
						container.chartsVerticesOffset +
						container.Charts.Length * container.chartVerticesCount
					],
					AttributeLocations = ChartMaterial.ShaderProgram.MeshAttribLocations
				};
				int vi = container.chartsVerticesOffset;
				for (int ci = 0; ci < container.Charts.Length; ++ci) {
					for (int cvi = 0; cvi < container.chartVerticesCount; vi++, cvi++) {
						int x = (cvi / 2) * container.ControlPointsSpacing;
						mesh.Vertices[vi] = new Vector3(x, 0, container.Charts[ci].ColorIndex);
					}
				}
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState((Widget)node);
				ro.Colors = colors;
				ro.Mesh = mesh;
				ro.Material = material;
				ro.Container = container;
				return ro;
			}

			public IPresenter Clone() => (IPresenter)MemberwiseClone();

			private class RenderObject : WidgetRenderObject
			{
				public Color4[] Colors;
				public Mesh<Vector3> Mesh;
				public ChartMaterial Material;
				public ChartsContainer Container;

				public override void Render()
				{
					PrepareRenderState();
					Renderer.DrawRect(Vector2.Zero, Container.Size, Container.BackgroundColor);
					Renderer.MainRenderList.Flush();

					if (Container.isMeshUpdateRequired || Container.hasPreviousUpdate) {
						Container.RebuildMesh(Mesh);
						if (Container.AutoInvalidate) {
							Window.Current.Invalidate();
						}
					}
					Container.hasPreviousUpdate = Container.isMeshUpdateRequired;
					Container.isMeshUpdateRequired = false;

					Material.Matrix = Renderer.FixupWVP((Matrix44)LocalToWorldTransform * Renderer.ViewProjection);
					Material.Apply(0);
					int chartsStartVertex = Line.VerticesCount * Container.userLines.Length;
					int chartsVertexCount = Container.GetActiveChartsCount() * Container.chartVerticesCount;
#if LIME_PROFILER
					var profilingInfo = GpuCallInfo.Acquire(Material, 0);
					Mesh.Topology = PrimitiveTopology.TriangleStrip;
					Mesh.Draw(chartsStartVertex, chartsVertexCount, profilingInfo);
					Mesh.Topology = PrimitiveTopology.TriangleList;
					Mesh.Draw(0, chartsStartVertex, profilingInfo);
#else
					Mesh.Topology = PrimitiveTopology.TriangleStrip;
					Mesh.Draw(chartsStartVertex, chartsVertexCount);
					Mesh.Topology = PrimitiveTopology.TriangleList;
					Mesh.Draw(0, chartsStartVertex);
#endif
					const float fontHeight = 14;
					foreach (var line in Container.userLines) {
						if (line.Caption != null && line.CaptionPosition.Y > fontHeight) {
							var startPosition = new Vector2(
								line.CaptionPosition.X,
								Container.Height - line.CaptionPosition.Y);
							Renderer.DrawTextLine(
								startPosition,
								line.Caption,
								fontHeight,
								Colors[line.ColorIndex],
								letterSpacing: 0);
						}
					}
				}
			}
		}
	}
}
