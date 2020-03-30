using Lime;
using Lime.Widgets.Charts;

namespace Tangerine.UI
{
	internal class AreaReplaceableCharts : AreaCharts
	{
		private int indexOfLast;
		private float[][] originalPoints;

		public AreaReplaceableCharts(Parameters parameters) : base(parameters)
		{
			indexOfLast = parameters.ControlPointsCount;
			originalPoints = new float[parameters.ChartsCount][];
			for (int i = 0; i < originalPoints.Length; i++) {
				originalPoints[i] = new float[parameters.ControlPointsCount];
			}
		}

		public override void PushSlice(float[] values)
		{
			base.PushSlice(values);
			indexOfLast = (indexOfLast + 1) % ControlPointsCount;
			for (int i = 0; i < originalPoints.Length; i++) {
				originalPoints[i][indexOfLast] = values[i];
			}
		}

		public override void Reset()
		{
			base.Reset();
			indexOfLast = ControlPointsCount;
			foreach (var chartPoints in originalPoints) {
				for (int i = 0; i < chartPoints.Length; i++) {
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
			var chartOriginalPoints = originalPoints[chartIndex];
			if (restoreOriginalValues) {
				int pointIndex = (indexOfLast + 1) % ControlPointsCount;
				for (int i = 0; i < chart.Points.Length; i++) {
					chart.Points[i] = chartOriginalPoints[pointIndex] + sgn * values[i];
					pointIndex = (pointIndex + 1) % chart.Points.Length;
				}
			} else {
				for (int i = 0; i < chart.Points.Length; i++) {
					chart.Points[i] += sgn * values[i];
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
			var chartOriginalPoints = originalPoints[chartIndex];
			int pointIndex = (indexOfLast + 1) % ControlPointsCount;
			for (int i = 0; i < chart.Points.Length; i++) {
				chart.Points[i] = chartOriginalPoints[pointIndex];
				pointIndex = (pointIndex + 1) % chart.Points.Length;
			}
			isMeshUpdateRequired = true;
		}
	}
}
