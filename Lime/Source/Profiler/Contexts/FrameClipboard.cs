#if PROFILER

using System.Collections.Generic;

namespace Lime.Profiler.Contexts
{
	/// <summary>
	/// Contains all profiling data for one frame.
	/// </summary>
	public class FrameClipboard
	{
		public Dictionary<int, string> TypesDictionary;

		public readonly ReferenceTable ReferenceTable;

		public readonly RingPool<ReferenceTable.RowIndex> UpdateOwnersPool;
		public readonly RingPool<ReferenceTable.RowIndex> RenderOwnersPool;

		public readonly List<CpuUsage> UpdateCpuUsages;
		public readonly List<CpuUsage> RenderCpuUsages;
		public readonly List<GpuUsage> GpuUsages;

		public FrameClipboard()
		{
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
