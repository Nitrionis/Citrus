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

	/// <summary>
	/// Gets the total CPU and GPU time spent by filtered objects for each frame in history.
	/// </summary>
	internal class ObjectsSummaryRequest : IDataSelectionRequest
	{
		private const string EmptyOwnersText = "Empty_Owners";
		private const string ObjectNoNameText = "No_Object_Name";
		private const string EmptyOwnersListText = "Empty_Owners_List";

		/// <inheritdoc/>
		public bool IsRunning { get; set; }

		/// <inheritdoc/>
		public IAsyncResponseProcessor AsyncResponseProcessor { get; }

		[YuzuMember]
		private string regexp;
		[YuzuMember]
		private SearchMode mode;

		public ObjectsSummaryRequest(string regexp, IAsyncResponseProcessor asyncResponseProcessor)
		{
			this.regexp = regexp;
			mode = GetMode(regexp);
			AsyncResponseProcessor = asyncResponseProcessor;
		}

		public void FetchData(IProfilerDatabase database, BinaryWriter writer) =>
			TypeIdentifiersCache.SafeAccess(types => Execute(database, writer, types));

		private void Execute(IProfilerDatabase database, BinaryWriter writer, ITypeIdentifiersCache types)
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
				serializer.ToWriter(new ObjectsSummaryResponse { IsSucceed = false }, writer);
				return;
			}
			var response = new ObjectsSummaryResponse { IsSucceed = true };
			response.UpdateTimeForEachFrame = new float[database.FrameLifespan];
			response.RenderTimeForEachFrame = new float[database.FrameLifespan];
			response.DrawTimeForEachFrame = new float[database.FrameLifespan];
			long start = database.LastAvailableFrame - database.FrameLifespan + 1;
			long end = database.LastAvailableFrame;
			response.FirstFrameIdentifier = start;
			response.LastFrameIdentifier = end;
			for (long identifier = start; identifier <= end; identifier++) {
				if (database.CanAccessFrame(identifier)) {
					var frame = database.GetFrame(identifier);
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
					bool CheckType(TypeIdentifier id) => regexp.IsMatch(types.GetTypeName(id, database));
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
					response.UpdateTimeForEachFrame[identifier - start] = elapsed / (Stopwatch.Frequency / 1000f);
					ResetCounter();
					foreach (var usage in database.RenderCpuUsagesPool.Reversed(frame.RenderCpuUsagesList)) {
						ProcessCpuUsage(usage.StartTime, usage.FinishTime, IsCpuUsageFiltersPassed(usage, renderOwnersPool));
					}
					response.RenderTimeForEachFrame[identifier - start] = elapsed / (Stopwatch.Frequency / 1000f);
					ResetCounter();
					foreach (var usage in database.GpuUsagesPool.Reversed(frame.DrawingGpuUsagesList)) {
						ProcessGpuUsage(usage.StartTime, usage.FinishTime, IsGpuUsageFiltersPassed(usage, renderOwnersPool));
					}
					response.DrawTimeForEachFrame[identifier - start] = elapsed / 1000f;
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

		public enum SearchMode
		{
			ObjectName,
			TypeName,
			ReasonName,
		}

		private static class ReasonsNames
		{
			private static readonly string[] names;

			static ReasonsNames()
			{
				names = new string[CpuUsage.ReasonsBitMask + 1];
				foreach (var v in Enum.GetValues(typeof(CpuUsage.Reasons))) {
					uint value = (uint)v & CpuUsage.ReasonsBitMask;
					if (value != 0) {
						names[value] = ((CpuUsage.Reasons)value).ToString();
					}
				}
			}

			public static string TryGetName(CpuUsage.Reasons reasons)
			{
				uint value = (uint)reasons & CpuUsage.ReasonsBitMask;
				return value == 0 ? null : names[value];
			}
		}
	}

	/// <summary>
	/// Represents the total time spent by filtered objects for each frame in history.
	/// </summary>
	public class ObjectsSummaryResponse : IDataSelectionResponseBuilder, IDataSelectionResponse
	{
		[YuzuMember]
		public bool IsSucceed { get; set; }
		[YuzuMember]
		public long FirstFrameIdentifier { get; set; }
		[YuzuMember]
		public long LastFrameIdentifier { get; set; }
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
