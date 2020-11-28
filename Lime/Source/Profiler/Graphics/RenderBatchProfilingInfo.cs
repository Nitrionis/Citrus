#if PROFILER

using System;

namespace Lime.Profiler.Graphics
{
	/// <summary>
	/// Used to accumulate profiling information from render calls to various objects.
	/// </summary>
	/// <remarks>
	/// Since batch render is deferred, it is necessary to save information from various
	/// areas (for example, OverdrawMaterialScope) in an instance of this class.
	/// </remarks>
	internal struct RenderBatchProfilingInfo
	{
		private static uint currentOwnersCount;
		private static readonly ReferenceTable.RowIndex[] ownersBuffer;

		static RenderBatchProfilingInfo()
		{
			const int MinVertexCountPerOwner = 3;
			int maxCapacity = Math.Max(
				RenderBatchLimits.MaxIndices / MinVertexCountPerOwner,
				RenderBatchLimits.MaxVertices / MinVertexCountPerOwner);
			ownersBuffer = new ReferenceTable.RowIndex[maxCapacity];
		}

		public bool IsPartOfScene { get; private set; }
		public bool IsInsideOverdrawMaterialScope { get; private set; }
		public CpuUsage.Reasons UsageReasons { get; set; }

		private ProfilerDatabase.CpuUsageStartInfo usageStartInfo;

		public void Initialize()
		{
			IsPartOfScene = false;
			IsInsideOverdrawMaterialScope = false;
			currentOwnersCount = 0;
		}

		public void ProcessNode(IProfileableObject node)
		{
			IsPartOfScene |= node == null || node.IsPartOfScene;
			IsInsideOverdrawMaterialScope |= OverdrawMaterialScope.IsInside;
			ownersBuffer[currentOwnersCount++] = node.RowIndex;
		}

		public void Rendering()
		{
			if (IsInsideOverdrawMaterialScope) {
				OverdrawMaterialScope.Enter();
			}
			//var list = ProfilerDatabase.OwnersPool.AcquireList();
			//ProfilerDatabase.OwnersPool.AddToNewestList(ownersBuffer, currentOwnersCount);
			//usageStartInfo = ProfilerDatabase.CpuUsageStarted(new Owners(list), UsageReasons);
		}

		public void Rendered(Guid renderBatchTypeGuid)
		{
			//ProfilerDatabase.CpuUsageFinished(usageStartInfo, renderBatchTypeGuid);
			if (IsInsideOverdrawMaterialScope) {
				OverdrawMaterialScope.Leave();
			}
		}
	}
}
#endif // PROFILER
