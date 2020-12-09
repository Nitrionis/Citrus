#if PROFILER

using System;
using System.Collections.Generic;
using System.IO;
using Yuzu;
using Yuzu.Binary;

namespace Lime.Profiler.Contexts
{
	using TypesDictionary = Dictionary<int, string>;

	internal class FrameDataRequest : IDataSelectionRequest
	{
		private static bool[] requiredDescriptions;

		static FrameDataRequest()
		{
			// 100.000 is just start capacity
			requiredDescriptions = new bool[100_000];
		}

		[YuzuMember]
		public long FrameIdentifier { get; }

		public FrameDataRequest(long frameIdentifer) => FrameIdentifier = frameIdentifer;

		/// <inheritdoc/>
		/// <remarks> Will be called asynchronously.</remarks>
		public void Execute(IProfilerDatabase database, BinaryWriter writer)
		{
			void SerializeOwners(Owners owners, RingPool<ReferenceTable.RowIndex> pool) {
				void RequestDescription(ReferenceTable.RowIndex rowIndex)
				{
					while (rowIndex.IsValid) {
						if (rowIndex.Value >= requiredDescriptions.Length) {
							int newLength = Math.Max(
								1 + (int)rowIndex.Value,
								2 * requiredDescriptions.Length);
							Array.Resize(ref requiredDescriptions, newLength);
						}
						requiredDescriptions[rowIndex.Value] = true;
						var parentRowIndex = database.NativeReferenceTable[rowIndex.Value].ParentRowIndex;
						rowIndex = requiredDescriptions[parentRowIndex.Value] ?
							ReferenceTable.RowIndex.Invalid : parentRowIndex;
					}
				}
				if (!owners.IsEmpty) {
					if (owners.IsListDescriptor && !owners.AsListDescriptor.IsNull) {
						writer.Write(pool.GetLength(owners.AsListDescriptor));
						foreach (var owner in pool.Enumerate(owners.AsListDescriptor)) {
							RequestDescription(owner);
							writer.Write(owner.Value);
						}
					} else {
						RequestDescription(owners.AsIndex);
					}
				}
			}
			var serializer = new BinarySerializer();
			bool canAccessFrame = database.CanAccessFrame(FrameIdentifier);
			serializer.ToWriter(new FrameDataResponse(canAccessFrame, FrameIdentifier), writer);
			if (canAccessFrame) {
				NumberedTypesDictionary.SafeExecute((dictionary) => {
					void SerializeCpuUsage(CpuUsage usage, RingPool<ReferenceTable.RowIndex> pool) {
						dictionary.EnsureKeyValuePairFor(usage.TypeIdentifier);
						writer.Write((uint)usage.Reason);
						writer.Write(usage.TypeIdentifier.Value);
						writer.Write(usage.Owners.PackedData);
						writer.Write(usage.StartTime);
						writer.Write(usage.FinishTime);
						SerializeOwners(usage.Owners, pool);
					}
					void SerializeGpuUsage(GpuUsage usage, RingPool<ReferenceTable.RowIndex> pool) {
						dictionary.EnsureKeyValuePairFor(usage.MaterialTypeIdentifier);
						writer.Write(usage.MaterialTypeIdentifier.Value);
						writer.Write(usage.RenderPassIndex);
						writer.Write(usage.Owners.PackedData);
						writer.Write(usage.StartTime);
						writer.Write(usage.AllPreviousFinishTime);
						writer.Write(usage.FinishTime);
						writer.Write(usage.TrianglesCount);
						writer.Write(usage.VerticesCount);
						SerializeOwners(usage.Owners, pool);
					}
					var frame = database.GetFrame(FrameIdentifier);
					for (int i = 0; i < requiredDescriptions.Length; i++) {
						requiredDescriptions[i] = false;
					}
					writer.Write(database.UpdateCpuUsagesPool.GetLength(frame.UpdateCpuUsagesList));
					foreach (var usage in database.UpdateCpuUsagesPool.Enumerate(frame.UpdateCpuUsagesList)) {
						SerializeCpuUsage(usage, database.UpdateOwnersPool);
					}
					writer.Write(database.RenderCpuUsagesPool.GetLength(frame.RenderCpuUsagesList));
					foreach (var usage in database.RenderCpuUsagesPool.Enumerate(frame.RenderCpuUsagesList)) {
						SerializeCpuUsage(usage, database.RenderOwnersPool);
					}
					writer.Write(database.GpuUsagesPool.GetLength(frame.DrawingGpuUsagesList));
					foreach (var usage in database.GpuUsagesPool.Enumerate(frame.DrawingGpuUsagesList)) {
						SerializeGpuUsage(usage, database.RenderOwnersPool);
					}
					serializer.ToWriter(dictionary.FindAndGetTypeNames(database), writer);
				});
				for (uint i = 0; i < requiredDescriptions.Length; i++) {
					if (requiredDescriptions[i]) {
						writer.Write(i);
						var description = database.NativeReferenceTable[i];
						serializer.ToWriter(description.IsPartOfScene, writer);
						serializer.ToWriter(description.ObjectName, writer);
						serializer.ToWriter(description.TypeName, writer);
						serializer.ToWriter(description.ParentRowIndex.Value, writer);
					}
				}
				// End of descriptions.
				writer.Write(uint.MaxValue);
			}
		}
	}

	internal class FrameDataResponse : IDataSelectionResponse
	{
		private static uint nextRowIndex;
		private static uint[] ownersRedirection;

		[YuzuMember]
		public bool IsSuccessed { get; private set; }

		[YuzuMember]
		public long FrameIdentifier { get; }

		public FrameDataResponse(bool isSuccessed, long frameIdentifier)
		{
			IsSuccessed = isSuccessed;
			FrameIdentifier = frameIdentifier;
		}

