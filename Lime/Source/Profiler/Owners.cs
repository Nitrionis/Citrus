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
		public static Owners Empty => new Owners { packedData = InvalidData };

		private uint packedData;

		public uint PackedData => packedData;

		/// <summary>
		/// If true, then Owners is neither RingPool.ListDescriptor nor ReferenceTable.RowIndex.
		/// </summary>
		public bool IsEmpty => (packedData & InvalidData) == InvalidData;

		/// <summary>
		/// If true, then Owners is ReferenceTable list descriptor. 
		/// </summary>
		public bool IsListDescriptor
		{
			get => (packedData & ListBitMask) == ListBitMask;
			set => packedData = value ? packedData | ListBitMask : packedData & ~ListBitMask;
		}

		/// <summary>
		/// Interprets the Owners as RingPool<ReferenceTable.RowIndex>.ListDescriptor.
		/// </summary>
		/// <remarks>
		/// This property is only available if <see cref="IsListDescriptor"/> is true.
		/// </remarks>
		public RingPool.ListDescriptor AsListDescriptor
		{
			get => (RingPool.ListDescriptor)(packedData & ~FlagsBitMask);
			set => packedData = (uint)value | packedData & FlagsBitMask;
		}

		/// <summary>
		/// Interprets the Owners as ReferencesTable.RowIndex.
		/// </summary>
		/// <remarks>
		/// This property is only valid if <see cref="IsListDescriptor"/> is false.
		/// </remarks>
		public ReferenceTable.RowIndex AsIndex
		{
			get => (ReferenceTable.RowIndex)(packedData & ~FlagsBitMask);
			set => packedData = (uint)value | packedData & FlagsBitMask;
		}

		public Owners(ReferenceTable.RowIndex rowIndex) => packedData = (uint)rowIndex;

		public Owners(IProfileableObject @object) : this(@object?.RowIndex ?? ReferenceTable.RowIndex.Invalid) { }

		public Owners(RingPool.ListDescriptor descriptor) => packedData = (uint)descriptor | ListBitMask;
	}
}
#endif // PROFILER
