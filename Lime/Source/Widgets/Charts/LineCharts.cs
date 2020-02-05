namespace Lime.Widgets.Charts
{
	public class LineCharts : Charts
	{
		/// <summary>
		/// Typically, charts in <see cref="Charts"/> is aligned to the top.
		/// This setting allows you to change the target height for each chart.
		/// </summary>
		public readonly float[] CustomChartScales;

		/// <summary>
		/// If enabled, each chart will scale independently.
		/// </summary>
		public bool IsIndependentMode => CustomChartScales != null;

		/// <summary>
		/// Stores the maximum value for each chart calculated in the previous <see cref="RecalculateVertices"/>.
		/// </summary>
		private float[] maxValuePerChart;

		private float chartsMaxValue = 1;

		public new class Parameters : Charts.Parameters
		{
			public bool IsIndependentMode;
			public Parameters(int controlPointsCount, Color4[] colors) : base(controlPointsCount, colors) { }
		}

		public LineCharts(Parameters parameters) : base(parameters)
		{
			if (parameters.IsIndependentMode) {
				CustomChartScales = new float[parameters.ChartsCount];
				for (int i = 0; i < parameters.ChartsCount; i++) {
					CustomChartScales[i] = 1.0f;
				}
			}
			maxValuePerChart = new float[parameters.ChartsCount];
			for (int i = 0; i < parameters.ChartsCount; i++) {
				maxValuePerChart[i] = 1.0f;
			}
		}

		private Vector2 GetNormal(Vector2 v) => new Vector2(-v.Y, v.X);

		protected override void RecalculateVertices()
		{
			if (!isMeshUpdateRequired) return;
			isMeshUpdateRequired = false;
			UpdateUserLines(IsIndependentMode ? 1 : chartsMaxHeight / chartsMaxValue);
			int parity = 0;
			int chartIndex = 0;
			int vertexIndex = Line.VerticesCount * userLines.Length;
			foreach (var chart in charts) {
				if (chart.IsVisible) {
					float maxValue = 0;
					float scalingFactor = chartsMaxHeight / (IsIndependentMode ?
						CustomChartScales[chartIndex] * maxValuePerChart[chartIndex] : chartsMaxValue);
					int step = (1 - parity) * 2 - 1; // -1 or +1
					int start = parity * (chart.Points.Length - 1);
					int end = (1 - parity) * (chart.Points.Length - 1);
					for (int i = start; i != end; i += step) {
						Vector2 a = new Vector2(
							x: i * controlPointsSpacing,
							y: chartsMaxHeight - chart.Points[i] * scalingFactor);
						Vector2 b = new Vector2(
							x: (i + step) * controlPointsSpacing,
							y: chartsMaxHeight - chart.Points[i + step] * scalingFactor);
						Vector2 n = GetNormal((b - a).Normalized * 0.5f);
						vertices[vertexIndex++] = new Vector3(a - n, chart.ColorIndex);
						vertices[vertexIndex++] = new Vector3(a + n, chart.ColorIndex);
						vertices[vertexIndex++] = new Vector3(b - n, chart.ColorIndex);
						vertices[vertexIndex++] = new Vector3(b + n, chart.ColorIndex);
						maxValue = Mathf.Max(maxValue, chart.Points[i]);
					}
					parity = (parity + 1) % 2;
					maxValuePerChart[chartIndex] = maxValue;
					chartsMaxValue = Mathf.Max(maxValue, chartsMaxValue);
				}
				chartIndex++;
			}
			mesh.Vertices = vertices;
			mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}

		protected override int CalculateSubmeshVerticesCount(int controlPointsCount) => 4 * controlPointsCount - 4;
	}
}
