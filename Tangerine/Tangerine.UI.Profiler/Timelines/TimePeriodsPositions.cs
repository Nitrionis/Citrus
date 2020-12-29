#if PROFILER

using System;
using System.Collections;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.Timelines
{
	internal class TimePeriodsPositions : IEnumerable<Vector2>
	{
		private readonly Parameters parameters;
		
		public TimePeriodsPositions(Parameters parameters) => this.parameters = parameters;

		public IEnumerator<Vector2> GetEnumerator() => new Enumerator(parameters);
		
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private class Enumerator : IEnumerator<Vector2>
		{
			private readonly Parameters parameters;
			private readonly List<float> freeSpaceOfLines;
			private readonly IEnumerator<TimePeriod> timePerios;
			
			public Vector2 Current { get; private set; }

			object IEnumerator.Current => Current;
			
			public Enumerator(Parameters parameters)
			{
				this.parameters = parameters;
				freeSpaceOfLines = new List<float>();
				timePerios = parameters.Periods.GetEnumerator();
			}

			public void Dispose() => timePerios.Dispose();

			public bool MoveNext()
			{
				bool value = timePerios.MoveNext();
				if (value) {
					Current = AcquirePosition(timePerios.Current);
				}
				return value;
			}
			
			public void Reset()
			{
				freeSpaceOfLines.Clear();
				timePerios.Reset();
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
				float finishTime = (float)(period.FinishTime + Math.Ceiling(parameters.MicrosecondsPerPixel));
				if (lineIndex == -1) {
					lineIndex = freeSpaceOfLines.Count;
					freeSpaceOfLines.Add(finishTime);
				} else {
					freeSpaceOfLines[lineIndex] = finishTime;
				}
				return new Vector2(
					parameters.HorizontalOffset + period.StartTime / parameters.MicrosecondsPerPixel,
					parameters.ItemHeight * lineIndex + parameters.ItemMargin * (lineIndex + 1));
			}
		}
		
		public struct Parameters
		{
			public IEnumerable<TimePeriod> Periods;
			public float MicrosecondsPerPixel;
			public float HorizontalOffset;
			public float ItemMargin;
			public float ItemHeight;
		}
	}
}

#endif // PROFILER