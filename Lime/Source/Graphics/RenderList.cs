using System.Collections.Generic;
#if PROFILER
using Lime.Profiler;
using Lime.Profiler.Graphics;
#endif // PROFILER

namespace Lime
{
	using Reasons = CpuUsage.Reasons;

	public class RenderList
	{
		public readonly List<IRenderBatch> Batches = new List<IRenderBatch>();
		private IRenderBatch lastBatch;

		public bool Empty { get { return lastBatch == null; } }

		public RenderBatch<TVertex> GetBatch<TVertex>(ITexture texture1, ITexture texture2, IMaterial material, int vertexCount, int indexCount)
			where TVertex : unmanaged
		{
			var atlas1 = texture1?.AtlasTexture;
			var atlas2 = texture2?.AtlasTexture;
			var typedLastBatch = lastBatch as RenderBatch<TVertex>;
			var needMesh =
				typedLastBatch == null ||
				typedLastBatch.LastVertex + vertexCount > RenderBatchLimits.MaxVertices ||
				typedLastBatch.LastIndex + indexCount > RenderBatchLimits.MaxIndices;
			if (needMesh ||
				typedLastBatch.Texture1 != atlas1 ||
				typedLastBatch.Texture2 != atlas2 ||
				typedLastBatch.Material != material ||
				typedLastBatch.Material.PassCount != 1
			) {
#if PROFILER
				var batchBreakReasons = GetBatchBreakReasons();
#endif // PROFILER
				typedLastBatch = RenderBatch<TVertex>.Acquire(needMesh ? null : typedLastBatch);
				typedLastBatch.Texture1 = atlas1;
				typedLastBatch.Texture2 = atlas2;
				typedLastBatch.Material = material;
				Batches.Add(typedLastBatch);
				lastBatch = typedLastBatch;
#if PROFILER
				typedLastBatch.ProfilingInfo.UsageReasons = batchBreakReasons;
#endif // PROFILER
			}
#if PROFILER
			typedLastBatch.ProfilingInfo.ProcessNode(RenderObjectOwnerInfo.CurrentNode);
#endif // PROFILER
			return typedLastBatch;

#if PROFILER
			Reasons GetBatchBreakReasons()
			{
				var usageReason = Reasons.BatchRender;
				if (ProfilerDatabase.IsRenderThreadDataCollectionAllowed) {
					if (typedLastBatch == null) {
						usageReason |= Reasons.BatchBreakNullLastBatch;
					} else {
						bool isVertexBufferOverflow =
							typedLastBatch.LastVertex + vertexCount > RenderBatchLimits.MaxVertices;
						bool isIndexBufferOverflow =
							typedLastBatch.LastIndex + indexCount > RenderBatchLimits.MaxIndices;
						usageReason |=
							(isVertexBufferOverflow ? Reasons.BatchBreakVertexBufferOverflow : 0) |
							(isIndexBufferOverflow ? Reasons.BatchBreakIndexBufferOverflow : 0) |
							(typedLastBatch.Texture1 != atlas1 ? Reasons.BatchBreakDifferentAtlasOne : 0) |
							(typedLastBatch.Texture2 != atlas2 ? Reasons.BatchBreakDifferentAtlasTwo : 0) |
							(typedLastBatch.Material != material ? Reasons.BatchBreakDifferentMaterials : 0) |
							(typedLastBatch.Material.PassCount != 1 ? Reasons.BatchBreakMaterialPassCount : 0);
					}
				}
				return usageReason;
			}
#endif // PROFILER
		}

		public void Render()
		{
			foreach (var batch in Batches) {
				batch.Render();
			}
		}

		public void Clear()
		{
			if (lastBatch == null) {
				return;
			}
			foreach (var i in Batches) {
				i.Release();
			}
			Batches.Clear();
			lastBatch = null;
		}

		public void Flush()
		{
			if (lastBatch != null) {
				Render();
				Clear();
			}
		}
	}
}
