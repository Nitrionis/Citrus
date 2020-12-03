#if PROFILER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Yuzu;
using Yuzu.Binary;

namespace Lime.Profiler.Network
{
	/// <summary>
	/// Requests all data for the specified frame.
	/// </summary>
	internal class FrameDataQuery : IDataSelectionCommand
	{
		private static readonly object lockObject;
		private static readonly Regex ignoredAssemblies;
		private static readonly Dictionary<TypeIdentifier, Type> typesDictionary;
		private static bool typesReloadRequired;
		private static bool[] requiredDescriptions;

		static FrameDataQuery()
		{
			lockObject = new object();
			ignoredAssemblies =  = new Regex(
				"^(System.*|mscorlib.*|Microsoft.*)",
				RegexOptions.Compiled
			);
			typesDictionary = new Dictionary<TypeIdentifier, Type>();
			requiredDescriptions = new bool[100_000];
		}

		[YuzuMember]
		private long identifer;

		public FrameDataQuery(long identifer) => this.identifer = identifer;

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database, BinaryWriter writer)
		{
			var serializer = new BinarySerializer();
			bool canAccessFrame = database.CanAccessFrame(identifer);
			serializer.ToWriter(new FrameDataResponse(canAccessFrame), writer);
			if (canAccessFrame) {
				var frame = database.GetFrame(identifer);
				for (int i = 0; i < requiredDescriptions.Length; i++) {
					requiredDescriptions[i] = false;
				}
				writer.Write(database.UpdateCpuUsagesPool.GetLength(frame.UpdateCpuUsagesList));
				foreach (var usage in database.UpdateCpuUsagesPool.Enumerate(frame.UpdateCpuUsagesList)) {
					Serialize(usage, writer, database.UpdateOwnersPool);
				}
				writer.Write(database.RenderCpuUsagesPool.GetLength(frame.RenderCpuUsagesList));
				foreach (var usage in database.RenderCpuUsagesPool.Enumerate(frame.RenderCpuUsagesList)) {
					Serialize(usage, writer, database.RenderOwnersPool);
				}
				writer.Write(database.GpuUsagesPool.GetLength(frame.DrawingGpuUsagesList));
				foreach (var usage in database.GpuUsagesPool.Enumerate(frame.DrawingGpuUsagesList)) {
					Serialize(usage, writer, database.RenderOwnersPool);
				}
				
				// send owners

				if (typesReloadRequired) {
					ReloadTypes(database);
				}
				serializer.ToWriter(typesDictionary, writer);
				
				// send ref table
				for (int i = 0; i < requiredDescriptions.Length; i++) {
					// Send descriptions 
					writer.Write(i);

				}
			}
		}

		private static void Serialize(CpuUsage usage, BinaryWriter writer, RingPool<ReferenceTable.RowIndex> pool)
		{
			EnsureKeyValuePairFor(usage.TypeIdentifier);
			writer.Write((uint)usage.Reason);
			writer.Write(usage.TypeIdentifier.Value);
			writer.Write(usage.Owners.PackedData);
			writer.Write(usage.StartTime);
			writer.Write(usage.FinishTime);
			Serialize(usage.Owners, pool, writer);
		}

		private static void Serialize(GpuUsage usage, BinaryWriter writer, RingPool<ReferenceTable.RowIndex> pool)
		{
			EnsureKeyValuePairFor(usage.MaterialTypeIdentifier);
			writer.Write(usage.MaterialTypeIdentifier.Value);
			writer.Write(usage.RenderPassIndex);
			writer.Write(usage.Owners.PackedData);
			writer.Write(usage.StartTime);
			writer.Write(usage.AllPreviousFinishTime);
			writer.Write(usage.FinishTime);
			writer.Write(usage.TrianglesCount);
			writer.Write(usage.VerticesCount);
			Serialize(usage.Owners, pool, writer);
		}

		private static void Serialize(Owners owners, RingPool<ReferenceTable.RowIndex> pool, BinaryWriter writer)
		{
			void RequestDescription(ReferenceTable.RowIndex rowIndex) {
				if (rowIndex.IsValid) {
					if (rowIndex.Value >= requiredDescriptions.Length) {
						int newLength = Math.Max((int)rowIndex.Value, 2 * requiredDescriptions.Length);
						Array.Resize(ref requiredDescriptions, newLength);
					}
					requiredDescriptions[rowIndex.Value] = true;
				}
			}
			if (!owners.IsEmpty) {
				if (owners.IsListDescriptor) {
					foreach (var owner in pool.Enumerate(owners.AsListDescriptor)) {
						RequestDescription(owner);
						writer.Write(owner.Value);
					}
				} else {
					RequestDescription(owners.AsIndex);
				}
			}
		}

		private static void EnsureKeyValuePairFor(TypeIdentifier identifier) =>
			typesReloadRequired |= !typesDictionary.ContainsKey(identifier);

		private static IEnumerable<Type> GetTypes() =>
			AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !ignoredAssemblies.IsMatch(a.FullName))
			.SelectMany(s => s.GetTypes());

		private static void ReloadTypes(IProfilerDatabase database)
		{
			foreach (var t in GetTypes()) {
				if (database.NativeTypesTable.TryGetValue(t, out var typeId)) {
					if (!typesDictionary.ContainsKey(new TypeIdentifier(typeId.Value))) {
						typesDictionary.Add(new TypeIdentifier(typeId.Value), t);
					}
				}
			}
		}
	}

	public class FrameDataResponse : IDataSelectionResponse
	{
		[YuzuMember]
		public bool IsSuccessed { get; private set; }

		public CpuUsage[] UpdateCpuUsages;
		public CpuUsage[] RenderCpuUsages;
		public CpuUsage[] GpuUsages;

		public FrameDataResponse(bool isSuccessed)
		{
			IsSuccessed = isSuccessed;
		}

		/// <inheritdoc/>
		public void DeserializeTail(BinaryReader reader)
		{
			UpdateCpuUsages = new CpuUsage[reader.ReadUInt32()];
			for (int i = 0; i < UpdateCpuUsages.Length; i++) {
				
			}
			RenderCpuUsages = new CpuUsage[reader.ReadUInt32()];
			for (int i = 0; i < RenderCpuUsages.Length; i++) {
				
			}
			GpuUsages = new CpuUsage[reader.ReadUInt32()];
			for (int i = 0; i < GpuUsages.Length; i++) {
				
			}
			throw new NotImplementedException();
		}
	}
}

#endif // PROFILER
