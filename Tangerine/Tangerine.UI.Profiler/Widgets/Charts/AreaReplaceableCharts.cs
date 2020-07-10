namespace Tangerine.UI.Charts
{
	internal class AreaReplaceableCharts : AreaCharts
	{
		private FixedCapacityQueue<float>[] originalData;

		public AreaReplaceableCharts(Parameters parameters) : base(parameters)
		{
			originalData = new FixedCapacityQueue<float>[Charts.Length];
			for (int i = 0; i < originalData.Length; i++) {
				originalData[i] = new FixedCapacityQueue<float>(controlPointsCount);
			}
		}

		public float GetOriginalPoint(int chartIndex, int pointIndex) =>
			originalData[chartIndex].GetItem(pointIndex + 1);

		public override void EnqueueSlice(float[] values)
		{
			base.EnqueueSlice(values);
			for (int i = 0; i < originalData.Length; i++) {
				originalData[i].Enqueue(values[i]);
			}
		}

		public override void Reset()
		{
			base.Reset();
			foreach (var chartPoints in originalData) {
				for (int i = 0; i < chartPoints.Capacity; i++) {
					chartPoints[i] = 0;
				}
			}
		}

		/// <summary>
		/// Subtracts values from graph points.
		/// </summary>
		/// <param name="restoreOriginalValues">Whether to restore the original values before subtraction.</param>
		public void Subtract(int chartIndex, float[] values, bool restoreOriginalValues = false) =>
			Add(chartIndex, values, restoreOriginalValues, -1f);

		/// <summary>
		/// Add values to graph points.
		/// </summary>
		/// <param name="restoreOriginalValues">Whether to restore the original values before addition.</param>
		public void Add(int chartIndex, float[] values, bool restoreOriginalValues = false) =>
			Add(chartIndex, values, restoreOriginalValues, 1f);

		private void Add(int chartIndex, float[] values, bool restoreOriginalValues, float sgn)
		{
			var chart = Charts[chartIndex];
			if (restoreOriginalValues) {
				var originalPoints = originalData[chartIndex];
				for (int i = 1; i < originalPoints.Capacity; i++) {
					int internalIndex = chart.Points.GetInternalIndex(i);
					chart.Points[internalIndex] = originalPoints[internalIndex] + sgn * values[i - 1];
				}
			} else {
				for (int i = 1; i < chart.Points.Capacity; i++) {
					chart.Points[chart.Points.GetInternalIndex(i)] += sgn * values[i - 1];
				}
			}
			isMeshUpdateRequired = true;
		}

		/// <summary>
		/// Restores graph values ​​as if the Add and Subtract methods were never called.
		/// </summary>
		public void Restore(int chartIndex)
		{
			var chart = Charts[chartIndex];
			var originalPoints = originalData[chartIndex];
			for (int i = 0; i < originalPoints.Capacity; i++) {
				chart.Points[i] = originalPoints[i];
			}
			isMeshUpdateRequired = true;
		}
	}
}
