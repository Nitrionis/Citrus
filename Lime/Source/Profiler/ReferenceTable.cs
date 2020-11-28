#if PROFILER

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lime.Profiler
{
	/// <summary>
	/// Used to compactly store data about each object that the profiler encounters.
	/// </summary>
	public class ReferenceTable
	{
		private TableRow[] rows;
		private readonly Queue<RowIndex> freeRows;

		/// <summary>
		/// The number of new descriptions in the table since the last garbage collection.
		/// </summary>
		public uint NewDescriptionsCount { get; private set; }

		public IObjectDescription this[uint rowIndex] => rows[rowIndex].ObjectDescription;

		public ReferenceTable(uint startCapacity = 1024)
		{
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
		/// If this method is called outside the update thread, the object should not be used in the update thread.
		/// </remarks>
		public static void ObjectDetachedFromMainHierarchy(object @object)
		{
			if (@object is IProfileableObject profileableObject) {
				profileableObject.RowIndex = RowIndex.Invalid;
			}
		}

		/// <summary>
		/// Ensure a description for each object in the branch, moving from @object to root.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureDescriptionFor(IProfileableObject @object)
		{
			ObjectDescription description = null;
			while (@object != null && !@object.RowIndex.IsValid) {
				var rowIndex = AcquireRow();
				@object.RowIndex = rowIndex;
				if (description != null) {
					description.ParentRowIndex = rowIndex;
				}
				description = new ObjectDescription {
					Reference = new WeakReference<IProfileableObject>(@object),
					ParentRowIndex = RowIndex.Invalid,
					Type = @object.GetType(),
					Name = @object.Name,
					IsPartOfScene = @object.IsPartOfScene
				};
				rows[rowIndex.Value] = new TableRow {
					ObjectDescription = description,
					FrameIndex = -1
				};
				@object = @object.Parent;
				++NewDescriptionsCount;
			}
			if (description != null && @object != null) {
				description.ParentRowIndex = @object.RowIndex;
			}
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
		/// Tells the table that the object description is still in use in this frame.
		/// </summary>
		/// <param name="profiledFramesCount">The number of frames for which profiling was performed.</param>
		/// <remarks>
		/// Also should be called immediately after <see cref="EnsureDescriptionFor"/>.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UpdateFrameIndexFor(RowIndex rowIndex, long profiledFramesCount) =>
			rows[rowIndex.Value].FrameIndex = profiledFramesCount;

		/// <summary>
		/// Releases obsolete objects.
		/// </summary>
		public void CollectGarbage(long minFrameIndexToStayAlive)
		{
			// Passing indices from branches to the root.
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
			// Removing obsolete descriptions.
			for (uint rowIndex = 0; rowIndex < rows.Length; rowIndex++) {
				ref var row = ref rows[rowIndex];
				if (!row.IsEmpty && row.FrameIndex < minFrameIndexToStayAlive) {
					row.ObjectDescription = null;
					freeRows.Enqueue((RowIndex)rowIndex);
				}
			}
			NewDescriptionsCount = 0;
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
			/// True if at least one owner belongs to the scene.
			/// </summary>
			bool IsPartOfScene { get; }

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

			public bool IsPartOfScene { get; set; }

			public WeakReference<IProfileableObject> Reference { get; set; }
		}

		public struct RowIndex
		{
			/// <remarks>~0x_8000_0000 is hack for compatibility with <see cref="Owners"/> struct.</remarks>
			private const uint InvalidValue = uint.MaxValue & ~0x_8000_0000;

			public static RowIndex Invalid => new RowIndex { Value = InvalidValue };

			public uint Value;

			public bool IsValid => Value != InvalidValue;

			public static explicit operator uint(RowIndex rowIndex) => rowIndex.Value;
			public static explicit operator RowIndex(uint value) => new RowIndex { Value = value };
		}
	}
}

#endif // PROFILER
