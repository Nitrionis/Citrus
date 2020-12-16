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
	}
}

#endif // PROFILER
