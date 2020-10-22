#if PROFILER

using System;
using System.Collections.Generic;

namespace Lime.Profiler
{
	/// <summary>
	/// Used to compactly store data about each object that the profiler encounters.
	/// </summary>
	public class ReferenceTable
	{
		private TableRow[] rows;
		private readonly Queue<RowIndex> freeRows;
		private readonly uint descriptionLifespan;

		public IObjectDescription this[uint rowIndex] => rows[rowIndex].ObjectDescription;

		/// <param name="descriptionLifespan">
		/// The number of frames during which a description remains alive since the last use.
		/// </param>
		public ReferenceTable(uint descriptionLifespan, uint startCapacity = 1024)
		{
			this.descriptionLifespan = descriptionLifespan;
			rows = new TableRow[startCapacity];
			freeRows = new Queue<RowIndex>(rows.Length);
			for (uint i = 0; i < rows.Length; i++) {
				freeRows.Enqueue((RowIndex)i);
			}
		}

		/// <summary>
		/// Since we store a hierarchy of objects for each object that the profiler touched,
		/// we must handle the event of an object disconnected from the main hierarchy.
		/// </summary>
		/// <remarks>
		/// Must be called for every object in the detached subtree.
		/// </remarks>
		public static void ObjectDetachedFromMainHierarchy(object @object)
		{
			if (@object is IProfileableObject profileableObject) {
				profileableObject.RowIndex = RowIndex.Invalid;
			}
		}

		/// <summary>
		/// Adds a description to the table for all objects in the subtree rooted in "Node" including the root.
		/// </summary>
		public void CreateDescriptionFor(IProfileableObject @object) => CreateDescriptionRecursively(@object);

		private RowIndex CreateDescriptionRecursively(IProfileableObject @object)
		{
			var rowIndex = @object.RowIndex;
			if (!rowIndex.IsValid) {
				rowIndex = AcquireRow();
				var description = new ObjectDescription {
					ParentRowIndex = CreateDescriptionRecursively(@object.Parent),
					Reference = new WeakReference<IProfileableObject>(@object),
					Type = @object.GetType(),
					Name = @object.Name
				};
				rows[rowIndex.Value] = new TableRow {
					ObjectDescription = description,
					FrameIndex = -1
				};
			} else {
				rows[rowIndex.Value].FrameIndex = -1;
			}
			return rowIndex;
		}

		private RowIndex AcquireRow()
		{
			if (freeRows.Count == 0) {
				var previousLength = (uint)rows.Length;
				Array.Resize(ref rows, rows.Length * 2);
				for (uint i = previousLength; i < rows.Length; i++) {
					freeRows.Enqueue((RowIndex)i);
				}
			}
			return freeRows.Dequeue();
		}

		/// <summary>
		/// Tells the table that the description for the object is still in use.
		/// </summary>
		/// <remarks>
		/// Also should be called immediately after creating the description.
		/// And in any case, do not specify the index of a frame that has not yet been profiled.
		/// </remarks>
		public void UpdateFrameIndexFor(RowIndex rowIndex, long frameIndex) =>
			rows[rowIndex.Value].FrameIndex = frameIndex;

		/// <summary>
		/// Releases obsolete objects.
		/// </summary>
		public void CollectGarbage()
		{
			long lastFrame = -1;
			for (uint rowIndex = 0; rowIndex < rows.Length; rowIndex++) {
				lastFrame = Math.Max(lastFrame, rows[rowIndex].FrameIndex);
			}
			long minFrameIndexToStayAlive = lastFrame - descriptionLifespan;
			for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++) {
				ref var row = ref rows[rowIndex];
				if (!row.IsEmpty && row.FrameIndex >= minFrameIndexToStayAlive) {
					var parentRowIndex = row.ObjectDescription.ParentRowIndex;
					while (parentRowIndex.IsValid) {
						ref var parentRow = ref rows[parentRowIndex.Value];
						if (parentRow.FrameIndex < minFrameIndexToStayAlive) {
							parentRow.FrameIndex = minFrameIndexToStayAlive;
						} else {
							parentRowIndex = RowIndex.Invalid;
						}
					}
				}
			}
			for (uint rowIndex = 0; rowIndex < rows.Length; rowIndex++) {
				ref var row = ref rows[rowIndex];
				if (!row.IsEmpty && row.FrameIndex < minFrameIndexToStayAlive) {
					row.ObjectDescription = null;
					freeRows.Enqueue((RowIndex)rowIndex);
				}
			}
		}

		private struct TableRow
		{
			/// <summary>
			/// The index of frame in which the line was updated or created.
			/// </summary>
			/// <remarks>
			/// Used to identify obsolete objects.
			/// </remarks>
			public long FrameIndex;

			/// <summary>
			/// Description of some object.
			/// </summary>
			public IObjectDescription ObjectDescription;

			public bool IsEmpty => ObjectDescription == null;
		}

		/// <summary>
		/// Description of some object.
		/// </summary>
		/// <remarks>
		/// Mostly used to describe a certain Node.
		/// In this case, the name is the Node Id.
		/// </remarks>
		public interface IObjectDescription
		{
			/// <summary>
			/// If this element has a parent, then a description will be created for the parent too.
			/// </summary>
			RowIndex ParentRowIndex { get; }

			/// <summary>
			/// Object name for which this description was created.
			/// </summary>
			string Name { get; }

			/// <summary>
			/// Object type for which this description was created.
			/// </summary>
			Type Type { get; }

			/// <summary>
			/// Reference to the object for which the description was created.
			/// </summary>
			WeakReference<IProfileableObject> Reference { get; }
		}

		private class ObjectDescription : IObjectDescription
		{
			public RowIndex ParentRowIndex { get; set; }

			public string Name { get; set; }

			public Type Type { get; set; }

			public WeakReference<IProfileableObject> Reference { get; set; }
		}

		public struct RowIndex
		{
			private const uint InvalidValue = uint.MaxValue;

			public static RowIndex Invalid => new RowIndex { Value = InvalidValue };

			public uint Value;

			public bool IsValid => Value != InvalidValue;

			public static explicit operator uint(RowIndex rowIndex) => rowIndex.Value;
			public static explicit operator RowIndex(uint value) => new RowIndex { Value = value };
		}
	}

	/// <remarks>
	/// This class can only be accessed from the side of the device that is profiling.
	/// </remarks>
	public static class NativeReferenceTable
	{
		private static ReferenceTable table = new ReferenceTable();


	}
}

#endif // PROFILER
