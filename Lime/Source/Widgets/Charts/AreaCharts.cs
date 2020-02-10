namespace Lime.Widgets.Charts
{
	public class AreaCharts : Charts
	{
		private float chartsMaxValue;
		private float[] accumulatedHeights;

		public AreaCharts(Parameters parameters) : base(parameters) =>
			accumulatedHeights = new float[ControlPointsCount];

		protected override void RecalculateVertices()
		{
			if (!isMeshUpdateRequired) return;
			isMeshUpdateRequired = false;
			chartsMaxValue = 0;
			for (int i = 0; i < accumulatedHeights.Length; i++) {
				chartsMaxValue = Mathf.Max(chartsMaxValue, accumulatedHeights[i]);
				accumulatedHeights[i] = 0;
			}
			float scaleCoefficient = ScaleCoefficient;
			int offset = Line.VerticesCount * userLines.Length;
			int parity = 0;
			UpdateUserLines(scaleCoefficient);
			foreach (var chart in charts) {
				if (chart.IsVisible) {
					int step = parity == 0 ? 1 : -1;
					int pointIndex = parity * (chart.Points.Length - 1);
					for (int i = 0; i < chartVerticesCount; i += 2, pointIndex += step) {
						float point = chart.Points[pointIndex];
						float ah = accumulatedHeights[pointIndex];
						accumulatedHeights[pointIndex] += point;
						float x = ControlPointsSpacing * pointIndex;
						float y0 = chartsMaxHeight - (ah + point * parity) * scaleCoefficient;
						float y1 = chartsMaxHeight - (ah + point * (1 - parity)) * scaleCoefficient;
						vertices[offset + i + 0] = new Vector3(x, y0, chart.ColorIndex);
						vertices[offset + i + 1] = new Vector3(x, y1, chart.ColorIndex);
					}
					offset += chartVerticesCount;
					parity = (parity + 1) % 2;
				}
			}
			mesh.Vertices = vertices;
			mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}

		protected override int CalculateSubmeshVerticesCount(int controlPointsCount) => 2 * controlPointsCount;

		public float ScaleCoefficient { get => chartsMaxHeight * 0.9f / Mathf.Max(chartsMaxValue, 1000f / 60f); }
	}
}
