using System.Collections;
using System.Collections.Generic;

namespace Tangerine.UI
{
	internal class FixedCapacityQueue<T> : IEnumerable<T>
	{
		private int indexOfLast;
		private T[] data;

		public FixedCapacityQueue(int historySize)
		{
			indexOfLast = - 1;
			data = new T[historySize];
		}

		/// <summary>
		/// Interprets the queue as an array, where the first element in the queue corresponds to index 0.
		/// </summary>
		public T GetItem(int index) => data[(indexOfLast + 1 + index) % data.Length];

		/// <summary>
		/// Replaces the oldest element in history.
		/// </summary>
		public void Enqueue(T item) => data[indexOfLast = (indexOfLast + 1) % data.Length] = item;

		public IEnumerator<T> GetEnumerator() => new Enumerator<T>(this);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator<T>(this);

		public class Enumerator<T> : IEnumerator<T>
		{
			private int itemIndex = -1;
			private int processedItemsCount = -1;
			private T[] data;
			private T current;

			public T Current => current;
			object IEnumerator.Current => current;

			public Enumerator(FixedCapacityQueue<T> storage)
			{
				data = storage.data;
				itemIndex = storage.indexOfLast;
			}

			public bool MoveNext()
			{
				itemIndex = (itemIndex + 1) % data.Length;
				current = data[itemIndex];
				return ++processedItemsCount < data.Length;
			}

			public void Reset() => itemIndex = -1;

			public void Dispose() { }
		}
	}
}
