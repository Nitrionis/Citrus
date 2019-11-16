namespace Lime
{
	public class LineChartsGroup : ChartsGroup
	{
		public bool IndependentMode = false;
		private float[] maxValuePerSubmesh;
		public readonly float[] CustomChartScales;

		public LineChartsGroup(ChartsGroupCreateInfo createInfo) : base(createInfo)
		{
			maxValuePerSubmesh = new float[createInfo.ChartsCount];
			CustomChartScales = new float[createInfo.ChartsCount];
			for (int i = 0; i < CustomChartScales.Length; i++) {
				CustomChartScales[i] = 1f;
			}
		}

		public override void UpdateVertices()
		{
			UpdateNonChartsLines(IndependentMode ? 1 : chartMaxHeight / chartMaxValue);
			int parity = 0;
			int vertexIndex = NonChartsLinesVerticesCount;
			int submeshIndex = 0;
			foreach (var submesh in submeshes) {
				if (submesh.Enable) {
					float submeshMaxValue = 0;
					float submeshScaleCoefficient = CustomChartScales[submeshIndex] * chartMaxHeight /
						(IndependentMode ? maxValuePerSubmesh[submeshIndex] : chartMaxValue);
					int step = (1 - parity) * 2 - 1;
					int start = parity * (submesh.Points.Length - 1);
					int end = (1 - parity) * (submesh.Points.Length - 1);
					for (int i = start; i != end; i += step) {
						Vector2 a = new Vector2(
							i * ControlPointsSpacing,
							chartMaxHeight - submesh.Points[i] * submeshScaleCoefficient);
						Vector2 b = new Vector2(
							(i + step) * ControlPointsSpacing,
							chartMaxHeight - submesh.Points[i + step] * submeshScaleCoefficient);
						Vector2 n = GetVectorNormal((b - a).Normalized * 0.5f);
						Vertices[vertexIndex++] = new Vector3(a - n, submesh.ColorIndex);
						Vertices[vertexIndex++] = new Vector3(a + n, submesh.ColorIndex);
						Vertices[vertexIndex++] = new Vector3(b - n, submesh.ColorIndex);
						Vertices[vertexIndex++] = new Vector3(b + n, submesh.ColorIndex);
						submeshMaxValue = Mathf.Max(submeshMaxValue, submesh.Points[i]);
					}
					parity = (parity + 1) % 2;
					maxValuePerSubmesh[submeshIndex] = submeshMaxValue;
					chartMaxValue = Mathf.Max(submeshMaxValue, chartMaxValue);
				}
				submeshIndex++;
			}
			Mesh.Vertices = Vertices;
			Mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}

		protected override int GetSubmeshVerticesCount(int controlPointsCount) => 4 * controlPointsCount - 4;

		private Vector2 GetVectorNormal(Vector2 v) => new Vector2(-v.Y, v.X);
	}
}
