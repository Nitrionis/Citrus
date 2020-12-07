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
			/// <remarks>~0x_8000_0000 is hack for compatibility with <see cref="Owners"/> struct.</remarks>
			private const uint InvalidValue = uint.MaxValue & ~0x_8000_0000;

			/// <summary>
			/// Creates an invalid descriptor.
			/// </summary>
			public static ListDescriptor Null => (ListDescriptor)InvalidValue;

			public uint Value;

			public bool IsNull => Value == InvalidValue;

			public static explicit operator uint(ListDescriptor descriptor) => descriptor.Value;
			public static explicit operator ListDescriptor(uint value) => new ListDescriptor { Value = value };

			public ListDescriptor(uint value) => Value = value;
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

		/// <remarks>
		/// The ring pool should not be accessed within this event. This will cause undefined behavior.
		/// </remarks>
		public event Action InternalStorageExpanding;

		/// <remarks>
		/// The ring pool should not be accessed within this event. This will cause undefined behavior.
		/// </remarks>
		public event Action<int> InternalStorageExpanded;

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
		/// Returns an reversed enumerator for listing all objects in the list.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		/// <remarks>
		/// After changing the capacity of the pool, the enumerator will be invalid.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReverseEnumerable Reversed(ListDescriptor descriptor) => new ReverseEnumerable(descriptor, this);

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
		/// Creates a new empty list and returns its descriptor.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ListDescriptor AcquireList()
		{
			if (freeListIndices.Count == 0) {
				ResizeListsDescriptionsStorage();
			}
			uint redirectionIndex = freeListIndices.Dequeue();
			lists[redirectionIndex] = new ListInfo {
				Offset = nextItemIndex,
				Length = 0
			};
			return newestListDescriptor = new ListDescriptor(redirectionIndex);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void ResizeListsDescriptionsStorage()
		{
			for (uint i = 0; i < lists.Length; i++) {
				freeListIndices.Enqueue((uint)lists.Length + i);
			}
			Array.Resize(ref lists, 2 * lists.Length);
		}

		/// <summary>
		/// Returns the resources of the list back to the pool for reuse.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		public void FreeOldestList(ListDescriptor descriptor)
		{
			var list = lists[descriptor.Value];
			if (list.IsAcquired) {
				lists[descriptor.Value].IsAcquired = false;
#if DebugRingPool
				if (list.Offset != oldestItemIndex) {
					throw new InvalidOperationException("Wrong lists deleting order!");
				}
#endif // DebugRingPool
				oldestItemIndex = (oldestItemIndex + list.Length) % (uint)listsItems.Length;
				freeListIndices.Enqueue(descriptor.Value);
			}
		}

		/// <summary>
		/// Returns the resources of the list back to the pool for reuse.
		/// </summary>
		/// <param name="descriptor">List descriptor acquired from this pool.</param>
		public void FreeNewestList(ListDescriptor descriptor)
		{
			var list = lists[descriptor.Value];
			if (list.IsAcquired) {
				lists[descriptor.Value].IsAcquired = false;
#if DebugRingPool
				if (list.Offset + list.Length != nextItemIndex) {
					throw new InvalidOperationException("Wrong lists deleting order!");
				}
#endif // DebugRingPool
				uint capacity = (uint)listsItems.Length;
				nextItemIndex = (nextItemIndex + capacity - list.Length) % capacity;
				freeListIndices.Enqueue(descriptor.Value);
			}
		}

		/// <summary>
		/// Adds an item to the newest list.
		/// </summary>
		/// <remarks>
		/// May cause a resizing of the internal storage of items,
		/// which invalidate all <see cref="ListInfo"/> objects and
		/// also invalidate refs on <see cref="ListItemType"/>.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddToNewestList(ListItemType item)
		{
			++lists[newestListDescriptor.Value].Length;
			listsItems[nextItemIndex] = item;
			nextItemIndex = AcquireNextIndex();
		}

		/// <summary>
		/// Adds an items to the newest list.
		/// </summary>
		/// <param name="items"></param>
		/// <param name="count"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddToNewestList(ListItemType[] items, uint count)
		{
			lists[newestListDescriptor.Value].Length += count;
			for (int i = 0; i < count; i++) {
				listsItems[nextItemIndex] = items[i];
				nextItemIndex = AcquireNextIndex();
			}
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
			uint lastItemIndex = nextItemIndex;
			nextItemIndex = AcquireNextIndex();
			return ref listsItems[lastItemIndex];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint AcquireNextIndex()
		{
			uint index = (nextItemIndex + 1u) % (uint)listsItems.Length;
			if (index == oldestItemIndex) {
				ResizeListsItemsStorage(out index);
			}
			return index;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void ResizeListsItemsStorage(out uint index)
		{
			InternalStorageExpanding?.Invoke();
			int oldLength = listsItems.Length;
			int NextListsItemsLength() =>
				listsItems.Length < 262_144 ?
				listsItems.Length * 2 : listsItems.Length * 3 / 2;
			Array.Resize(ref listsItems, NextListsItemsLength());
			if (nextItemIndex < oldestItemIndex) {
				int shift = listsItems.Length - oldLength;
				int srcIndex = (int)oldestItemIndex;
				int dstIndex = shift + srcIndex;
				int length = oldLength - srcIndex;
				Array.Copy(listsItems, srcIndex, listsItems, dstIndex, length);
				oldestItemIndex = (uint)dstIndex;
				for (int i = 0; i < lists.Length; i++) {
					ref var redirection = ref lists[i];
					if (redirection.Offset >= srcIndex && redirection.IsAcquired) {
						redirection.Offset += (uint)shift;
					}
				}
			}
			index = nextItemIndex + 1;
			InternalStorageExpanded?.Invoke(listsItems.Length);
		}

		public struct ListInfo
		{
			/// <summary>
			/// The index of the first element of the list in <see cref="listsItems"/>.
			/// </summary>
			public uint Offset;

			public uint Length;

			public bool IsAcquired
			{
				get => Offset != uint.MaxValue;
				set => Offset = value ? throw new InvalidOperationException() : uint.MaxValue;
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

			public void Reverse() { }

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Enumerator GetEnumerator() => new Enumerator(Descriptor, Pool);

			public struct Enumerator
			{
				private ListItemType[] items;
				private int processedItemsCount;
				private int itemIndex;
				private int itemsCount;

				public ListItemType Current { get; private set; }

				public Enumerator(ListDescriptor descriptor, RingPool<ListItemType> pool)
				{
					items = pool.listsItems;
					var redirection = pool.lists[descriptor.Value];
					itemsCount = (int)redirection.Length;
					itemIndex = (int)redirection.Offset;
					processedItemsCount = -1;
					Current = new ListItemType();
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public bool MoveNext()
				{
					Current = items[itemIndex];
					itemIndex = (itemIndex + 1) % items.Length;
					return ++processedItemsCount < itemsCount;
				}
			}
		}

		public struct ReverseEnumerable
		{
			public ListDescriptor Descriptor;
			public RingPool<ListItemType> Pool;

			public ReverseEnumerable(ListDescriptor descriptor, RingPool<ListItemType> pool)
			{
				Descriptor = descriptor;
				Pool = pool;
			}

			public void Reverse() { }

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Enumerator GetEnumerator() => new Enumerator(Descriptor, Pool);

			public struct Enumerator
			{
				private ListItemType[] items;
				private int processedItemsCount;
				private int itemIndex;
				private int itemsCount;

				public ListItemType Current { get; private set; }

				public Enumerator(ListDescriptor descriptor, RingPool<ListItemType> pool)
				{
					items = pool.listsItems;
					var redirection = pool.lists[descriptor.Value];
					itemsCount = (int)redirection.Length;
					itemIndex = (int)redirection.Offset + itemsCount - 1;
					processedItemsCount = -1;
					Current = new ListItemType();
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public bool MoveNext()
				{
					Current = items[itemIndex];
					itemIndex = (itemIndex + items.Length - 1) % items.Length;
					return ++processedItemsCount < itemsCount;
				}
			}
		}
	}
}

#endif // PROFILER
