#if PROFILER

using System.Collections.Generic;

namespace Lime.Profiler.Contexts
{
	/// <summary>
	/// Contains all profiling data for one frame.
	/// </summary>
	public class FrameClipboard
	{
		public long FrameIdentifier;

		private long sortedFrameIdentifier;

		/// <summary>
		/// Indicates whether the data is sorted by
		/// <see cref="CpuUsage.StartTime"/> or <see cref="GpuUsage.StartTime"/>.
		/// </summary>
		public bool IsSortedByStartTime => sortedFrameIdentifier == FrameIdentifier;

		public Dictionary<int, string> TypesDictionary;

		public ReferenceTable ReferenceTable;

		public RingPool<ReferenceTable.RowIndex> UpdateOwnersPool;
		public RingPool<ReferenceTable.RowIndex> RenderOwnersPool;

		public List<CpuUsage> UpdateCpuUsages;
		public List<CpuUsage> RenderCpuUsages;
		public List<GpuUsage> GpuUsages;

		public FrameClipboard()
		{
			FrameIdentifier = -1;
			TypesDictionary = new Dictionary<int, string>();
			ReferenceTable = new ReferenceTable();
			UpdateOwnersPool = new RingPool<ReferenceTable.RowIndex>();
			RenderOwnersPool = new RingPool<ReferenceTable.RowIndex>();
			UpdateCpuUsages = new List<CpuUsage>();
			RenderCpuUsages = new List<CpuUsage>();
			GpuUsages = new List<GpuUsage>();
		}

		public void SortByStartTime()
		{
			sortedFrameIdentifier = FrameIdentifier;
			// We assume that the data is like this:
			// usages[i].FinishTime <= usages[i + 1].FinishTime
			UpdateCpuUsages.Reverse();
			UpdateCpuUsages.Sort((a, b) => {
				int value = a.StartTime.CompareTo(b.StartTime);
				return value != 0 ? value : (b.FinishTime - b.StartTime).CompareTo(a.FinishTime - a.StartTime);
			});
			RenderCpuUsages.Reverse();
			RenderCpuUsages.Sort((a, b) => {
				int value = a.StartTime.CompareTo(b.StartTime);
				return value != 0 ? value : (b.FinishTime - b.StartTime).CompareTo(a.FinishTime - a.StartTime);
			});
			GpuUsages.Reverse();
			GpuUsages.Sort((a, b) => {
				int value = a.StartTime.CompareTo(b.StartTime);
				return value != 0 ? value : (b.FinishTime - b.StartTime).CompareTo(a.FinishTime - a.StartTime);
			});
		}
	}
}

#endif // PROFILER
