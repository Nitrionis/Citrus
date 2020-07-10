using System;
using Lime;

namespace Tangerine.UI.Charts
{
	internal abstract class ChartsContainer : Widget
	{
		protected readonly SwappablePart swappablePart;
		protected readonly Chart[] charts;

		protected int UnchangedSlice { get; private set; }
		protected int LastChangedSlice { get; private set; }
		protected int MappedLastChangedSlice { get; private set; }

		protected bool IsMeshFullRebuildRequired { get; private set; }

		protected Vector2 CachedSize { get; private set; }

		/// <summary>
		/// Max number of control points by horizontally.
		/// </summary>
		public int MaxControlPointsCount { get; }

		/// <summary>
		/// Current number of control points by horizontally.
		/// Calculated automatically based on widget width.
		/// </summary>
		public int MappedControlPointsCount { get; protected set; }

		/// <summary>
		/// The horizontal distance between the control points in pixels.
		/// </summary>
		public int ControlPointsSpacing { get; }

		/// <summary>
		/// Charts scaling factor calculated during the previous update.
		/// </summary>
		public float ScalingFactor { get; protected set; }

		/// <summary>
		/// To calculate the scaling factor, the charts are based on the results of the previous frame.
		/// If AutoInvalidate disabled updating of data in the charts will not cause a redraw of the window.
		/// If rendering stops, we will see scaling from the previous frame.
		/// </summary>
		public bool AutoInvalidate { get; set; } = true;

		/// <summary>
		/// Called when you click on the charts.
		/// </summary>
		public Action<Slice> SliceSelected;

		private readonly Color4 backgroundColor;
		private readonly Vector4 burntColor;

		protected ChartsContainer(Parameters parameters)
		{
			MaxControlPointsCount = parameters.ControlPointsCount;
			ControlPointsSpacing = parameters.ControlPointsSpacing;
			AddNode(new Widget {
				Anchors = Anchors.LeftRightTopBottom,
				HitTestTarget = true,
				HitTestMethod = HitTestMethod.BoundingRect,
				Clicked = () => {
					int mappedIndex = Math.Min(
						MappedControlPointsCount - 1,
						((int)LocalMousePosition().X + ControlPointsSpacing / 2) / ControlPointsSpacing);
					int unmappedIndex =
						LastChangedSlice + MaxControlPointsCount - MappedControlPointsCount + 1 +
						(mappedIndex + MappedControlPointsCount - MappedLastChangedSlice - 1) % MappedControlPointsCount;
					SliceSelected?.Invoke(GetSlice(unmappedIndex));
				},
				
			});
			backgroundColor = parameters.BackgroundColor;
			burntColor = parameters.BurntColor;
		}

		protected override void OnSizeChanged(Vector2 sizeDelta)
		{
			base.OnSizeChanged(sizeDelta);
			if (Size.X != CachedSize.X) {
				MappedControlPointsCount = (int)(Size.X / ControlPointsSpacing);
				MappedLastChangedSlice = Math.Min(MappedLastChangedSlice, MappedControlPointsCount);
			}
		}

		/// <summary>
		/// Enables or disables a specific chart.
		/// </summary>
		public void SetVisible(int chartIndex, bool value)
		{
			IsMeshFullRebuildRequired = true;
			charts[chartIndex].IsVisible = value;
		}

		/// <summary>
		/// Returns a vertical slice of charts.
		/// </summary>
		public Slice GetSlice(int index)
		{
			var values = new float[charts.Length];
			for (int i = 0; i < charts.Length; i++) {
				values[i] = charts[i].Points[index];
			}
			return new Slice { Points = values, Index = index };
		}

		/// <summary>
		/// Enqueue a vertical slice to charts.
		/// </summary>
		public virtual void EnqueueSlice(float[] points)
		{
			LastChangedSlice = (LastChangedSlice + 1) % MaxControlPointsCount;
			MappedLastChangedSlice = (MappedLastChangedSlice + 1) % MappedControlPointsCount;
			for (int i = 0; i < charts.Length; i++) {
				charts[i].Points[LastChangedSlice] = points[i];
			}
		}

		/// <summary>
		/// Inserts a line into the specified slot.
		/// </summary>
		public void SetLine(int lineIndex, Line line) =>
			swappablePart.WriteTarget.Lines[lineIndex] = line;

		/// <summary>
		/// Clears the charts data.
		/// </summary>
		public virtual void Reset()
		{
			IsMeshFullRebuildRequired = true;
			foreach (var chart in charts) {
				for (int i = 0; i < chart.Points.Length; i++) {
					chart.Points[i] = 0;
				}
			}
		}

		protected int GetActiveChartsCount()
		{
			int activeCount = 0;
			foreach (var chart in charts) {
				activeCount += chart.IsVisible ? 1 : 0;
			}
			return activeCount;
		}

		public struct Line
		{
			public Color4 Color;

			/// <summary>
			/// Line start position.
			/// </summary>
			public Vector2 Start;

			/// <summary>
			/// Line end position.
			/// </summary>
			public Vector2 End;

			/// <summary>
			/// Some caption near the line.
			/// </summary>
			public string Caption;

			/// <summary>
			/// Indicates whether the line will scale along with the chart.
			/// </summary>
			public bool IsScalable { get; set; }

