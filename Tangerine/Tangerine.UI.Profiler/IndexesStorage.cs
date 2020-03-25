using System.Collections;
using System.Collections.Generic;

namespace Tangerine.UI
{
	internal class IndexesStorage : IEnumerable<long>
	{
		private int indexOfLast;
		private long[] history;

		public IndexesStorage(int historySize)
		{
			indexOfLast = historySize;
			history = new long[historySize];
		}

		/// <summary>
		/// Considers the collection as fixed capacity queue.
		/// </summary>
		/// <param name="index">Item index from the beginning of the queue.</param>
		public long GetItem(int index) => history[(indexOfLast + index) % history.Length];

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

			public Enumerator(IndexesStorage storage)
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
