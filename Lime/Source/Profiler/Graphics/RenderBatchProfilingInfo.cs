#if PROFILER

using System;

namespace Lime.Profiler.Graphics
{
	internal class RenderBatchStatistics
	{
		public static int FullSavedByBatching;
		public static int SceneSavedByBatching;

		public static void Reset()
		{
			FullSavedByBatching = 0;
			SceneSavedByBatching = 0;
		}
	}
	
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
		private static ProfilerDatabase.CpuUsageStartInfo usageStartInfo;

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

		public void Rendering(int materialPassCount)
		{
			if (IsInsideOverdrawMaterialScope) {
				OverdrawMaterialScope.Enter();
			}
			int savedByBatching = Math.Max(0, (int)currentOwnersCount - 1);
			RenderBatchStatistics.FullSavedByBatching += savedByBatching;
			if (SceneRenderScope.IsInside) {
				RenderBatchStatistics.SceneSavedByBatching += savedByBatching;
			}
			usageStartInfo = ProfilerDatabase.CpuUsageStarted();
		}

		public void Rendered()
		{
			if (IsInsideOverdrawMaterialScope) {
				OverdrawMaterialScope.Leave();
			}
			var ownersPool = ProfilerDatabase.OwnersPool;
			if (ownersPool != null) {
				var list = ownersPool.AcquireList();
				ownersPool.AddToNewestList(ownersBuffer, currentOwnersCount);
				ProfilerDatabase.CpuUsageFinished(usageStartInfo, new Owners(list), UsageReasons, TypeIdentifier.Empty);
			}
		}
	}
}
#endif // PROFILER
