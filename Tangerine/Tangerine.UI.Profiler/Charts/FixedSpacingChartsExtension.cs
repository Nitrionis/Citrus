#if PROFILER

using System;
using static Tangerine.UI.Charts.FixedHorizontalSpacingCharts;

namespace Tangerine.UI.Charts
{
	internal static class FixedSpacingChartsExtension
	{
		/// <summary>
		/// The original values will be shifted 1 to the left and inserts point at the end of the array.
		/// </summary>
		public static void Enqueue(this IFixedSpacingChart chart, float point)
		{
			var heights = chart.Heights;
			Array.Copy(heights, 1, heights, 0, heights.Length - 1);
			heights[heights.Length - 1] = point;
		}

		/// <summary>
		/// Gets the maximum height among the chart points. O(n).
		/// </summary>
		public static float MaxValue(this IFixedSpacingChart chart)
		{
			float value = float.NegativeInfinity;
			var heights = chart.Heights;
			unchecked {
				foreach (float t in heights) {
					value = Math.Max(value, t);
				}
			}
			return value;
		}

		/// <summary>
		/// Gets the maximum height among all charts. O(n) for each chart.
		/// </summary>
		public static float MaxValue(this FixedHorizontalSpacingCharts chartsGroup)
		{
			float value = float.NegativeInfinity;
			unchecked {
				foreach (var chart in chartsGroup.Charts) {
					value = Math.Max(value, chart.MaxValue());
				}
			}
			return value;
		}
	}
}

#endif // PROFILER