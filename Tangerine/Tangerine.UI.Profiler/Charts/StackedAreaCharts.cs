#if PROFILER

using Lime;
using System;

namespace Tangerine.UI.Charts
{
	internal class StackedAreaCharts : FixedHorizontalSpacingCharts
	{
		private readonly ChartsGroupMeshBuilder meshBuilder;

		public StackedAreaCharts(Parameters parameters) : base(parameters)
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
			private readonly float[] accumulatedHeights;

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
				accumulatedHeights = new float[chartsGroup.ControlPointsCount];
				int chartVertexCount = GetChartVertexCount(chartsGroup.ControlPointsCount);
				int capacity = chartsGroup.Charts.Count * chartVertexCount;
				vertexStorage = new ChartsCommon.SwappableVertexStorage(capacity, buffersCount: 2);
				Rebuild();
			}

			/// <inheritdoc/>
			public void Rebuild()
			{
				float containerHeight = chartsGroup.Height;
				float chartsMaxValue = float.NegativeInfinity;
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
					foreach (var chart in chartsGroup.Charts) {
						if (chart.Visible) {
							var heights = chart.Heights;
							int step = (1 - visibleChartParity) * 2 - 1;
							int start = visibleChartParity * (heightsRange - 1);
							int end = (1 - visibleChartParity) * heightsRange - visibleChartParity;
							for (int i = start; i != end; i += step) {
								int hIndex = heightIndexOffset + i;
								float h = heights[hIndex];
								float ah = accumulatedHeights[hIndex];
								accumulatedHeights[hIndex] += h;
								float x = i * controlPointsSpacing;
								float y0 = ah + h * visibleChartParity;
								float y1 = ah + h * (1 - visibleChartParity);
								vertexBuffer[vertexIndex++] = new Vector3(x, y0, chart.ColorIndex);
								vertexBuffer[vertexIndex++] = new Vector3(x, y1, chart.ColorIndex);
							}
							visibleChartParity = 1 - visibleChartParity;
							++VisibleVertexCount;
						}
					}
					for (int i = heightIndexOffset; i < accumulatedHeights.Length; i++) {
						chartsMaxValue = Math.Max(chartsMaxValue, accumulatedHeights[i]);
						accumulatedHeights[i] = 0;
					}
				}
				VisibleVertexCount *= GetChartVertexCount(heightsRange);
				MeshDirtyFlags = MeshDirtyFlags.Vertices;
				float scaleCoefficient = containerHeight * ChartsScaleFactor / Mathf.Max(chartsMaxValue, 1e-6f);
				ExtraTransform =
					Matrix44.CreateScale(1, -scaleCoefficient, 1) *
					Matrix44.CreateTranslation(0, containerHeight, 0);
				IsRebuildRequired = false;
			}

			/// <inheritdoc/>
			public void RenderObjectAcquired(RenderObject renderObject)
			{
				MeshDirtyFlags = MeshDirtyFlags.None;
				vertexStorage.Swap();
			}

			private int GetChartVertexCount(int controlPointsCount) => 2 * controlPointsCount;
		}
	}
}

#endif // PROFILER