namespace Lime.Widgets.Charts
{
	public class LineCharts : ChartsContainer
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
		private float[] maxValuePerChart;

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
			maxValuePerChart = new float[parameters.ChartsCount];
			for (int i = 0; i < parameters.ChartsCount; i++) {
				maxValuePerChart[i] = 1.0f;
			}
		}

		private Vector2 GetNormal(Vector2 v) => new Vector2(-v.Y, v.X);

		protected override void RecalculateMesh()
		{
			if (!isMeshUpdateRequired) return;
			isMeshUpdateRequired = false;
			int parity = 0;
			int chartIndex = 0;
			int vertexIndex = Line.VerticesCount * userLines.Length;
			float newChartsMaxValue = 1;
			foreach (var chart in Charts) {
				if (chart.IsVisible) {
					float maxValue = 0;
					float scalingFactor = CustomChartScales[chartIndex] * chartsHeight /
						(IsIndependentMode ? maxValuePerChart[chartIndex] : chartsMaxValue);
					int step = (1 - parity) * 2 - 1; // -1 or +1
					int start = parity * (chart.Points.Length - 1);
					int end = (1 - parity) * (chart.Points.Length - 1);
					for (int i = start; i != end; i += step) {
						Vector2 a = new Vector2(
							x: i * ControlPointsSpacing,
							y: chartsHeight - chart.Points[i] * scalingFactor);
						Vector2 b = new Vector2(
							x: (i + step) * ControlPointsSpacing,
							y: chartsHeight - chart.Points[i + step] * scalingFactor);
						Vector2 n = GetNormal((b - a).Normalized * 0.5f);
						mesh.Vertices[vertexIndex++] = new Vector3(a - n, chart.ColorIndex);
						mesh.Vertices[vertexIndex++] = new Vector3(a + n, chart.ColorIndex);
						mesh.Vertices[vertexIndex++] = new Vector3(b - n, chart.ColorIndex);
						mesh.Vertices[vertexIndex++] = new Vector3(b + n, chart.ColorIndex);
						maxValue = Mathf.Max(maxValue, chart.Points[i]);
					}
					parity = (parity + 1) % 2;
					maxValuePerChart[chartIndex] = maxValue;
					newChartsMaxValue = Mathf.Max(maxValue, newChartsMaxValue);
				}
				chartIndex++;
			}
			RecalculateUserLinesMesh(IsIndependentMode ? 1 : chartsHeight / chartsMaxValue);
			chartsMaxValue = newChartsMaxValue;
			mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}

		protected override int CalculateSubmeshVerticesCount(int controlPointsCount) => 4 * controlPointsCount - 4;
	}
}
