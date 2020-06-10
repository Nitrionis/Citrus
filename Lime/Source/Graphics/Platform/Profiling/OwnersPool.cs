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
		public const uint ListBit = 0x_8000_0000;
		public const uint BatchBit = 0x_4000_0000;
		public const uint ThreadBit = 0x_2000_0000;
		public const uint FlagsBitMask = ListBit | BatchBit | ThreadBit;

		[YuzuRequired]
		public uint Data;

		public bool IsList
		{
			get => (Data & ListBit) == ListBit;
			set => Data = value ? Data | ListBit : Data & ~ListBit;
		}

		public bool IsBatch
		{
			get => (Data & BatchBit) == BatchBit;
			set => Data = value ? Data | BatchBit : Data & ~BatchBit;
		}

		public bool IsRenderThread
		{
			get => (Data & ThreadBit) == ThreadBit;
			set => Data = value ? Data | ThreadBit : Data & ~ThreadBit;
		}

		public bool IsUpdateThread
		{
			get => (Data & ThreadBit) == 0;
			set => Data = value ? Data & ~ThreadBit : Data | ThreadBit;
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
		private readonly ReferencesTable renderSafeNodesTable;
		private readonly ReferencesTable updateSafeNodesTable;

		private readonly RingPool<uint> renderSafeListsPool;
		private readonly RingPool<uint> updateSafeListsPool;

		private bool isListLinksDoubling;
		private int listItemsCreationsReferencesCount;

		public OwnersPool(ReferencesTable nodesTable)
		{
			renderSafeNodesTable = updateSafeNodesTable = nodesTable;
			renderSafeListsPool = updateSafeListsPool = new RingPool<uint>();
		}

		public OwnersPool(ReferencesTable renderSafeNodesTable, ReferencesTable updateSafeNodesTable)
		{
			this.renderSafeNodesTable = renderSafeNodesTable;
			this.updateSafeNodesTable = updateSafeNodesTable;
			renderSafeListsPool = new RingPool<uint>();
			updateSafeListsPool = new RingPool<uint>();
		}

		public Owners Acquire(bool isListLinksDoubling)
		{
			this.isListLinksDoubling = isListLinksDoubling;
			listItemsCreationsReferencesCount = isListLinksDoubling ? 2 : 1;
			return new Owners();
		}

		public void AddToNewest(ref Owners owners, IReferencesTableCompatible node)
		{
			var table = owners.IsRenderThread ? renderSafeNodesTable : updateSafeNodesTable;
			if (owners.IsBatch) {
				var pool = owners.IsRenderThread ? renderSafeListsPool : updateSafeListsPool;
				if (owners.IsList) {
					table.CreateOrAddReferenceTo(node, listItemsCreationsReferencesCount);
					pool.AddToNewestList(owners.Descriptor, node.ReferenceTableRowIndex);
				} else {
					table.CreateOrAddReferenceTo(node, listItemsCreationsReferencesCount);
					uint descriptor = pool.AcquireList(isDoubleDeletion: isListLinksDoubling);
					pool.AddToNewestList(descriptor, owners.AsIndex);
					pool.AddToNewestList(descriptor, node.ReferenceTableRowIndex);
					owners = new Owners(descriptor) { IsList = true };
				}
			} else {
				table.CreateOrAddReferenceTo(node, listItemsCreationsReferencesCount);
				owners.IsBatch = true;
				owners.AsIndex = node.ReferenceTableRowIndex;
			}
		}

		public void FreeOldest(Owners owners)
		{
			var table = owners.IsRenderThread ? renderSafeNodesTable : updateSafeNodesTable;
			var pool = owners.IsRenderThread ? renderSafeListsPool : updateSafeListsPool;
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
			var table = owners.IsRenderThread ? renderSafeNodesTable : updateSafeNodesTable;
			var pool = owners.IsRenderThread ? renderSafeListsPool : updateSafeListsPool;
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

		public static Owners AcquireList(bool isListLinksDoubling) => Instance.Acquire(isListLinksDoubling);

		public static void AddToNewest(ref Owners owners, IReferencesTableCompatible @object) =>
			Instance.AddToNewest(ref owners, @object);

		public static void FreeOldest(Owners owners) => Instance.FreeOldest(owners);

		public static void FreeNewest(Owners owners) => Instance.FreeNewest(owners);
	}
}
