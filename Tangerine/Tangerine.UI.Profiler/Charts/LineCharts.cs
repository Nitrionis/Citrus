#if PROFILER

using System;
using System.Linq;
using Lime;

namespace Tangerine.UI.Charts
{
	internal class LineCharts : FixedHorizontalSpacingCharts
	{
		private readonly ChartsGroupMeshBuilder meshBuilder;

		/// <summary>
		/// If set to true the charts will scale independently of each other.
		/// </summary>
		public bool IndependentScaling
		{
			get => meshBuilder.IndependentScaling; 
			set => meshBuilder.IndependentScaling = value;
		}
		
		/// <summary>
		/// The last scaling option for the specified chart.
		/// </summary>
		public float GetScale(int chartIndex) => meshBuilder.Scales[chartIndex];
		
		public LineCharts(Parameters parameters) : base(parameters)
		{
			if (parameters.ControlPointsCount < 2) {
				throw new InvalidOperationException();
			}
			meshBuilder = new ChartsGroupMeshBuilder(this);
			Presenter = new ChartsCommon.Presenter(this, meshBuilder);
		}

		public void Rebuild() => meshBuilder.Rebuild();

		/// <inheritdoc/>
		public override void Invalidate() => meshBuilder.IsRebuildRequired = true;

		private class ChartsGroupMeshBuilder : IChartsGroupMeshBuilder
		{
			/// <summary>
			/// Provides free space above сharts.
			/// </summary>
			private const float ChartsScaleFactor = 0.9f;

			private readonly FixedHorizontalSpacingCharts chartsGroup;
			private readonly ChartsCommon.SwappableVertexStorage vertexStorage;

			public bool IndependentScaling;
			public readonly float[] Scales;
			
			/// <inheritdoc/>
			public Vector3[] Vertices => vertexStorage.Vertices;

			/// <inheritdoc/>
			public int FirstVisibleVertex => 0;

			/// <inheritdoc/>
			public int VisibleVertexCount { get; private set; }

			/// <inheritdoc/>
			public MeshDirtyFlags MeshDirtyFlags { get; set; }

			/// <inheritdoc/>
			public bool IsRebuildRequired { get; set; } = true;

			/// <inheritdoc/>
			public Matrix44 ExtraTransform { get; private set; }

			public ChartsGroupMeshBuilder(FixedHorizontalSpacingCharts chartsGroup)
			{
				this.chartsGroup = chartsGroup;
				int chartVertexCount = GetChartVertexCount(chartsGroup.ControlPointsCount);
				int capacity = chartsGroup.Charts.Count * chartVertexCount;
				vertexStorage = new ChartsCommon.SwappableVertexStorage(capacity, buffersCount: 2);
				Scales = new float[chartsGroup.Charts.Count];
				Rebuild();
			}

			/// <inheritdoc/>
			public void Rebuild()
			{
				float scaledContainerHeight = chartsGroup.Height/* * Window.Current.PixelScale*/;
				float lineWidthScale = 1;
				int controlPointsSpacing = chartsGroup.ControlPointsSpacing;
				int vertexIndex = 0;
				int visibleChartParity = 0;
				int heightsRange = (int)Mathf.Min(
					chartsGroup.ControlPointsCount,
					chartsGroup.Width / chartsGroup.ControlPointsSpacing + 1);
				int heightIndexOffset = chartsGroup.ControlPointsCount - heightsRange;
				VisibleVertexCount = 0;
				var vertexBuffer = Vertices;
				unchecked {
					for (int i = 0; i < Scales.Length; i++) {
						Scales[i] = scaledContainerHeight / Math.Max(
							1e-6f, chartsGroup.Charts[i].MaxValue());
					}
					if (!IndependentScaling) {
						float minValue = Scales.Min();
						for (int i = 0; i < Scales.Length; i++) {
							Scales[i] = minValue;
						}
					}
					int chartIndex = 0;
					foreach (var chart in chartsGroup.Charts) {
						if (chart.Visible) {
							var heights = chart.Heights;
							var scale = Scales[chartIndex++];
							int step = (1 - visibleChartParity) * 2 - 1;
							int start = visibleChartParity * (heightsRange - 1);
							int end = (1 - visibleChartParity) * heightsRange - (1 - visibleChartParity);
							for (int i = start; i != end; i += step) {
								float p1 = heights[heightIndexOffset + i];
								float p2 = heights[heightIndexOffset + i + step];
								var a = new Vector2(x: i * controlPointsSpacing, y: p1 * scale);
								var b = new Vector2(x: (i + step) * controlPointsSpacing, y: p2 * scale);
								var n = GetNormal((b - a).Normalized * 0.5f) * lineWidthScale;
								vertexBuffer[vertexIndex++] = new Vector3(a - n, chart.ColorIndex);
								vertexBuffer[vertexIndex++] = new Vector3(a + n, chart.ColorIndex);
								vertexBuffer[vertexIndex++] = new Vector3(b - n, chart.ColorIndex);
								vertexBuffer[vertexIndex++] = new Vector3(b + n, chart.ColorIndex);
							}
							visibleChartParity = 1 - visibleChartParity;
							++VisibleVertexCount;
						}
					}
				}
				VisibleVertexCount *= GetChartVertexCount(heightsRange);
				MeshDirtyFlags = MeshDirtyFlags.Vertices;
				ExtraTransform =
					Matrix44.CreateScale(1, -1 * ChartsScaleFactor, 1) *
					Matrix44.CreateTranslation(0, chartsGroup.Height, 0);
				IsRebuildRequired = false;
			}

			/// <inheritdoc/>
			public void RenderObjectAcquired(RenderObject renderObject)
			{
				MeshDirtyFlags = MeshDirtyFlags.None;
				vertexStorage.Swap();
			}

			private Vector2 GetNormal(Vector2 v) => new Vector2(-v.Y, v.X);

			private int GetChartVertexCount(int controlPointsCount) => 4 * controlPointsCount - 4;
		}
	}
}

#endif // PROFILER