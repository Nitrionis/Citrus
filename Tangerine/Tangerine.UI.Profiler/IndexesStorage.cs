using System.Collections;
using System.Collections.Generic;

namespace Tangerine.UI
{
	internal class IndexesStorage : IEnumerable<IndexesStorage.Item>
	{
		public struct Item
		{
			public long FrameIndex;
			public long UpdateIndex;
		}

		private int indexOfLast;
		private Item[] history;

		public IndexesStorage(int historySize)
		{
			indexOfLast = historySize;
			history = new Item[historySize];
		}

		/// <summary>
		/// Considers the collection as fixed capacity queue.
		/// </summary>
		/// <param name="index">Item index from the beginning of the queue.</param>
		public Item GetItem(int index) => history[(indexOfLast + index) % history.Length];

		/// <summary>
		/// Replaces the oldest element in history.
		/// </summary>
		public void Enqueue(Item item) => history[indexOfLast = (indexOfLast + 1) % history.Length] = item;

		public IEnumerator<Item> GetEnumerator() => new Enumerator(this);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

		public class Enumerator : IEnumerator<Item>
		{
			private int itemIndex = -1;
			private int processedItemsCount = -1;
			private Item[] history;

			private Item current;

			public Item Current => current;

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
