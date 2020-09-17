using System;

namespace Tangerine.UI.Charts
{
	internal static class FixedSpacingChartsExtension
	{
		/// <summary>
		/// Reinterprets the charts data as queues and inserts a data slice into it.
		/// </summary>
		/// <param name="points">Vertical slice of data.</param>
		public static void EnqueueSlice(this FixedHorizontalSpacingCharts chartsGroup, float[] points)
		{
			int chartIndex = 0;
			foreach (var chart in chartsGroup.Charts) {
				var heights = chart.Heights;
				Array.Copy(heights, 1, heights, 0, heights.Length - 1);
				heights[heights.Length - 1] = points[chartIndex++];
			}
			chartsGroup.Invalidate();
		}
	}
}
