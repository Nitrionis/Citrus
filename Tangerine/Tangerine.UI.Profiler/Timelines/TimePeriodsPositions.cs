#if PROFILER

using System.Collections;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.Timelines
{
	internal partial class Timeline
	{
		private class TimePeriodsPositions : IEnumerable<Vector2>
		{
			private readonly SpacingParameters parameters;
			private readonly IEnumerable<TimePeriod> periods;

			public TimePeriodsPositions(IEnumerable<TimePeriod> periods, SpacingParameters parameters)
			{
				this.periods = periods;
				this.parameters = parameters;
			}

			public IEnumerator<Vector2> GetEnumerator() => new Enumerator(periods.GetEnumerator(), parameters);

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			private class Enumerator : IEnumerator<Vector2>
			{
				private readonly IEnumerator<TimePeriod> periods;
				private readonly SpacingParameters parameters;
				private readonly List<float> freeSpaceOfLines;

				public Vector2 Current { get; private set; }

				object IEnumerator.Current => Current;

				public Enumerator(IEnumerator<TimePeriod> periods, SpacingParameters parameters)
				{
					this.periods = periods;
					this.parameters = parameters;
					freeSpaceOfLines = new List<float>();
				}

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
						period.StartTime / parameters.MicrosecondsPerPixel,
						parameters.ItemHeight * lineIndex + parameters.ItemVerticalMargin * (lineIndex + 1));
				}
			}

			public struct SpacingParameters
			{
				public float MicrosecondsPerPixel;
				public float ItemVerticalMargin;
				public float ItemHeight;
			}
		}
	}
}

#endif // PROFILER