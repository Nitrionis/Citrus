#if PROFILER

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Yuzu;
using Yuzu.Binary;

namespace Lime.Profiler.Contexts
{
	using OwnersPool = RingPool<ReferenceTable.RowIndex>;

	internal class ObjectsSummaryRequest : IDataSelectionRequest
	{
		private const string EmptyOwnersText = "Empty_Owners";
		private const string ObjectNoNameText = "No_Object_Name";
		private const string EmptyOwnersListText = "Empty_Owners_List";

		/// <inheritdoc/>
		public bool IsRunning { get; set; }

		/// <inheritdoc/>
		public IResponseProcessor ResponseProcessor { get; set; }

		[YuzuMember]
		private string regexp;
		[YuzuMember]
		private SearchMode mode;

		public ObjectsSummaryRequest(string regexp)
		{
			this.regexp = regexp;
			mode = GetMode(regexp);
		}

		public void FetchData(IProfilerDatabase database, BinaryWriter writer) =>
			NumberedTypesDictionary.SafeAccess(types => Execute(database, writer, types));

		private void Execute(IProfilerDatabase database, BinaryWriter writer, INumberedTypesDictionary types)
		{
			if (IsRunning) {
				throw new InvalidOperationException("Profiler: The request execution has already started!");
			}
			IsRunning = true;
			var serializer = new BinarySerializer();
			Regex regexp = null;
			try {
				regexp = new Regex(this.regexp);
			} catch (ArgumentException) {
				serializer.ToWriter(new ObjectsSummaryResponse { IsSuccessed = false }, writer);
				return;
			}
			var response = new ObjectsSummaryResponse { IsSuccessed = true };
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
								return name != null && regexp.IsMatch(name);
							case SearchMode.TypeName:
								return CheckType(usage.TypeIdentifier);
						}
						throw new InvalidOperationException();
					}
					bool IsGpuUsageFiltersPassed(GpuUsage usage, OwnersPool ownersPool) {
						switch (mode) {
							case SearchMode.ObjectName:
								return CheckOwner(usage.Owners, ownersPool);
							case SearchMode.ReasonName:
								return false;
							case SearchMode.TypeName:
								return CheckType(usage.MaterialTypeIdentifier);
						}
						throw new InvalidOperationException();
					}
					bool CheckOwner(Owners owners, OwnersPool ownersPool) {
						bool CheckObject(ReferenceTable.RowIndex rowIndex) {
							if (rowIndex.IsValid) {
								var description = database.NativeReferenceTable[rowIndex.Value];
								return string.IsNullOrEmpty(description.ObjectName) ?
									regexp.IsMatch(ObjectNoNameText) :
									regexp.IsMatch(description.ObjectName);
							} else {
								return regexp.IsMatch(EmptyOwnersText);
							}
						}
						if (owners.IsEmpty) {
							return regexp.IsMatch(EmptyOwnersText);
						} else {
							if (owners.IsListDescriptor) {
								var list = owners.AsListDescriptor;
								if (list.IsNull) {
									return regexp.IsMatch(EmptyOwnersListText);
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
					/// Usages example:           ▀▀▀▀▀▀ ▀▀▀▀▀▀▀▀▀▀▀   ▀▀▀▀▀▀▀
					///                ▀▀▀▀▀▀▀  ▀▀▀▀▀▀▀▀      ▀▀▀▀▀▀▀▀▀▀▀   ▀▀▀▀  ▀▀▀▀▀▀▀▀▀
					///                ▀▀▀  ▀▀▀▀▀▀▀▀      ▀▀▀▀▀▀▀▀▀▀▀   ▀▀▀▀  ▀▀▀▀▀▀▀▀▀      ▀▀▀▀▀▀▀▀▀
					/// Result sum:    ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀ ▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀  ▀▀▀▀▀▀▀▀▀
					///                <--------------------------------------------------------------
					void ProcessCpuUsage(long startTimestamp, long endTimestamp, bool isFiltersPassed) {
						if (isFiltersPassed && lastStartTimestamp > startTimestamp) {
							if (startTimestamp > endTimestamp) {
								throw new System.Exception("Profiler: wrong timestamp?");
							}
							lastStartTimestamp = Math.Min(lastStartTimestamp, endTimestamp);
							elapsed += lastStartTimestamp - startTimestamp;
							lastStartTimestamp = startTimestamp;
						}
					}
					void ProcessGpuUsage(uint startTimestamp, uint endTimestamp, bool isFiltersPassed) {
						if (isFiltersPassed && lastStartTimestamp > startTimestamp) {
							lastStartTimestamp = Math.Min(lastStartTimestamp, endTimestamp);
							elapsed += lastStartTimestamp - startTimestamp;
							lastStartTimestamp = startTimestamp;
						}
					}
					var updateOwnersPool = database.UpdateOwnersPool;
					var renderOwnersPool = database.RenderOwnersPool;
					ResetCounter();
					foreach (var usage in database.UpdateCpuUsagesPool.Reversed(frame.UpdateCpuUsagesList)) {
						ProcessCpuUsage(usage.StartTime, usage.FinishTime, IsCpuUsageFiltersPassed(usage, updateOwnersPool));
					}
					response.UpdateTimeForEachFrame[identifer] = elapsed / (Stopwatch.Frequency / 1000);
					ResetCounter();
					foreach (var usage in database.RenderCpuUsagesPool.Reversed(frame.RenderCpuUsagesList)) {
						ProcessCpuUsage(usage.StartTime, usage.FinishTime, IsCpuUsageFiltersPassed(usage, renderOwnersPool));
					}
					response.RenderTimeForEachFrame[identifer] = elapsed / (Stopwatch.Frequency / 1000);
					ResetCounter();
					foreach (var usage in database.GpuUsagesPool.Reversed(frame.DrawingGpuUsagesList)) {
						ProcessGpuUsage(usage.StartTime, usage.FinishTime, IsGpuUsageFiltersPassed(usage, renderOwnersPool));
					}
					response.DrawTimeForEachFrame[identifer] = elapsed / 1000;
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

	public class ObjectsSummaryResponse : IDataSelectionResponseBuilder, IDataSelectionResponse
	{
		[YuzuMember]
		public bool IsSuccessed { get; set; }
		[YuzuMember]
		public long FirstFrameIdentifer { get; set; }
		[YuzuMember]
		public long LastFrameIdentifer { get; set; }
		[YuzuMember]
		public float[] RenderTimeForEachFrame { get; set; }
		[YuzuMember]
		public float[] UpdateTimeForEachFrame { get; set; }
		[YuzuMember]
		public float[] DrawTimeForEachFrame { get; set; }

		/// <inheritdoc/>
		public IDataSelectionResponse Build(FrameClipboard clipboard, BinaryReader reader) => this;
	}
}

#endif // PROFILER
