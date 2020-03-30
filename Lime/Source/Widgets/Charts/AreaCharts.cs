namespace Lime.Widgets.Charts
{
	public class AreaCharts : ChartsContainer
	{
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
			foreach (var chart in Charts) {
				if (chart.IsVisible) {
					int step = parity == 0 ? 1 : -1;
					int pointIndex = parity * (chart.Points.Length - 1);
					for (int i = 0; i < chartVerticesCount; i += 2, pointIndex += step) {
						float point = chart.Points[pointIndex];
						float ah = accumulatedHeights[pointIndex];
						accumulatedHeights[pointIndex] += point;
						float x = ControlPointsSpacing * pointIndex;
						float y0 = chartsHeight - (ah + point * parity) * scaleCoefficient;
						float y1 = chartsHeight - (ah + point * (1 - parity)) * scaleCoefficient;
						mesh.Vertices[offset + i + 0] = new Vector3(x, y0, chart.ColorIndex);
						mesh.Vertices[offset + i + 1] = new Vector3(x, y1, chart.ColorIndex);
					}
					offset += chartVerticesCount;
					parity = (parity + 1) % 2;
				}
			}
			UpdateUserLines(scaleCoefficient);
			mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
			Window.Current?.Invalidate();
		}

		protected override int CalculateSubmeshVerticesCount(int controlPointsCount) => 2 * controlPointsCount;

		public float ScaleCoefficient { get => chartsHeight * 0.9f / Mathf.Max(chartsMaxValue, 0.1f); }
	}
}
