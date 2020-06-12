using System;
using Yuzu;

namespace Lime.Graphics.Platform.Profiling
{
	/// <summary>
	/// It is a Node index or a Node-s indices list descriptor.
	/// Indexes are used to indicate owners of GPU or CPU usage periods.
	/// </summary>
	public struct Owners
	{
		private const uint ListBitMask = 0x_8000_0000;
		private const uint BatchBitMask = 0x_4000_0000;
		private const uint ThreadBitMask = 0x_2000_0000;
		private const uint FlagsBitMask = ListBitMask | BatchBitMask | ThreadBitMask;

		public const int ThreadBitOffset = 29;
		public const uint InvalidData = ~FlagsBitMask;

		public enum ThreadBit : uint
		{
			Render = ThreadBitMask,
			Update = 0x_0000_0000
		}

		[YuzuRequired]
		public uint Data;

		public bool IsValid => (Data & InvalidData) != InvalidData;

		public bool IsList
		{
			get => (Data & ListBitMask) == ListBitMask;
			set => Data = value ? Data | ListBitMask : Data & ~ListBitMask;
		}

		public bool IsBatch
		{
			get => (Data & BatchBitMask) == BatchBitMask;
			set => Data = value ? Data | BatchBitMask : Data & ~BatchBitMask;
		}

		public bool IsRenderThread
		{
			get => (Data & ThreadBitMask) == ThreadBitMask;
			set => Data = value ? Data | ThreadBitMask : Data & ~ThreadBitMask;
		}

		public bool IsUpdateThread
		{
			get => (Data & ThreadBitMask) == 0;
			set => Data = value ? Data & ~ThreadBitMask : Data | ThreadBitMask;
		}

		/// <summary>
		/// This property is only available if <see cref="IsList"/> is true.
		/// </summary>
		public uint Descriptor
		{
			get => Data & ~FlagsBitMask;
			set => Data = value | Data & FlagsBitMask;
		}

		/// <summary>
		/// This property is only available if <see cref="IsList"/> is false.
		/// </summary>
		public uint AsIndex
		{
			get => Data & ~FlagsBitMask;
			set => Data = value | Data & FlagsBitMask;
		}

		public Owners(uint value) => Data = value;

		public static explicit operator Owners(uint value) => new Owners(value);
	}

	public class OwnersPool
	{
		private readonly ReferencesTable[] tables;
		private readonly RingPool<uint>[] pools;

		private bool isLinksDoubling;
		private int itemsCreationsReferencesCount;

		public OwnersPool(ReferencesTable nodesTable)
		{
			var pool = new RingPool<uint>();
			pools = new RingPool<uint>[] { pool, pool };
			tables = new ReferencesTable[] { nodesTable, nodesTable };
		}

		public OwnersPool(ReferencesTable renderSafeNodesTable, ReferencesTable updateSafeNodesTable)
		{
			pools = new RingPool<uint>[] { new RingPool<uint>(), new RingPool<uint>() };
			tables = new ReferencesTable[] { updateSafeNodesTable, renderSafeNodesTable };
		}

		private uint GetTablePoolIndex(Owners owners) =>
			(owners.Data & (uint)Owners.ThreadBit.Render) >> Owners.ThreadBitOffset;

		private ReferencesTable GetTable(Owners owners) => tables[GetTablePoolIndex(owners)];

		private RingPool<uint> GetPool(Owners owners) => pools[GetTablePoolIndex(owners)];

		public Owners Acquire(IReferencesTableCompatible node, Owners.ThreadBit threadBit, bool isLinksDoubling)
		{
			if (node != null) {
				var table = tables[(uint)threadBit >> Owners.ThreadBitOffset];
				table.CreateOrAddReferenceTo(node, isLinksDoubling ? 2 : 1);
				return new Owners(node.ReferenceTableRowIndex | (uint)threadBit);
			} else {
				return new Owners(Owners.InvalidData | (uint)Owners.ThreadBit.Render);
			}
		}

		public Owners AcquireEmptyList(Owners.ThreadBit threadBit, bool isLinksDoubling)
		{
			this.isLinksDoubling = isLinksDoubling;
			itemsCreationsReferencesCount = isLinksDoubling ? 2 : 1;
			return new Owners((uint)threadBit);
		}

		public void AddToNewestList(ref Owners owners, IReferencesTableCompatible node)
		{
			var table = GetTable(owners);
			if (owners.IsBatch) {
				var pool = GetPool(owners);
				if (owners.IsList) {
					table.CreateOrAddReferenceTo(node, itemsCreationsReferencesCount);
					pool.AddToNewestList(owners.Descriptor, node.ReferenceTableRowIndex);
				} else {
					table.CreateOrAddReferenceTo(node, itemsCreationsReferencesCount);
					uint descriptor = pool.AcquireList(isDoubleDeletion: isLinksDoubling);
					pool.AddToNewestList(descriptor, owners.AsIndex);
					pool.AddToNewestList(descriptor, node.ReferenceTableRowIndex);
					owners = new Owners(descriptor) { IsList = true };
				}
			} else {
				table.CreateOrAddReferenceTo(node, itemsCreationsReferencesCount);
				owners.IsBatch = true;
				owners.AsIndex = node.ReferenceTableRowIndex;
			}
		}

		public void FreeOldest(Owners owners)
		{
			var table = GetTable(owners);
			var pool = GetPool(owners);
			if (owners.IsList) {
				if (pool.HasDoubleDeletionFlag(owners.Descriptor)) {
					pool.RemoveDoubleDeletionFlag(owners.Descriptor);
				} else {
					foreach (var rowIndex in pool.Enumerate(owners.Descriptor)) {
						table.RemoveReferenceTo(rowIndex);
					}
					pool.FreeOldestList(owners.Descriptor);
				}
			} else {
				table.RemoveReferenceTo(owners.AsIndex);
			}
		}

		public void FreeNewest(Owners owners)
		{
			var table = GetTable(owners);
			var pool = GetPool(owners);
			if (owners.IsList) {
				if (pool.HasDoubleDeletionFlag(owners.Descriptor)) {
					pool.RemoveDoubleDeletionFlag(owners.Descriptor);
				} else {
					foreach (var rowIndex in pool.Enumerate(owners.Descriptor)) {
						table.RemoveReferenceTo(rowIndex);
					}
					pool.FreeNewestList(owners.Descriptor);
				}
			} else {
				table.RemoveReferenceTo(owners.AsIndex);
			}
		}
	}

	public static class NativeOwnersPool
	{
		public static readonly OwnersPool Instance =
			new OwnersPool(
				NativeNodesTables.RenderOnlyTable,
				NativeNodesTables.UpdateOnlyTable
			);

		public static Owners Acquire(IReferencesTableCompatible node, Owners.ThreadBit threadBit, bool isLinksDoubling = false) =>
			Instance.Acquire(node, threadBit, isLinksDoubling);

		public static Owners AcquireEmptyList(Owners.ThreadBit threadBit, bool isLinksDoubling) =>
			Instance.AcquireEmptyList(threadBit, isLinksDoubling);

		public static void AddToNewest(ref Owners owners, IReferencesTableCompatible @object) =>
			Instance.AddToNewestList(ref owners, @object);

		public static void FreeOldest(Owners owners) => Instance.FreeOldest(owners);

		public static void FreeNewest(Owners owners) => Instance.FreeNewest(owners);
	}
}
