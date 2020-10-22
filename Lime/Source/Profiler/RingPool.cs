// Lists are allocated from and returned to the RingPool in order equals to the queue.
// This define allows you to enable additional validation for list acquire and release operations.
#define DebugRingPool

#if PROFILER

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lime.Profiler
{
	public class RingPool
	{
		public struct ListDescriptor
		{
			private const uint InvalidValue = uint.MaxValue;

			/// <summary>
			/// Creates an invalid descriptor.
			/// </summary>
			public static ListDescriptor Null => (ListDescriptor)InvalidValue;

			public uint Value;

			public bool IsNull => Value == InvalidValue;

			public static explicit operator uint(ListDescriptor descriptor) => descriptor.Value;
			public static explicit operator ListDescriptor(uint value) => new ListDescriptor { Value = value };
		}
	}

	/// <summary>
	/// Represents queue of lists.
	/// </summary>
	/// <remarks>
	/// Memory for lists is allocated and released in order: first-Acquired, first-Released.
	/// </remarks>
	public class RingPool<ListItemType> : RingPool where ListItemType : struct
	{
		/// <summary>
		/// Locked when resizing internal storage.
		/// </summary>
		public readonly object ResizeLockObject = new object();

		/// <summary>
		/// Stores lists of ReferencesTable indices.
		/// </summary>
		protected ListItemType[] listsItems;
		protected uint nextItemIndex;
		protected uint oldestItemIndex;

		/// <summary>
		/// Lets find lists after resizing listsItems. Also store list flags.
		/// </summary>
		protected ListInfo[] lists;
		protected Queue<uint> freeListIndices;

		private ListDescriptor newestListDescriptor;

		public RingPool(int startCapacity = 1024)
		{
			if (startCapacity < 2) {
				throw new InvalidOperationException();
			}

			listsItems = new ListItemType[startCapacity];
			nextItemIndex = 0;
			oldestItemIndex = 0;

			lists = new ListInfo[startCapacity / 2];
			freeListIndices = new Queue<uint>(lists.Length);
			for (uint i = 0; i < lists.Length; i++) {
				freeListIndices.Enqueue(i);
			}
		}

		/// <summary>
		/// Calling this method will invalidate the descriptors of all lists allocated from
		/// this pool. Memory allocated for lists will not be released, but will be reused.
		/// </summary>
		public void Clear()
		{
			nextItemIndex = 0;
			oldestItemIndex = 0;
			freeListIndices.Clear();
			for (uint i = 0; i < lists.Length; i++) {
				freeListIndices.Enqueue(i);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint ListOffset(ListDescriptor descriptor) => lists[descriptor.Value].Offset;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint NextListItemIndex(uint currentItemIndex) => (currentItemIndex + 1u) % (uint)listsItems.Length;

		/// <summary>
		/// Returns an enumerator for listing all objects in the list.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		/// <remarks>
		/// After changing the capacity of the pool, the enumerator will be invalid.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Enumerable Enumerate(ListDescriptor descriptor) => new Enumerable(descriptor, this);

		/// <summary>
		/// Returns the i-th element of the list.
		/// </summary>
		/// <param name="descriptor">List handle acquired from this pool.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ListItemType ItemAt(ListDescriptor descriptor, int itemIndex) =>
			listsItems[(ListOffset(descriptor) + itemIndex) % listsItems.Length];

		/// <summary>
		/// Provides direct access to the elements of all lists.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ListItemType ItemAt(uint rawIndex) => listsItems[rawIndex];

		/// <summary>
		/// Gets a reference to an list element.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		/// <remarks>
		/// After changing the capacity of the pool, the reference will become invalid!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref ListItemType ReferenceAtItem(ListDescriptor descriptor, int itemIndex) =>
			ref listsItems[(ListOffset(descriptor) + itemIndex) % listsItems.Length];

		/// <summary>
		/// Provides direct access to a list item.
		/// </summary>
		/// <remarks>
		/// After changing the capacity of the pool, the reference will become invalid!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref ListItemType ReferenceAtItem(uint rawIndex) => ref listsItems[rawIndex];

		/// <summary>
		/// Returns the number of items in the list.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint GetLength(ListDescriptor descriptor) => lists[descriptor.Value].Length;

		/// <summary>
		/// Returns information about the list that is associated with this descriptor.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		/// <remarks>
		/// After changing the capacity of the pool, the ListInfo will become invalid!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ListInfo GetListInfo(ListDescriptor descriptor) => lists[descriptor.Value];

		/// <summary>
		/// Sets the number of links for the given list.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		/// <returns>Links count on the list after operation.</returns>
		/// <remarks>
		/// Changing the number of links does not delete the object.
		/// </remarks>
		public void SetLinksCount(ListDescriptor descriptor, uint linksCount)
		{
#if DebugRingPool
			if (linksCount > ListInfo.MaxLinkCount) {
				throw new InvalidOperationException("Links counter overflow!");
			}
#endif // DebugRingPool
			lists[descriptor.Value].LinkCount = linksCount;
		}

		/// <summary>
		/// Increments list links counter on the given number.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		/// <returns>Links count on the list after operation.</returns>
		/// <remarks>
		/// Changing the number of links does not delete the object.
		/// </remarks>
		public uint AddLinks(ListDescriptor descriptor, uint linksCount)
		{
			ref var list = ref lists[descriptor.Value];
#if DebugRingPool
			if (list.LinkCount + linksCount > ListInfo.MaxLinkCount) {
				throw new InvalidOperationException("Links counter overflow!");
			}
#endif // DebugRingPool
			return list.LinkCount += linksCount;
		}

		/// <summary>
		/// Decrements list links counter on the given number.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		/// <returns>Links count on the list after operation.</returns>
		/// <remarks>
		/// Changing the number of links does not delete the object.
		/// </remarks>
		public uint RemoveLinks(ListDescriptor descriptor, uint linksCount)
		{
			ref var list = ref lists[descriptor.Value];
			return list.LinkCount -= Math.Min(list.LinkCount, linksCount);
		}

		/// <summary>
		/// Creates a new empty list and returns its descriptor.
		/// </summary>
		public ListDescriptor AcquireList(uint linksCount = 1)
		{
			if (freeListIndices.Count == 0) {
				for (uint i = 0; i < lists.Length; i++) {
					freeListIndices.Enqueue((uint)lists.Length + i);
				}
				Array.Resize(ref lists, 2 * lists.Length);
			}
			uint redirectionIndex = freeListIndices.Dequeue();
			lists[redirectionIndex] = new ListInfo {
				LinkCount = linksCount,
				Offset = nextItemIndex,
				Length = 0
			};
			newestListDescriptor = (ListDescriptor)redirectionIndex;
			return newestListDescriptor;
		}

		/// <summary>
		/// Returns the resources of the list back to the pool for reuse.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		public void FreeOldestList(ListDescriptor descriptor)
		{
			var list = GetListInfo(descriptor);
#if DebugRingPool
			if (list.Offset != oldestItemIndex) {
				throw new InvalidOperationException("Wrong lists deleting order!");
			}
#endif // DebugRingPool
			oldestItemIndex = (oldestItemIndex + list.Length) % (uint)listsItems.Length;
			freeListIndices.Enqueue(descriptor.Value);
		}

		/// <summary>
		/// Returns the resources of the list back to the pool for reuse.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		public void FreeNewestList(ListDescriptor descriptor)
		{
			var list = GetListInfo(descriptor);
#if DebugRingPool
			if (list.Offset + list.Length != nextItemIndex) {
				throw new InvalidOperationException("Wrong lists deleting order!");
			}
#endif // DebugRingPool
			uint capacity = (uint)listsItems.Length;
			nextItemIndex = (nextItemIndex + capacity - list.Length) % capacity;
			freeListIndices.Enqueue(descriptor.Value);
		}

		/// <summary>
		/// Adds an item to the newest list.
		/// </summary>
		/// <remarks>
		/// May cause a resizing of the internal storage of items,
		/// which invalidate all <see cref="ListInfo"/> objects and
		/// also invalidate refs on <see cref="ListItemType"/>.
		/// </remarks>
		public void AddToNewestList(ListItemType item)
		{
			++lists[newestListDescriptor.Value].Length;
			listsItems[nextItemIndex] = item;
			nextItemIndex = AcquireNextIndex();
		}

		/// <summary>
		/// Reserve space for an item on the newest list.
		/// </summary>
		/// <remarks>
		/// May cause a resizing of the internal storage of items,
		/// which invalidate all <see cref="ListInfo"/> objects and
		/// also invalidate refs on <see cref="ListItemType"/>.
		/// </remarks>
		public ref ListItemType ReserveItemInNewestList()
		{
			++lists[newestListDescriptor.Value].Length;
			nextItemIndex = AcquireNextIndex();
			return ref listsItems[nextItemIndex];
		}

		private uint AcquireNextIndex()
		{
			uint index = (nextItemIndex + 1u) % (uint)listsItems.Length;
			if (index == oldestItemIndex) {
				lock (ResizeLockObject) {
					Array.Resize(ref listsItems, 2 * listsItems.Length);
					if (nextItemIndex < oldestItemIndex) {
						uint halfSize = (uint)listsItems.Length / 2u;
						uint srcIndex = oldestItemIndex;
						uint dstIndex = halfSize + oldestItemIndex;
						uint length = halfSize - oldestItemIndex;
						Array.Copy(listsItems, srcIndex, listsItems, dstIndex, length);
						oldestItemIndex = dstIndex;
						uint offset = dstIndex - srcIndex;
						for (int i = 0; i < lists.Length; i++) {
							ref var redirection = ref lists[i];
							if (redirection.Offset >= srcIndex) {
								redirection.Offset += offset;
							}
						}
					}
					index = nextItemIndex + 1;
				}
			}
			return index;
		}

		public struct ListInfo
		{
			/// <summary>
			/// LinksCount bits offset at <see cref="PackedLengthAndLinks"/>.
			/// </summary>
			private const int LinkCounterBitsOffset = 19;

			/// <summary>
			/// Links count bit mask.
			/// </summary>
			private const uint LinkCounterBitMask = (uint.MaxValue >> LinkCounterBitsOffset) << LinkCounterBitsOffset;

			/// <summary>
			/// Maximum number of links per list.
			/// </summary>
			public const uint MaxLinkCount = 1 << (32 - LinkCounterBitsOffset);

			/// <summary>
			/// Maximum length of one list.
			/// </summary>
			public const uint MaxLength = 1 << LinkCounterBitsOffset;

			/// <summary>
			/// Number of links and length compressed into one value.
			/// </summary>
			/// <remarks>
			/// We store the number of links to the list and his length together just to save memory.
			/// The RingPool is used by the profiler, which can create thousands of lists per frame.
			/// Usually these lists are short, stored for the next ~ 500 frames, and the size of one
			/// element corresponds to uint.
			/// </remarks>
			public uint PackedLengthAndLinks;

			/// <summary>
			/// The index of the first element of the list in <see cref="listsItems"/>.
			/// </summary>
			public uint Offset;

			public uint Length
			{
				get => PackedLengthAndLinks & ~LinkCounterBitMask;
				set => PackedLengthAndLinks = value | PackedLengthAndLinks & LinkCounterBitMask;
			}

			public uint LinkCount
			{
				get => PackedLengthAndLinks >> LinkCounterBitsOffset;
				set => PackedLengthAndLinks = PackedLengthAndLinks & ~LinkCounterBitMask | value << LinkCounterBitsOffset;
			}
		}

		public struct Enumerable
		{
			public ListDescriptor Descriptor;
			public RingPool<ListItemType> Pool;

			public Enumerable(ListDescriptor descriptor, RingPool<ListItemType> pool)
			{
				Descriptor = descriptor;
				Pool = pool;
			}

			public Enumerator GetEnumerator() => new Enumerator(Descriptor, Pool);

			public struct Enumerator
			{
				private RingPool<ListItemType> pool;
				private int processedItemsCount;
				private uint itemIndex;
				private uint itemsCount;

				public ListItemType Current { get; private set; }

				public Enumerator(ListDescriptor descriptor, RingPool<ListItemType> pool)
				{
					this.pool = pool;
					var redirection = pool.lists[descriptor.Value];
					itemsCount = redirection.Length;
					itemIndex = redirection.Offset;
					processedItemsCount = -1;
					Current = new ListItemType();
				}

				public bool MoveNext()
				{
					Current = pool.ItemAt(itemIndex);
					itemIndex = pool.NextListItemIndex(itemIndex);
					return ++processedItemsCount < itemsCount;
				}
			}
		}
	}
}

#endif // PROFILER
