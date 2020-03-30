using System.Collections;
using System.Collections.Generic;

namespace Tangerine.UI
{
	internal class FixedCapacityQueue : IEnumerable<long>
	{
		private int indexOfLast;
		private long[] history;

		public FixedCapacityQueue(int historySize)
		{
			indexOfLast = - 1;
			history = new long[historySize];
		}

		/// <summary>
		/// Interprets the queue as an array, where the first element in the queue corresponds to index 0.
		/// </summary>
		public long GetItem(int index) => history[(indexOfLast + 1 + index) % history.Length];

		/// <summary>
		/// Replaces the oldest element in history.
		/// </summary>
		public void Enqueue(long index) => history[indexOfLast = (indexOfLast + 1) % history.Length] = index;

		public IEnumerator<long> GetEnumerator() => new Enumerator(this);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

		public class Enumerator : IEnumerator<long>
		{
			private int itemIndex = -1;
			private int processedItemsCount = -1;
			private long[] history;

			private long current;

			public long Current => current;

			object IEnumerator.Current => current;

			public Enumerator(FixedCapacityQueue storage)
			{
				history = storage.history;
				itemIndex = storage.indexOfLast;
			}

			public bool MoveNext()
			{
				itemIndex = (itemIndex + 1) % history.Length;
				current = history[itemIndex];
				return ++processedItemsCount < history.Length;
			}

			public void Reset() => itemIndex = -1;

			public void Dispose() { }
		}
	}
}
