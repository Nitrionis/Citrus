using System;
using System.Collections.Generic;
using Yuzu;

namespace Lime.Graphics.Platform.Profiling
{
	/// <summary>
	/// Represents queue of lists.
	/// </summary>
	public class RingPool<T> where T : new()
	{
		/// <summary>
		/// Locked when resizing internal indices storage.
		/// Storage size changes a finite number of times.
		/// </summary>
		public readonly object ResizeLockObject = new object();

		/// <summary>
		/// Stores lists of ReferencesTable indices.
		/// The list in the array is as follows: [items_count & flags][item][item][item]...
		/// </summary>
		private T[] data;
		private uint nextItemIndex;
		private uint oldestItemIndex;

		/// <summary>
		/// Lets find lists after resizing data array. Also store list flags.
		/// </summary>
		private Redirection[] redirections;
		private Queue<uint> freeRedirectionsIndices;

		public RingPool(int startCapacity = 1024)
		{
			if (startCapacity < 2) {
				throw new InvalidOperationException();
			}

			data = new T[startCapacity];
			nextItemIndex = 0;
			oldestItemIndex = 0;

			redirections = new Redirection[startCapacity / 2];
			freeRedirectionsIndices = new Queue<uint>(redirections.Length);
			for (uint i = 0; i < redirections.Length; i++) {
				freeRedirectionsIndices.Enqueue(i);
			}
		}

		public Enumerable<T> Enumerate(uint descriptor) => new Enumerable<T>(descriptor, this);

		public uint Capacity => (uint)data.Length;

		public uint ListOffset(uint descriptor) => redirections[descriptor].ListOffset;

		public uint GetListItemsCount(uint descriptor) => redirections[descriptor].ListSize;

		public bool HasDoubleDeletionFlag(uint descriptor) =>
			redirections[descriptor].FlagBits == Redirection.DoubleDeletionBit;

		public void RemoveDoubleDeletionFlag(uint descriptor) =>
			redirections[descriptor].FlagBits = 0;

		public T ItemAt(uint descriptor, int itemIndex) =>
			data[(ListOffset(descriptor) + itemIndex) % data.Length];

		public T ItemAt(uint rawIndex) => data[rawIndex];

		/// <summary>
		/// Creates a new empty list and returns its descriptor.
		/// </summary>
		/// <param name="isDoubleDeletion">Indicates that the list will be deleted a second time.</param>
		public uint AcquireList(bool isDoubleDeletion)
		{
			if (freeRedirectionsIndices.Count == 0) {
				for (uint i = 0; i < redirections.Length; i++) {
					freeRedirectionsIndices.Enqueue((uint)redirections.Length + i);
				}
				lock (ResizeLockObject) {
					Array.Resize(ref redirections, 2 * redirections.Length);
				}
			}
			uint redirectionIndex = freeRedirectionsIndices.Dequeue();
			uint flag = isDoubleDeletion ? Redirection.DoubleDeletionBit : 0;
			redirections[redirectionIndex] = new Redirection {
				ListOffsetAndFlags = nextItemIndex | flag,
				ListSize = 0
			};
			nextItemIndex = AcquireNextIndex();
			return redirectionIndex;
		}

		public void FreeOldestList(uint descriptor)
		{
			oldestItemIndex = (oldestItemIndex + GetListItemsCount(descriptor)) % Capacity;
			freeRedirectionsIndices.Enqueue(descriptor);
		}

		public void FreeNewestList(uint descriptor)
		{
			nextItemIndex = (nextItemIndex + Capacity - GetListItemsCount(descriptor)) % Capacity;
			freeRedirectionsIndices.Enqueue(descriptor);
		}

		public void AddToNewestList(uint descriptor, T item)
		{
			++redirections[descriptor].ListSize;
			data[nextItemIndex] = item;
			nextItemIndex = AcquireNextIndex();
		}

		private uint AcquireNextIndex()
		{
			uint index = (nextItemIndex + 1u) % Capacity;
			if (index == oldestItemIndex) {
				lock (ResizeLockObject) {
					Array.Resize(ref data, 2 * data.Length);
					if (nextItemIndex < oldestItemIndex) {
						uint halfSize = (uint)data.Length / 2u;
						uint srcIndex = oldestItemIndex;
						uint dstIndex = halfSize + oldestItemIndex;
						uint length   = halfSize - oldestItemIndex;
						Array.Copy(data, srcIndex, data, dstIndex, length);
						oldestItemIndex = dstIndex;
						uint offset = dstIndex - srcIndex;
						for (int i = 0; i < redirections.Length; i++) {
							ref var redirection = ref redirections[i];
							uint listOffset = redirection.ListOffset;
							if (listOffset >= srcIndex) {
								redirection.ListOffsetAndFlags = redirection.FlagBits | listOffset + offset;
							}
						}
					}
					index = nextItemIndex + 1;
				}
			}
			return index;
		}

		private struct Redirection
		{
			/// <summary>
			/// Indicates that the list will be deleted a second time.
			/// </summary>
			public const uint DoubleDeletionBit = 0x_8000_0000;

			[YuzuRequired]
			public uint ListOffsetAndFlags;

			[YuzuRequired]
			public uint ListSize;

			public uint FlagBits
			{
				get => ListOffsetAndFlags & DoubleDeletionBit;
				set => ListOffsetAndFlags = ListOffsetAndFlags & ~DoubleDeletionBit | value;
			}

			public uint ListOffset
			{
				get => ListOffsetAndFlags & ~DoubleDeletionBit;
				set => ListOffsetAndFlags = value | FlagBits;
			}
		}

		public struct Enumerable<T> where T : new()
		{
			public uint Descriptor;
			public RingPool<T> Pool;

			public Enumerable(uint descriptor, RingPool<T> pool)
			{
				Descriptor = descriptor;
				Pool = pool;
			}

			public Enumerator<T> GetEnumerator() => new Enumerator<T>(Descriptor, Pool);
		}

		public struct Enumerator<T> where T : new()
		{
			private RingPool<T> pool;
			private uint itemIndex;
			private uint itemsCount;
			private int processedItemsCount;

			public T Current { get; private set; }

			public Enumerator(uint descriptor, RingPool<T> pool)
			{
				this.pool = pool;
				var redirection = pool.redirections[descriptor];
				itemsCount = redirection.ListSize;
				itemIndex = redirection.ListOffset;
				processedItemsCount = -1;
				Current = new T();
			}

			public bool MoveNext()
			{
				Current = pool.ItemAt(itemIndex);
				itemIndex = (itemIndex + 1u) % pool.Capacity;
				return ++processedItemsCount < itemsCount;
			}
		}
	}
}
