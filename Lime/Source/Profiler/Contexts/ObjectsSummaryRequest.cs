#if PROFILER

using System;
using System.IO;
using System.Text.RegularExpressions;
using Yuzu;
using Yuzu.Binary;

namespace Lime.Profiler.Contexts
{
	using OwnersPool = RingPool<ReferenceTable.RowIndex>;

	internal class ObjectsSummaryRequest : IDataSelectionRequest
	{
		[YuzuMember]
		private string regexp;
		[YuzuMember]
		private SearchMode mode;

		public ObjectsSummaryRequest(string regexp)
		{
			this.regexp = regexp;
			mode = GetMode(regexp);
		}

		public void Execute(IProfilerDatabase database, BinaryWriter writer) =>
			NumberedTypesDictionary.SafeExecute(types => Execute(database, writer, types));

		private void Execute(IProfilerDatabase database, BinaryWriter writer, INumberedTypesDictionary types)
		{
			var serializer = new BinarySerializer();
			Regex regexp = null;
			try {
				regexp = new Regex(this.regexp);
			} catch (ArgumentException) {
				serializer.ToWriter(new ObjectsSummaryResponse() { IsSuccessed = false }, writer);
				return;
			}
			var response = new ObjectsSummaryResponse() { IsSuccessed = true };
			response.UpdateTimeForEachFrame = new float[database.FrameLifespan];
			response.RenderTimeForEachFrame = new float[database.FrameLifespan];
			response.DrawTimeForEachFrame = new float[database.FrameLifespan];
			long start = database.LastAvailableFrame - database.FrameLifespan + 1;
			long end = database.LastAvailableFrame;
			response.FirstFrameIdentifer = start;
			response.LastFrameIdentifer = end;
			for (long identifer = start; identifer <= end; identifer++) {
				if (database.CanAccessFrame(identifer)) {
					var frame = database.GetFrame(identifer);
					long elapsed;
					long lastStartTimestamp;
					void ResetCounter() {
						elapsed = 0;
						lastStartTimestamp = long.MaxValue;
					}
					bool IsCpuUsageFiltersPassed(CpuUsage usage, OwnersPool ownersPool) {
						switch (mode) {
							case SearchMode.ObjectName:
								return CheckOwner(usage.Owners, ownersPool);
							case SearchMode.ReasonName:
								var name = ReasonsNames.TryGetName(usage.Reason);
								return name == null ? false : regexp.IsMatch(name);
							case SearchMode.TypeName:
								return CheckType(usage.TypeIdentifier);
						}
						throw new NotImplementedException();
					}
					bool IsGpuUsageFiltersPassed(GpuUsage usage, OwnersPool ownersPool) {
						switch (mode) {
							case SearchMode.ObjectName:
								return CheckOwner(usage.Owners, ownersPool);
							case SearchMode.ReasonName:
								return false;
							case SearchMode.TypeName:
								return CheckType(usage.MaterialTypeIdentifier);
							default: throw new InvalidOperationException();
						}
					}
					bool CheckOwner(Owners owners, OwnersPool ownersPool) {
						bool CheckObject(ReferenceTable.RowIndex rowIndex) {
							if (rowIndex.IsValid) {
								var description = database.NativeReferenceTable[rowIndex.Value];
								return string.IsNullOrEmpty(description.ObjectName) ?
									regexp.IsMatch("Object_Name_Not_Set") :
									regexp.IsMatch(description.ObjectName);
							} else {
								return regexp.IsMatch("Empty_Owners");
							}
						}
						if (owners.IsEmpty) {
							return regexp.IsMatch("Empty_Owners");
						} else {
							if (owners.IsListDescriptor) {
								var list = owners.AsListDescriptor;
								if (list.IsNull) {
									return regexp.IsMatch("Empty_Owners_List");
								} else {
									bool hasMatch = false;
									foreach (var rowIndex in ownersPool.Enumerate(list)) {
										hasMatch |= CheckObject(rowIndex);
									}
									return hasMatch;
								}
							} else {
								return CheckObject(owners.AsIndex);
							}
						}
					}
					bool CheckType(TypeIdentifier identifier) =>
						regexp.IsMatch(types.GetTypeName(identifier, database));
					/// Usages example:           ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀   ▀▀▀▀▀▀▀
					///                ▀▀▀▀▀▀▀  ▀▀▀▀▀▀▀▀      ▀▀▀▀▀▀▀▀▀▀▀   ▀▀▀▀  ▀▀▀▀▀▀▀▀▀
					///                ▀▀▀  ▀▀▀▀▀▀▀▀      ▀▀▀▀▀▀▀▀▀▀▀   ▀▀▀▀  ▀▀▀▀▀▀▀▀▀      ▀▀▀▀▀▀▀▀▀
					/// Result sum:    ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀  ▀▀▀▀▀▀▀▀▀
					///                <--------------------------------------------------------------
					void ProcessCpuUsage(long startTimestamp, long endTimestamp, bool isFiltersPassed) {
						if (isFiltersPassed) {
							if (startTimestamp > endTimestamp) {
								throw new System.Exception("Profiler: wrong timestamp?");
							}

							lastStartTimestamp = Math.Min(lastStartTimestamp, startTimestamp);

						}
					}
					void ProcessGpuUsage(uint startTimestamp, uint endTimestamp, bool isFiltersPassed) {
						if (isFiltersPassed) {
							if (endTimestamp < lastStartTimestamp) {
								
							}
							lastStartTimestamp = Math.Min(lastStartTimestamp, startTimestamp);
						}
					}
					var updateOwnersPool = database.UpdateOwnersPool;
					var renderOwnersPool = database.RenderOwnersPool;
					ResetCounter();
					foreach (var usage in database.UpdateCpuUsagesPool.Reversed(frame.UpdateCpuUsagesList)) {
						ProcessCpuUsage(usage.StartTime, usage.FinishTime, IsCpuUsageFiltersPassed(usage, updateOwnersPool));
					}
					response.UpdateTimeForEachFrame[identifer] = elapsed;
					ResetCounter();
					foreach (var usage in database.RenderCpuUsagesPool.Reversed(frame.RenderCpuUsagesList)) {
						ProcessCpuUsage(usage.StartTime, usage.FinishTime, IsCpuUsageFiltersPassed(usage, renderOwnersPool));
					}
					response.RenderTimeForEachFrame[identifer] = elapsed;
					ResetCounter();
					foreach (var usage in database.GpuUsagesPool.Reversed(frame.DrawingGpuUsagesList)) {
						ProcessGpuUsage(usage.StartTime, usage.FinishTime, IsGpuUsageFiltersPassed(usage, renderOwnersPool));
					}
					response.DrawTimeForEachFrame[identifer] = elapsed;
				}
			}
			serializer.ToWriter(response, writer);
		}

