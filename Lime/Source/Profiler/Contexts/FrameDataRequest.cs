#if PROFILER

using System;
using System.Collections.Generic;
using System.IO;
using Yuzu;
using Yuzu.Binary;

namespace Lime.Profiler.Contexts
{
	using TypesDictionary = Dictionary<int, string>;
	using OwnersPool = RingPool<ReferenceTable.RowIndex>;
	
	internal class FrameDataRequest : IDataSelectionRequest
	{
		private static bool[] requiredDescriptions;

		static FrameDataRequest()
		{
			// 100.000 is just start capacity
			requiredDescriptions = new bool[100_000];
		}

		/// <inheritdoc/>
		public bool IsRunning { get; set; }

		[YuzuMember]
		public long FrameIdentifier { get; }

		/// <inheritdoc/>
		public IAsyncResponseProcessor AsyncResponseProcessor { get; }

		public FrameDataRequest(long frameIdentifier, IAsyncResponseProcessor asyncResponseProcessor)
		{
			FrameIdentifier = frameIdentifier;
			AsyncResponseProcessor = asyncResponseProcessor;
		}

		/// <inheritdoc/>
		public void FetchData(IProfilerDatabase database, BinaryWriter writer, BinarySerializer serializer)
		{
			var frame = database.GetFrame(FrameIdentifier);
			bool canAccessFrame = frame != null;
			var profiledFrame = canAccessFrame ? frame.CommonData : new ProfiledFrame { Identifier = FrameIdentifier };
			serializer.ToWriter(new FrameDataResponseBuilder(canAccessFrame, profiledFrame), writer);
			if (canAccessFrame) {
				TypeIdentifiersCache.SafeAccess(dictionary => {
					void SerializeCpuUsage(CpuUsage usage, RingPool<ReferenceTable.RowIndex> pool) {
						dictionary.EnsureKeyValuePairFor(usage.TypeIdentifier);
						writer.Write((uint)usage.Reason);
						writer.Write(usage.TypeIdentifier.Value);
						writer.Write(usage.StartTime);
						writer.Write(usage.FinishTime);
						writer.Write(usage.Owners.PackedData);
						SerializeOwners(usage.Owners, pool, database.NativeReferenceTable, writer);
					}
					void SerializeGpuUsage(GpuUsage usage, RingPool<ReferenceTable.RowIndex> pool) {
						dictionary.EnsureKeyValuePairFor(usage.MaterialTypeIdentifier);
						writer.Write(usage.MaterialTypeIdentifier.Value);
						writer.Write(usage.RenderPassIndex);
						writer.Write(usage.StartTime);
						writer.Write(usage.AllPreviousFinishTime);
						writer.Write(usage.FinishTime);
						writer.Write(usage.TrianglesCount);
						writer.Write(usage.VerticesCount);
						writer.Write(usage.Owners.PackedData);
						SerializeOwners(usage.Owners, pool, database.NativeReferenceTable, writer);
					}
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
						writer.Write(description.IsPartOfScene);
						writer.Write(description.ObjectName ?? "");
						writer.Write(description.TypeName);
						writer.Write(description.ParentRowIndex.Value);
					}
				}
				// End of descriptions.
				writer.Write(uint.MaxValue);
			}
		}

