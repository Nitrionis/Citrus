namespace Lime
{
	public class AreaChartsGroup : ChartsGroup
	{
		private float[] accumulatedHeights;

		public AreaChartsGroup(ChartsGroupCreateInfo createInfo) : base(createInfo)
		{
			accumulatedHeights = new float[controlPointsCount];
		}

		public override void UpdateVertices()
		{
			chartMaxValue = 0;
			for (int i = 0; i < accumulatedHeights.Length; i++) {
				chartMaxValue = Mathf.Max(chartMaxValue, accumulatedHeights[i]);
				accumulatedHeights[i] = 0;
			}
			float scaleCoefficient = ScaleCoefficient;
			int vi = NonChartsLinesVerticesCount;
			int parity = 0;
			UpdateNonChartsLines(scaleCoefficient);
			foreach (var submesh in submeshes) {
				if (submesh.Enable) {
					int step = (1 - parity) * 2 - 1;
					int pointIndex = parity * (submesh.Points.Length - 1);
					for (int i = vi; i < vi + submeshVerticesCount; i += 2, pointIndex += step) {
						float ah = accumulatedHeights[pointIndex];
						float point = submesh.Points[pointIndex];
						accumulatedHeights[pointIndex] += point;
						float x = ControlPointsSpacing * pointIndex;
						Vertices[i] = new Vector3(x, chartMaxHeight - (ah + point * parity) * scaleCoefficient, submesh.ColorIndex);
						Vertices[i + 1] = new Vector3(x, chartMaxHeight - (ah + point * (1 - parity)) * scaleCoefficient, submesh.ColorIndex);
					}
					vi += submeshVerticesCount;
					parity = (parity + 1) % 2;
				}
			}
			Mesh.Vertices = Vertices;
			Mesh.DirtyFlags |= MeshDirtyFlags.Vertices;
		}

		protected override int GetSubmeshVerticesCount(int controlPointsCount) => 2 * controlPointsCount;

		public float ScaleCoefficient { get => chartMaxHeight * 0.9f / Mathf.Max(chartMaxValue, 1000f / 60f); }
	}
}