		private SearchMode GetMode(string regex)
		{
			string prefix = "(?# ";
			if (regex.StartsWith(prefix)) {
				var s = regex.Substring(prefix.Length, regex.IndexOf(')') - prefix.Length);
				return s == "type" ? SearchMode.TypeName : s == "reason" ? SearchMode.ReasonName : SearchMode.ObjectName;
			}
			return SearchMode.ObjectName;
		}

		public enum SearchMode : int
		{
			ObjectName,
			TypeName,
			ReasonName,
		}

		private static class ReasonsNames
		{
			private const uint BitMask = 0x3F;

			private static readonly string[] names;

			static ReasonsNames()
			{
				names = new string[64];
				foreach (var v in Enum.GetValues(typeof(CpuUsage.Reasons))) {
					uint value = (uint)v & BitMask;
					if (value != 0) {
						names[value] = ((CpuUsage.Reasons)value).ToString();
					}
				}
			}

			public static string TryGetName(CpuUsage.Reasons reasons)
			{
				uint value = (uint)reasons & BitMask;
				return value == 0 ? null : names[value];
			}
		}
	}

	internal class ObjectsSummaryResponse : IDataSelectionResponse
	{
		[YuzuMember]
		public bool IsSuccessed;
		[YuzuMember]
		public long FirstFrameIdentifer;
		[YuzuMember]
		public long LastFrameIdentifer;
		[YuzuMember]
		public float[] RenderTimeForEachFrame;
		[YuzuMember]
		public float[] UpdateTimeForEachFrame;
		[YuzuMember]
		public float[] DrawTimeForEachFrame;

		/// <inheritdoc/>
		public void DeserializeTail(FrameClipboard clipboard, BinaryReader reader) { }
	}
}

#endif // PROFILER
