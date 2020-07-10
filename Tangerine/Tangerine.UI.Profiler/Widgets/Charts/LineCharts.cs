using Lime;

namespace Tangerine.UI.Charts
{
	internal class LineCharts : ChartsContainer
	{
		/// <summary>
		/// Typically, charts in <see cref="ChartsContainer"/> is aligned to the top.
		/// This setting allows you to change the target height for each chart.
		/// </summary>
		public readonly float[] CustomChartScales;

		/// <summary>
		/// If enabled, each chart will scale independently.
		/// </summary>
		public readonly bool IsIndependentMode;

		/// <summary>
		/// Stores the maximum value for each chart calculated in the previous <see cref="RecalculateMesh"/>.
		/// </summary>
		private float[] maxValuesPerCharts;

		public new class Parameters : ChartsContainer.Parameters
		{
			public bool IsIndependentMode;
			public Parameters(int controlPointsCount, Color4[] colors) : base(controlPointsCount, colors) { }
		}

		public LineCharts(Parameters parameters) : base(parameters)
		{
			IsIndependentMode = parameters.IsIndependentMode;
			CustomChartScales = new float[parameters.ChartsCount];
			for (int i = 0; i < parameters.ChartsCount; i++) {
				CustomChartScales[i] = 1.0f;
			}
			maxValuesPerCharts = new float[parameters.ChartsCount];
			for (int i = 0; i < parameters.ChartsCount; i++) {
				maxValuesPerCharts[i] = 1.0f;
			}
		}

		private Vector2 GetNormal(Vector2 v) => new Vector2(-v.Y, v.X);

		protected override void RebuildFullMesh(Mesh<Vector3> mesh)
		{
			int parity = 0;
			int chartIndex = 0;
			int vertexIndex = 0;
			float newChartsMaxValue = 1;
			foreach (var chart in charts) {
				if (chart.IsVisible) {
					float maxValue = IsIndependentMode ? maxValuesPerCharts[chartIndex] : ChartsMaxValue;
					float scalingFactor = CustomChartScales[chartIndex] * Height / maxValue;
					maxValue = 0;
					int step = (1 - parity) * 2 - 1;
					int start = parity * (chart.Points.Capacity - 2);
					int end = (1 - parity) * (chart.Points.Capacity - 2);
					for (int i = start; i != end; i += step) {
						float p1 = chart.Points.GetItem(i + 1);
						float p2 = chart.Points.GetItem(i + 1 + step);
						Vector2 a = new Vector2(
							x: i * ControlPointsSpacing,
							y: Height - p1 * scalingFactor);
						Vector2 b = new Vector2(
							x: (i + step) * ControlPointsSpacing,
							y: Height - p2 * scalingFactor);
						Vector2 n = GetNormal((b - a).Normalized * 0.5f);
						mesh.Vertices[vertexIndex++] = new Vector3(a - n, chart.ColorIndex + 0.5f);
						mesh.Vertices[vertexIndex++] = new Vector3(a + n, chart.ColorIndex + 0.5f);
						mesh.Vertices[vertexIndex++] = new Vector3(b - n, chart.ColorIndex + 0.5f);
						mesh.Vertices[vertexIndex++] = new Vector3(b + n, chart.ColorIndex + 0.5f);
						maxValue = Mathf.Max(maxValue, p1);
					}
					parity = (parity + 1) % 2;
					maxValuesPerCharts[chartIndex] = maxValue;
					newChartsMaxValue = Mathf.Max(maxValue, newChartsMaxValue);
				}
				chartIndex++;
			}
			RecalculateUserLines(mesh, IsIndependentMode ? 1 : Height / chartsMaxValue);
			chartsMaxValue = newChartsMaxValue;
			mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}

		protected override int CalculateSubmeshVerticesCount(int controlPointsCount) => 4 * controlPointsCount - 4;
	}
}
