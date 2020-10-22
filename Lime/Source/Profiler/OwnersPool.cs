#if PROFILER

namespace Lime.Profiler
{
	/// <summary>
	/// Represents the index of a row in a lookup table, or a list descriptor of such indexes.
	/// </summary>
	public struct Owners
	{
		private const uint ListBitMask = 0x_8000_0000;
		private const uint FlagsBitMask = ListBitMask;

		private const uint InvalidData = ~FlagsBitMask;

		/// <summary>
		/// Use to initialize an empty instance of the Owners structure.
		/// </summary>
		public static Owners Empty => new Owners { PackedData = InvalidData };

		private uint PackedData;

		/// <summary>
		/// If true, then Owners is neither RingPool.ListDescriptor nor ReferenceTable.RowIndex.
		/// </summary>
		public bool IsEmpty => (PackedData & InvalidData) == InvalidData;

		/// <summary>
		/// If true, then Owners is ReferenceTable list descriptor. 
		/// </summary>
		public bool IsListDescriptor
		{
			get => (PackedData & ListBitMask) == ListBitMask;
			set => PackedData = value ? PackedData | ListBitMask : PackedData & ~ListBitMask;
		}

		/// <summary>
		/// Interprets the Owners as RingPool<ReferenceTable.RowIndex>.ListDescriptor.
		/// </summary>
		/// <remarks>
		/// This property is only available if <see cref="IsListDescriptor"/> is true.
		/// </remarks>
		public RingPool.ListDescriptor AsListDescriptor
		{
			get => (RingPool.ListDescriptor)(PackedData & ~FlagsBitMask);
			set => PackedData = (uint)value | PackedData & FlagsBitMask;
		}

		/// <summary>
		/// Interprets the Owners as ReferencesTable.RowIndex.
		/// </summary>
		/// <remarks>
		/// This property is only valid if <see cref="IsListDescriptor"/> is false.
		/// </remarks>
		public ReferenceTable.RowIndex AsIndex
		{
			get => (ReferenceTable.RowIndex)(PackedData & ~FlagsBitMask);
			set => PackedData = (uint)value | PackedData & FlagsBitMask;
		}

		public static explicit operator Owners(uint value) => new Owners { PackedData = value };
	}

	public class OwnersPool
	{
		public const int NodesTablesCount = 2;

		private readonly ReferenceTable[] tables;

		/// <summary>
		/// 
		/// </summary>
		public RingPool<uint> BatchIndicesListsPool { get; }

		private uint linksCount;

		public OwnersPool(ReferenceTable renderSafeNodesTable, ReferenceTable updateSafeNodesTable)
		{
			BatchIndicesListsPool = new RingPool<uint>();
			tables = new ReferenceTable[] { updateSafeNodesTable, renderSafeNodesTable };
		}

		private uint GetTableIndex(Owners owners) =>
			(owners.PackedData & Owners.ThreadBitMask) >> Owners.ThreadBitOffset;

		public ReferenceTable GetTable(Owners.ThreadBit threadBit) =>
			tables[(uint)threadBit >> Owners.ThreadBitOffset];

		public ReferenceTable GetTable(Owners owners) => tables[GetTableIndex(owners)];

		public Owners Acquire(NativeNodesTables.ICompatible owner, Owners.ThreadBit threadBit, uint linksCount)
		{
			var table = tables[(uint)threadBit >> Owners.ThreadBitOffset];
			uint rowIndex = owner == null ?
				Owners.InvalidData : table.CreateOrAddReferenceTo(owner, (int)linksCount);
			return new Owners(rowIndex | (uint)threadBit);
		}

		public Owners AcquireEmptyList() => new Owners(Owners.InvalidData | (uint)Owners.ThreadBit.Render);

		public void AddToNewestList(ref Owners owners, NativeNodesTables.ICompatible owner)
		{
			var table = GetTable(owners);
			if (owners.IsBatch) {
				var pool = BatchIndicesListsPool;
				if (!owners.IsListDescriptor) {
					uint descriptor = pool.AcquireList(linksCount);
					pool.AddToNewestList(descriptor, owners.AsIndex);
					owners.AsListDescriptor = descriptor;
					owners.IsListDescriptor = true;
				}
				pool.AddToNewestList(owners.AsListDescriptor, owner == null ?
					Owners.InvalidData : table.CreateOrAddReferenceTo(owner));
			} else {
				owners.IsBatch = true;
				owners.AsIndex = owner == null ?
					Owners.InvalidData : table.CreateOrAddReferenceTo(owner);
			}
		}

		public void AddLinks(Owners owners, uint count)
		{
			if (owners.IsListDescriptor) {
				var pool = BatchIndicesListsPool;
				uint linksCount = pool.GetLinksCount(owners.AsListDescriptor);
				pool.SetLinksCount(owners.AsListDescriptor, linksCount + count);
			} else {
				var table = GetTable(owners);
				table.AddReferenceTo(owners.AsIndex, count);
			}
		}

		public void FreeOldest(Owners owners)
		{
			var table = GetTable(owners);
			if (owners.IsListDescriptor) {
				var pool = BatchIndicesListsPool;
				if (pool.GetLinksCount(owners.AsListDescriptor) == 1) {
					foreach (var rowIndex in pool.Enumerate(owners.AsListDescriptor)) {
						if (rowIndex != Owners.InvalidData) {
							table.RemoveReferenceTo(rowIndex);
						}
					}
					pool.FreeOldestList(owners.AsListDescriptor);
				}
			} else {
				table.RemoveReferenceTo(owners.AsIndex);
			}
		}

		public void FreeNewest(Owners owners)
		{
			var table = GetTable(owners);
			if (owners.IsListDescriptor) {
				var pool = BatchIndicesListsPool;
				if (pool.GetLinksCount(owners.AsListDescriptor) == 1) {
					foreach (var rowIndex in pool.Enumerate(owners.AsListDescriptor)) {
						if (rowIndex != Owners.InvalidData) {
							table.RemoveReferenceTo(rowIndex);
						}
					}
					pool.FreeNewestList(owners.AsListDescriptor);
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

		/// <summary>
		/// Used to store link to single object.
		/// </summary>
		public static Owners Acquire(NativeNodesTables.ICompatible node, Owners.ThreadBit threadBit, uint linksCount) =>
			Instance.Acquire(node, threadBit, linksCount);

		/// <summary>
		/// Used to store links to owners of batches.
		/// </summary>
		public static Owners AcquireEmptyList() => Instance.AcquireEmptyList();

		public static void AddToNewestList(ref Owners owners, NativeNodesTables.ICompatible @object) =>
			Instance.AddToNewestList(ref owners, @object);

		public static void AddLinks(Owners owners, uint count) => Instance.AddLinks(owners, count);

		public static void FreeOldest(Owners owners) => Instance.FreeOldest(owners);

		public static void FreeNewest(Owners owners) => Instance.FreeNewest(owners);
	}
}
#endif // PROFILER
