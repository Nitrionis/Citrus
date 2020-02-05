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

		private float chartsMaxValue;

		public new class Parameters : Charts.Parameters
		{
			public bool IsIndependentMode;
			public Parameters(int controlPointsCount, Vector4[] colors) : base(controlPointsCount, colors) { }
		}

		public LineCharts(Parameters parameters) : base(parameters) =>
			CustomChartScales = new float[parameters.ChartsCount];

		private Vector2 GetNormal(Vector2 v) => new Vector2(-v.Y, v.X);

		protected override void RecalculateVertices()
		{
			if (!isMeshUpdateRequired) return;
			isMeshUpdateRequired = false;
			UpdateUserLines(IsIndependentMode ? 1 : chartsMaxHeight / chartsMaxValue);
			int parity = 0;
			int vertexIndex = Line.VerticesCount * userLines.Length;
			int submeshIndex = 0;
			foreach (var submesh in charts) {
				if (submesh.IsVisible) {
					float submeshMaxValue = 0;
					float submeshScaleCoefficient = CustomChartScales[submeshIndex] * chartsMaxHeight /
						(IsIndependentMode ? maxValuePerChart[submeshIndex] : chartsMaxValue);
					int step = (1 - parity) * 2 - 1;
					int start = parity * (submesh.Points.Length - 1);
					int end = (1 - parity) * (submesh.Points.Length - 1);
					for (int i = start; i != end; i += step) {
						Vector2 a = new Vector2(
							x: i * controlPointsSpacing,
							y: chartsMaxHeight - submesh.Points[i] * submeshScaleCoefficient);
						Vector2 b = new Vector2(
							x: (i + step) * controlPointsSpacing,
							y: chartsMaxHeight - submesh.Points[i + step] * submeshScaleCoefficient);
						Vector2 n = GetNormal((b - a).Normalized * 0.5f);
						vertices[vertexIndex++] = new Vector3(a - n, submesh.ColorIndex);
						vertices[vertexIndex++] = new Vector3(a + n, submesh.ColorIndex);
						vertices[vertexIndex++] = new Vector3(b - n, submesh.ColorIndex);
						vertices[vertexIndex++] = new Vector3(b + n, submesh.ColorIndex);
						submeshMaxValue = Mathf.Max(submeshMaxValue, submesh.Points[i]);
					}
					parity = (parity + 1) % 2;
					maxValuePerChart[submeshIndex] = submeshMaxValue;
					chartsMaxValue = Mathf.Max(submeshMaxValue, chartsMaxValue);
				}
				submeshIndex++;
			}
			mesh.Vertices = vertices;
			mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}

		protected override int CalculateSubmeshVerticesCount(int controlPointsCount) => 4 * controlPointsCount - 4;
	}
}
