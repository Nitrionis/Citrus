using Lime;

namespace Tangerine.UI.Charts
{
	internal class AreaCharts : ChartsContainer
	{
		private float[] accumulatedHeights;

		public AreaCharts(Parameters parameters) : base(parameters) =>
			accumulatedHeights = new float[parameters.ControlPointsCount];

		protected override void RebuildMesh(Mesh<Vector3> mesh)
		{
			chartsMaxValue = 0;
			for (int i = 0; i < accumulatedHeights.Length; i++) {
				chartsMaxValue = Mathf.Max(chartsMaxValue, accumulatedHeights[i]);
				accumulatedHeights[i] = 0;
			}
			float scaleCoefficient = ScaleCoefficient;
			int vertexIndex = Line.VerticesCount * userLines.Length;
			int parity = 0;
			foreach (var chart in Charts) {
				if (chart.IsVisible) {
					int step = (1 - parity) * 2 - 1;
					int start = parity * (chart.Points.Capacity - 2);
					int end = (1 - parity) * (chart.Points.Capacity - 1) - parity;
					for (int i = start; i != end; i += step) {
						float point = chart.Points.GetItem(i + 1);
						float ah = accumulatedHeights[i];
						accumulatedHeights[i] += point;
						float x = i * ControlPointsSpacing;
						float y0 = Height - (ah + point * parity) * scaleCoefficient;
						float y1 = Height - (ah + point * (1 - parity)) * scaleCoefficient;
						mesh.Vertices[vertexIndex++] = new Vector3(x, y0, chart.ColorIndex);
						mesh.Vertices[vertexIndex++] = new Vector3(x, y1, chart.ColorIndex);
					}
					parity = (parity + 1) % 2;
				}
			}
			RecalculateUserLines(mesh, scaleCoefficient);
			mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}

		protected override int CalculateSubmeshVerticesCount(int controlPointsCount) => 2 * controlPointsCount;

		public float ScaleCoefficient { get => Height * 0.9f / Mathf.Max(chartsMaxValue, 0.01f); }
	}
}