			/// <summary>
			/// Caption position relative to the line.
			/// If x and y is 0 then it is start of the line.
			/// If x and y is 1 then it is end of the line.
			/// </summary>
			public Vector2 CaptionPosition;

			/// <summary>
			/// Caption offset at pixels relative caption position.
			/// </summary>
			public Vector2 CaptionOffset;

			public Line(Vector2 start, Vector2 end, Color4 color)
			{
				Start            = start;
				End              = end;
				Color            = color;
				Caption          = null;
				IsScalable       = true;
				CaptionPosition  = Vector2.Zero;
				CaptionOffset    = Vector2.Zero;
			}
		}

		protected class Chart
		{
			public int ColorIndex;
			public bool IsVisible = false;
			public float[] Points = null;
		}

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
			public int ControlPointsSpacing = 5;
			public int ControlPointsCount;
			public int ChartsCount = 1;
			public int UserLinesCount = 0;
			public Color4[] ChartsColors;
			public Color4 BackgroundColor = Color4.Black;
			public Vector4 BurntColor = Vector4.One;

			public Parameters(int controlPointsCount, Color4[] colors)
			{
				ControlPointsCount = controlPointsCount;
				ChartsColors = colors;
			}
		}

		protected class SwappablePart
		{
			public MutablePart ReadTarget { get; private set; }
			public MutablePart WriteTarget { get; private set; }

			public SwappablePart(Parameters parameters, int verticesCount, int indicesCount)
			{
				ReadTarget = new MutablePart(parameters, verticesCount, indicesCount);
				WriteTarget = new MutablePart(parameters, verticesCount, indicesCount);
			}

			public void SwapTargets() => (ReadTarget, WriteTarget) = (WriteTarget, ReadTarget);
		}

		protected class MutablePart
		{
			public readonly Line[] Lines;
			public readonly Vector3[] Vertices;
			public readonly ushort[] Indices;

			public float NewestSlicePosition;
			public float ScaleFactor;

			public MutablePart(Parameters parameters, int verticesCount, int indicesCount)
			{
				Lines = new Line[parameters.UserLinesCount];
				Vertices = new Vector3[verticesCount];
				Indices = new ushort[indicesCount];
			}
		}

		protected abstract class ChartsPresenter : IPresenter
		{
			protected readonly ChartsContainer container;
			protected readonly ChartsMesh<Vector3> mesh;
			protected readonly ChartMaterial material;

			public ChartsPresenter(Color4[] colors, ChartsContainer container)
			{
				this.container = container;
				var chartsColors = new Vector4[colors.Length];
				for (int i = 0; i < colors.Length; i++) {
					chartsColors[i] = colors[i].ToVector4();
				}
				material = new ChartMaterial() {
					Colors = chartsColors,
					BurntColor = container.burntColor
				};
				mesh = new ChartsMesh<Vector3> {
					AttributeLocations = ChartMaterial.ShaderProgram.MeshAttribLocations
				};
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			protected abstract RenderObject AcquireRenderObject();

			public Lime.RenderObject GetRenderObject(Node node)
			{
				container.swappablePart.SwapTargets();
				var immutablePart = container.swappablePart.ReadTarget;
				mesh.Vertices = immutablePart.Vertices;
				mesh.Indices = immutablePart.Indices;

				var ro = AcquireRenderObject();
				ro.CaptureRenderState((Widget)node);
				ro.Mesh            = mesh;
				ro.Material        = material;
				ro.ImmutablePart   = immutablePart;
				ro.Size            = container.Size;
				ro.BackgroundColor = container.backgroundColor;
				return ro;
			}

			public IPresenter Clone() => throw new NotImplementedException();

			protected abstract class RenderObject : WidgetRenderObject
			{
				public ChartMaterial Material;
				public ChartsMesh<Vector3> Mesh;
				public MutablePart ImmutablePart;
				public Vector2 Size;
				public Color4 BackgroundColor;

				protected abstract void DrawCharts();

				public sealed override void Render()
				{
					PrepareRenderState();
					Renderer.DrawRect(Vector2.Zero, Size, BackgroundColor);
					Renderer.MainRenderList.Flush();

					var scaleMatrix = Matrix44.CreateScale(0, ImmutablePart.ScaleFactor, 0);

					Material.Matrix = Renderer.FixupWVP(
						(Matrix44)LocalToWorldTransform * scaleMatrix * Renderer.ViewProjection);
					Material.BurnRange = Size.X;
					Material.NewestItemPosition = ImmutablePart.NewestSlicePosition;
					Material.Apply(0);

					((IPartialInvalidationMesh<Vector3>)Mesh).FlushInvalidatedData();
					DrawCharts();

					// Draw additional lines over charts.
					const float fontHeight = 14;
					foreach (var line in ImmutablePart.Lines) {
						var tsp = scaleMatrix.TransformVector(line.Start);
						var tep = scaleMatrix.TransformVector(line.End);
						Renderer.DrawLine(tsp, tep, line.Color);
						if (line.Caption != null && line.CaptionPosition.Y > fontHeight) {
							var position = line.CaptionOffset + new Vector2(
									Mathf.Lerp(line.CaptionPosition.X, tsp.X, tep.X),
									Mathf.Lerp(line.CaptionPosition.Y, tsp.Y, tep.Y));
							Renderer.DrawTextLine(position, line.Caption, fontHeight, line.Color, 0);
						}
					}
				}
			}
		}
	}
}
