#if PROFILER

using System.Collections;
using System.Collections.Generic;

namespace Tangerine.UI
{
	/// <remarks>
	/// It is considered that the queue is always filled with elements.
	/// </remarks>
	internal class FixedCapacityQueue<T> : IEnumerable<T>
	{
		private int oldestItemIndex;
		private readonly T[] data;

		public int Capacity { get; }

		public FixedCapacityQueue(int historySize)
		{
			Capacity = historySize;
			oldestItemIndex = 0;
			data = new T[historySize];
		}

		public T this[int index]
		{
			get => data[index];
			set => data[index] = value;
		}

		/// <summary>
		/// Interprets the queue as an array, where the oldest element in the queue corresponds to index 0.
		/// </summary>
		public int GetInternalIndex(int index) => (oldestItemIndex + index) % data.Length;

		/// <summary>
		/// Interprets the queue as an array, where the oldest element in the queue corresponds to index 0.
		/// </summary>
		public T GetItem(int index) => data[GetInternalIndex(index)];

		/// <summary>
		/// Replaces the oldest element in history.
		/// </summary>
		public void Enqueue(T item)
		{
			data[oldestItemIndex] = item;
			oldestItemIndex = (oldestItemIndex + 1) % data.Length;
		}

		public IEnumerator<T> GetEnumerator() => new Enumerator(this);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

		IEnumerator<T> Reversed() => new Enumerator(this, reverse: true);

		public class Enumerator : IEnumerator<T>
		{
			private int itemIndex;
			private int processedItemsCount = -1;
			private readonly int step;
			private readonly T[] data;

			public T Current { get; private set; }
			object IEnumerator.Current => Current;

			public Enumerator(FixedCapacityQueue<T> storage, bool reverse = false)
			{
				data = storage.data;
				step = reverse ? storage.Capacity - 1 : 1;
				itemIndex = storage.oldestItemIndex - (reverse ? 0 : 1);
			}

			public bool MoveNext()
			{
				itemIndex = (itemIndex + step) % data.Length;
				Current = data[itemIndex];
				return ++processedItemsCount < data.Length;
			}

			public void Reset() => itemIndex = -1;

			public void Dispose() { }
		}
	}
}

#endif // PROFILER