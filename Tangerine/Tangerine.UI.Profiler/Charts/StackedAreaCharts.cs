using Lime;

namespace Tangerine.UI.Charts
{
	internal class StackedAreaCharts : FixedHorizontalSpacingCharts
	{
		private readonly ChartsGroupMeshBuilder meshBuilder;

		public StackedAreaCharts(Parameters parameters) : base(parameters)
		{
			meshBuilder = new ChartsGroupMeshBuilder(this);
			Presenter = new ChartsCommon.Presenter(this, meshBuilder);
		}

		private class ChartsGroupMeshBuilder : IChartsGroupMeshBuilder
		{
			/// <summary>
			/// Provides free space above сharts.
			/// </summary>
			private const float ChartsScaleFactor = 0.9f;

			private readonly int chartVertexCount;
			private readonly float[] accumulatedHeights;
			private readonly FixedHorizontalSpacingCharts chartsGroup;
			private readonly ChartsCommon.SwappableVertexStorage vertexStorage;

			/// <inheritdoc/>
			public Vector3[] Vertices => vertexStorage.Vertices;

			/// <inheritdoc/>
			public int FirstVisibleVertex => 0;

			/// <inheritdoc/>
			public int VisibleVertexCount { get; private set; }

			/// <inheritdoc/>
			public MeshDirtyFlags MeshDirtyFlags { get; set; }

			public ChartsGroupMeshBuilder(FixedHorizontalSpacingCharts chartsGroup)
			{
				this.chartsGroup = chartsGroup;
				accumulatedHeights = new float[chartsGroup.ControlPointsCount];
				chartVertexCount = GetChartVertexCount(chartsGroup.ControlPointsCount);
				int capacity = chartsGroup.Charts.Count * chartVertexCount;
				vertexStorage = new ChartsCommon.SwappableVertexStorage(capacity, buffersCount: 2);
			}

			public void Rebuild()
			{
				float chartsMaxValue = 0;
				for (int i = 0; i < accumulatedHeights.Length; i++) {
					chartsMaxValue = Mathf.Max(chartsMaxValue, accumulatedHeights[i]);
					accumulatedHeights[i] = 0;
				}
				float containerHeight = chartsGroup.Height;
				float scaleCoefficient = containerHeight * ChartsScaleFactor / Mathf.Max(chartsMaxValue, 1e-6f);
				int controlPointsSpacing = chartsGroup.ControlPointsSpacing;
				int vertexIndex = 0;
				int visibleChartParity = 0;
				VisibleVertexCount = 0;
				foreach (var chart in chartsGroup.Charts) {
					if (chart.Visible) {
						var heights = chart.Heights;
						int step = (1 - visibleChartParity) * 2 - 1;
						int start = visibleChartParity * (heights.Length - 1);
						int end = (1 - visibleChartParity) * heights.Length - visibleChartParity;
						for (int i = start; i != end; i += step) {
							float h = heights[i];
							float ah = accumulatedHeights[i];
							accumulatedHeights[i] += h;
							float x = i * controlPointsSpacing;
							float y0 = containerHeight - (ah + h * visibleChartParity) * scaleCoefficient;
							float y1 = containerHeight - (ah + h * (1 - visibleChartParity)) * scaleCoefficient;
							Vertices[vertexIndex++] = new Vector3(x, y0, chart.ColorIndex);
							Vertices[vertexIndex++] = new Vector3(x, y1, chart.ColorIndex);
						}
						visibleChartParity = 1 - visibleChartParity;
						++VisibleVertexCount;
					}
				}
				VisibleVertexCount *= chartVertexCount;
				MeshDirtyFlags = MeshDirtyFlags.Vertices;
			}

			/// <inheritdoc/>
			public void RenderObjectAcquired()
			{
				MeshDirtyFlags = MeshDirtyFlags.None;
				vertexStorage.Swap();
			}

			private int GetChartVertexCount(int controlPointsCount) => 2 * controlPointsCount;
		}
	}
}