		/// <inheritdoc/>
		public void DeserializeTail(FrameClipboard clipboard, BinaryReader reader)
		{
			CpuUsage DeserializeCpuUsage(RingPool<ReferenceTable.RowIndex> ownersPool) {
				var usage = new CpuUsage {
					Reason = (CpuUsage.Reasons)reader.ReadUInt32(),
					TypeIdentifier = new TypeIdentifier(reader.ReadInt32()),
					StartTime = reader.ReadInt64(),
					FinishTime = reader.ReadInt64(),
					Owners = DeserializeOwners(ownersPool)
				};
				return usage;
			}
			GpuUsage DeserializeGpuUsage(RingPool<ReferenceTable.RowIndex> ownersPool) {
				var usage = new GpuUsage {
					MaterialTypeIdentifier = new TypeIdentifier(reader.ReadInt32()),
					RenderPassIndex = reader.ReadInt32(),
					StartTime = reader.ReadUInt32(),
					AllPreviousFinishTime = reader.ReadUInt32(),
					FinishTime = reader.ReadUInt32(),
					TrianglesCount = reader.ReadInt32(),
					VerticesCount = reader.ReadInt32(),
					Owners = DeserializeOwners(ownersPool)
				};
				return usage;
			}
			Owners DeserializeOwners(RingPool<ReferenceTable.RowIndex> ownersPool) {
				ReferenceTable.RowIndex ReflectRowIndex() {
					uint rowIndex = reader.ReadUInt32();
					if (rowIndex >= ownersRedirection.Length) {
						int oldLength = ownersRedirection.Length;
						int newLength = Math.Max(
							1 + (int)rowIndex,
							2 * ownersRedirection.Length);
						Array.Resize(ref ownersRedirection, newLength);
						for (int i = oldLength; i < newLength; i++) {
							ownersRedirection[i] = Owners.InvalidData;
						}
					}
					return (ReferenceTable.RowIndex)(ownersRedirection[rowIndex] == Owners.InvalidData ?
						(ownersRedirection[rowIndex] = nextRowIndex++) : ownersRedirection[rowIndex]);
				}
				var owners = new Owners(reader.ReadUInt32());
				if (!owners.IsEmpty) {
					if (owners.IsListDescriptor && !owners.AsListDescriptor.IsNull) {
						uint length = reader.ReadUInt32();
						owners.AsListDescriptor = ownersPool.AcquireList();
						for (int i = 0; i < length; i++) {
							ownersPool.AddToNewestList(ReflectRowIndex());
						}
					} else {
						owners.AsIndex = ReflectRowIndex();
					}
				}
				return owners;
			}
			var updateOwnersPool = clipboard.UpdateOwnersPool;
			var renderOwnersPool = clipboard.RenderOwnersPool;
			updateOwnersPool.Clear();
			renderOwnersPool.Clear();
			nextRowIndex = 0;
			for (int i = 0; i < ownersRedirection.Length; i++) {
				ownersRedirection[i] = Owners.InvalidData;
			}
			var deserializer = new BinaryDeserializer();
			var updateCpuUsagesCount = reader.ReadUInt32();
			var updateCpuUsages = clipboard.UpdateCpuUsages;
			for (int i = 0; i < updateCpuUsagesCount; i++) {
				updateCpuUsages[i] = DeserializeCpuUsage(updateOwnersPool);
			}
			var renderCpuUsagesCount = reader.ReadUInt32();
			var renderCpuUsages = clipboard.RenderCpuUsages;
			for (int i = 0; i < renderCpuUsagesCount; i++) {
				renderCpuUsages[i] = DeserializeCpuUsage(renderOwnersPool);
			}
			var gpuUsagesCount = reader.ReadUInt32();
			var gpuUsages = clipboard.GpuUsages;
			for (int i = 0; i < gpuUsagesCount; i++) {
				gpuUsages[i] = DeserializeGpuUsage(renderOwnersPool);
			}
			clipboard.TypesDictionary = deserializer.FromReader<TypesDictionary>(reader);
			((DeserializableReferenceTable)clipboard.ReferenceTable).ReloadFromReader(reader);
		}

		private class DeserializableReferenceTable : ReferenceTable
		{
			public void ReloadFromReader(BinaryReader reader)
			{
				const long DefaulFrameIndex = 0;
				for (int i = 0; i < rows.Length; i++) {
					rows[i].FrameIndex = DefaulFrameIndex;
				}
				if (nextRowIndex >= rows.Length) {
					Array.Resize(ref rows, (int)nextRowIndex);
				}
				uint nativeIndex;
				while ((nativeIndex = reader.ReadUInt32()) != uint.MaxValue) {
					uint index = ownersRedirection[nativeIndex];
					var description = (rows[index].ObjectDescription as ObjectDescription) ?? new ObjectDescription();
					description.IsPartOfScene = reader.ReadBoolean();
					description.ObjectName = reader.ReadString();
					description.TypeName = reader.ReadString();
					description.ParentRowIndex = (RowIndex)reader.ReadUInt32();
					rows[index] = new TableRow {
						ObjectDescription = description,
						FrameIndex = -1
					};
				}
				freeRows.Clear();
				for (uint i = 0; i < rows.Length; i++) {
					if (rows[i].FrameIndex == DefaulFrameIndex) {
						rows[i].ObjectDescription = null;
						freeRows.Enqueue(new RowIndex { Value = i });
					}
				}
			}

			private class ObjectDescription : IObjectDescription
			{
				public RowIndex ParentRowIndex { get; set; }
				public string ObjectName { get; set; }
				public string TypeName { get; set; }
				public bool IsPartOfScene { get; set; }
				public WeakReference<IProfileableObject> Reference => null;
			}
		}
	}
}

#endif // PROFILER