		private void SerializeOwners(Owners owners, OwnersPool pool, ReferenceTable table, BinaryWriter writer)
		{
			void RequestDescription(ReferenceTable.RowIndex rowIndex) {
				while (rowIndex.IsValid) {
					if (rowIndex.Value >= requiredDescriptions.Length) {
						int newLength = Math.Max(1 + (int)rowIndex.Value, 2 * requiredDescriptions.Length);
						Array.Resize(ref requiredDescriptions, newLength);
					}
					if (requiredDescriptions[rowIndex.Value]) {
						break;
					}
					requiredDescriptions[rowIndex.Value] = true;
					rowIndex = table[rowIndex.Value].ParentRowIndex;
				}
			}
			if (!owners.IsEmpty) {
				if (owners.IsListDescriptor) {
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
	}

	public class FrameDataResponse : IDataSelectionResponse
	{
		public bool IsSucceed { get; set; }
		public ProfiledFrame ProfiledFrame { get; set; }
		public FrameClipboard Clipboard { get; set; }
	}
	
	internal class FrameDataResponseBuilder : IDataSelectionResponseBuilder
	{
		private static uint nextRowIndex;
		private static uint[] ownersRedirection = new uint[16];

		[YuzuMember]
		public bool IsSucceed { get; set; }

		[YuzuMember]
		public ProfiledFrame ProfiledFrame { get; set; }

		public FrameDataResponseBuilder() {}

		public FrameDataResponseBuilder(bool isSucceed, ProfiledFrame profiledFrame)
		{
			IsSucceed = isSucceed;
			ProfiledFrame = profiledFrame;
		}

		/// <inheritdoc/>
		public IDataSelectionResponse Build(FrameClipboard clipboard, BinaryReader reader)
		{
			CpuUsage DeserializeCpuUsage(OwnersPool ownersPool) {
				var usage = new CpuUsage {
					Reason = (CpuUsage.Reasons)reader.ReadUInt32(),
					TypeIdentifier = new TypeIdentifier(reader.ReadInt32()),
					StartTime = reader.ReadInt64(),
					FinishTime = reader.ReadInt64(),
					Owners = DeserializeOwners(ownersPool)
				};
				return usage;
			}
			GpuUsage DeserializeGpuUsage(OwnersPool ownersPool) {
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
			Owners DeserializeOwners(OwnersPool ownersPool) {
				var owners = new Owners(reader.ReadUInt32());
				if (!owners.IsEmpty) {
					if (owners.IsListDescriptor) {
						uint length = reader.ReadUInt32();
						owners.AsListDescriptor = ownersPool.AcquireList();
						for (int i = 0; i < length; i++) {
							ownersPool.AddToNewestList(ReflectRowIndex(reader.ReadUInt32()));
						}
					} else {
						owners.AsIndex = ReflectRowIndex(owners.AsIndex.Value);
					}
				}
				return owners;
			}
			if (!IsSucceed) {
				return new FrameDataResponse {
					IsSucceed = IsSucceed,
					ProfiledFrame = ProfiledFrame,
					Clipboard = null
				};
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
			updateCpuUsages.Clear();
			for (int i = 0; i < updateCpuUsagesCount; i++) {
				updateCpuUsages.Add(DeserializeCpuUsage(updateOwnersPool));
			}
			var renderCpuUsagesCount = reader.ReadUInt32();
			var renderCpuUsages = clipboard.RenderCpuUsages;
			renderCpuUsages.Clear();
			for (int i = 0; i < renderCpuUsagesCount; i++) {
				renderCpuUsages.Add(DeserializeCpuUsage(renderOwnersPool));
			}
			var gpuUsagesCount = reader.ReadUInt32();
			var gpuUsages = clipboard.GpuUsages;
			gpuUsages.Clear();
			for (int i = 0; i < gpuUsagesCount; i++) {
				gpuUsages.Add(DeserializeGpuUsage(renderOwnersPool));
			}
			clipboard.TypesDictionary = deserializer.FromReader<TypesDictionary>(reader);
			if (!(clipboard.ReferenceTable is DeserializableReferenceTable)) {
				clipboard.ReferenceTable = new DeserializableReferenceTable();
			}
			((DeserializableReferenceTable)clipboard.ReferenceTable).ReloadFromReader(reader);
			return new FrameDataResponse {
				IsSucceed = IsSucceed,
				ProfiledFrame = ProfiledFrame,
				Clipboard = clipboard
			};
		}

		private static ReferenceTable.RowIndex ReflectRowIndex(uint rowIndex) {
			if (rowIndex == ReferenceTable.RowIndex.Invalid.Value) {
				return ReferenceTable.RowIndex.Invalid;
			}
			if (rowIndex >= ownersRedirection.Length) {
				int oldLength = ownersRedirection.Length;
				int newLength = Math.Max(
					1 + (int)rowIndex,
					2 * ownersRedirection.Length);
				Array.Resize(ref ownersRedirection, newLength);
				for (int i = oldLength; i < newLength; i++) {
					ownersRedirection[i] = ReferenceTable.RowIndex.Invalid.Value;
				}
			}
			return (ReferenceTable.RowIndex)(ownersRedirection[rowIndex] == ReferenceTable.RowIndex.Invalid.Value ?
				(ownersRedirection[rowIndex] = nextRowIndex++) : ownersRedirection[rowIndex]);
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
					uint index = ReflectRowIndex(nativeIndex).Value;
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
