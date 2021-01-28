#if PROFILER

using System.Collections;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.Timelines
{
	/// <summary>
	/// Generates positions for time intervals.
	/// </summary>
	internal class PeriodPositions : IEnumerable<Vector2>
	{
		private readonly SpacingParameters parameters;
		private readonly IEnumerable<TimePeriod> periods;

		public PeriodPositions(IEnumerable<TimePeriod> periods, SpacingParameters parameters)
		{
			this.periods = periods;
			this.parameters = parameters;
		}

		public IEnumerator<Vector2> GetEnumerator() => new Enumerator(periods.GetEnumerator(), parameters);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private class Enumerator : IEnumerator<Vector2>
		{
			private readonly List<float> freeSpaceOfLines;
			private readonly SpacingParameters parameters;
			private readonly IEnumerator<TimePeriod> periods;

			public Enumerator(IEnumerator<TimePeriod> periods, SpacingParameters parameters)
			{
				this.periods = periods;
				this.parameters = parameters;
				freeSpaceOfLines = new List<float>();
			}

			public Vector2 Current { get; private set; }

			object IEnumerator.Current => Current;

			public void Dispose() => periods.Dispose();

			public bool MoveNext()
			{
				bool value = periods.MoveNext();
				if (value) {
					Current = AcquirePosition(periods.Current);
				}
				return value;
			}

			public void Reset()
			{
				freeSpaceOfLines.Clear();
				periods.Reset();
			}

			private Vector2 AcquirePosition(TimePeriod period)
			{
				int lineIndex = -1;
				for (int i = 0; i < freeSpaceOfLines.Count; i++) {
					if (freeSpaceOfLines[i] < period.StartTime) {
						lineIndex = i;
						break;
					}
				}
				// Add extra time to make small intervals on the timeline more visible.
				float finishTime = period.FinishTime + parameters.MicrosecondsPerPixel;
				if (lineIndex == -1) {
					lineIndex = freeSpaceOfLines.Count;
					freeSpaceOfLines.Add(finishTime);
				} else {
					freeSpaceOfLines[lineIndex] = finishTime;
				}
				return new Vector2(
					x: period.StartTime / parameters.MicrosecondsPerPixel,
					y: parameters.TimePeriodHeight * lineIndex +
					   parameters.TimePeriodVerticalMargin * (lineIndex + 1)
				);
			}
		}

		public struct SpacingParameters
		{
			public float MicrosecondsPerPixel;
			public float TimePeriodVerticalMargin;
			public float TimePeriodHeight;
		}
	}
}

#endif // PROFILER