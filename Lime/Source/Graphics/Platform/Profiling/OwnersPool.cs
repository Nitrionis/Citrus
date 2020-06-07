using Yuzu;

namespace Lime.Graphics.Platform.Profiling
{
	/// <summary>
	/// It is a Node index or a Node-s indices list descriptor.
	/// Indexes are used to indicate owners of GPU or CPU usage periods.
	/// </summary>
	public struct Owners
	{
		private const uint ListBit = 0x_8000_0000;
		private const uint BatchBit = 0x_4000_0000;
		private const uint FlagsBitMask = ListBit | BatchBit;

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
		public readonly RingPool<uint> Pool = new RingPool<uint>();

		public Owners AcquireList() => new Owners();

		public void AddToNewestList(ref Owners list, uint item)
		{
			if (list.IsBatch) {
				if (list.IsList) {
					Pool.AddToNewestList(list.Descriptor, item);
				} else {
					uint descriptor = Pool.AcquireList(isDoubleDeletion: true);
					Pool.AddToNewestList(descriptor, list.AsIndex);
					Pool.AddToNewestList(descriptor, item);
					list = new Owners(descriptor) { IsList = true };
				}
			} else {
				list.IsBatch = true;
				list.AsIndex = item;
			}
		}

		public void FreeOldest(Owners owners)
		{
			if (owners.IsList) {
				if (Pool.HasDoubleDeletionFlag(owners.Descriptor)) {
					Pool.RemoveDoubleDeletionFlag(owners.Descriptor);
				} else {
					foreach (var rowIndex in Pool.Enumerate(owners.Descriptor)) {
						NativeNodesTable.RemoveReferenceTo(rowIndex);
					}
					Pool.FreeOldestList(owners.Descriptor);
				}
			} else {
				NativeNodesTable.RemoveReferenceTo(owners.AsIndex);
			}
		}

		public void FreeNewest(Owners owners)
		{
			if (owners.IsList) {
				if (Pool.HasDoubleDeletionFlag(owners.Descriptor)) {
					Pool.RemoveDoubleDeletionFlag(owners.Descriptor);
				} else {
					foreach (var rowIndex in Pool.Enumerate(owners.Descriptor)) {
						NativeNodesTable.RemoveReferenceTo(rowIndex);
					}
					Pool.FreeNewestList(owners.Descriptor);
				}
			} else {
				NativeNodesTable.RemoveReferenceTo(owners.AsIndex);
			}
		}
	}

	public static class NativeOwnersPool
	{
		public static readonly OwnersPool Pool = new OwnersPool();

		public static Owners AcquireList() => Pool.AcquireList();

		public static void AddToNewestList(ref Owners list, uint item) => Pool.AddToNewestList(ref list, item);

		public static void FreeOldest(Owners list) => Pool.FreeOldest(list);

		public static void FreeNewest(Owners list) => Pool.FreeNewest(list);
	}
}
