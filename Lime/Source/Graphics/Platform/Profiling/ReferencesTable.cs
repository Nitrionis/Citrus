using System;
using System.Collections.Generic;

namespace Lime.Graphics.Platform.Profiling
{
	public interface IReferencesTableCompatible
	{
		uint ReferenceTableRowIndex { get; set; }
	}

	public class ReferencesTable
	{
		public const uint InvalidReference = uint.MaxValue;

		private struct ReferenceCounter
		{
			public IReferencesTableCompatible Object;
			public int ReferencesCount;
		}

		private ReferenceCounter[] table;
		private readonly Stack<uint> freeRows;
		private uint rowsCount;

		public ReferencesTable(uint startCapacity = 1024)
		{
			rowsCount = startCapacity;
			table = new ReferenceCounter[rowsCount];
			freeRows = new Stack<uint>(table.Length);
			for (uint i = 0; i < table.Length; i++) {
				table[i] = new ReferenceCounter();
				freeRows.Push(i);
			}
		}

		public IReferencesTableCompatible this[uint rowIndex] => table[rowIndex].Object;

		public void CreateOrAddReferenceTo(IReferencesTableCompatible @object, int referencesCount = 1)
		{
			if (@object.ReferenceTableRowIndex != InvalidReference) {
				table[@object.ReferenceTableRowIndex].ReferencesCount += referencesCount;
			} else {
				if (freeRows.Count > 0) {
					uint rowIndex = freeRows.Pop();
					@object.ReferenceTableRowIndex = rowIndex;
					ref var row = ref table[rowIndex];
					row.Object = @object;
					row.ReferencesCount = referencesCount;
				} else {
					@object.ReferenceTableRowIndex = rowsCount;
					if (table.Length == rowsCount) {
						Array.Resize(ref table, 2 * table.Length);
					}
					table[rowsCount++] = new ReferenceCounter {
						Object = @object,
						ReferencesCount = referencesCount
					};
				}
			}
		}

		public void RemoveReferenceTo(uint rowIndex)
		{
			ref var row = ref table[rowIndex];
			if (--row.ReferencesCount == 0) {
				row.Object.ReferenceTableRowIndex = InvalidReference;
				row.Object = null;
				freeRows.Push(rowIndex);
			}
		}
	}

	public static class NativeNodesTables
	{
		/// <summary>
		/// Table accessed only from update thread.
		/// </summary>
		public static readonly ReferencesTable UpdateOnlyTable = new ReferencesTable();

		/// <summary>
		/// Table accessed only from render thread.
		/// </summary>
		public static readonly ReferencesTable RenderOnlyTable = new ReferencesTable();
	}
}
