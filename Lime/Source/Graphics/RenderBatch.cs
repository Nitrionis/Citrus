using System;
using System.Collections.Generic;
using Lime.Graphics.Platform;

namespace Lime
{
	public interface IRenderBatch
	{
		int LastVertex { get; set; }
		int StartIndex { get; set; }
		int LastIndex { get; set; }
		ITexture Texture1 { get; set; }
		ITexture Texture2 { get; set; }
		IMaterial Material { get; set; }
		void Render();
		void Release();
	}

	public static class RenderBatchLimits
	{
		public const int MaxVertices = 400;
		public const int MaxIndices = 600;
	}

#if !PROFILER_GPU
	public class RenderBatch<TVertex> : IRenderBatch where TVertex : unmanaged
#else
	public class RenderBatch<TVertex> : RenderBatchProfiler, IRenderBatch where TVertex : unmanaged
#endif
	{
		private static Stack<RenderBatch<TVertex>> batchPool = new Stack<RenderBatch<TVertex>>();
		private static Stack<Mesh<TVertex>> meshPool = new Stack<Mesh<TVertex>>();
		private bool ownsMesh;

		public ITexture Texture1 { get; set; }
		public ITexture Texture2 { get; set; }
		public IMaterial Material { get; set; }
		public int LastVertex { get; set; }
		public int StartIndex { get; set; }
		public int LastIndex { get; set; }
		public Mesh<TVertex> Mesh { get; set; }

		private void Clear()
		{
			Texture1 = null;
			Texture2 = null;
			Material = null;
			StartIndex = LastIndex = LastVertex = 0;
			if (Mesh != null) {
				if (ownsMesh) {
					ReleaseMesh(Mesh);
				}
				Mesh = null;
			}
			ownsMesh = false;
#if PROFILER_GPU
			isPartOfScene = false;
			drawCallsOwners = new List<object>();
#endif
		}

		public void Render()
		{
#if PROFILER_GPU
			int savedByBatching = drawCallsOwners.Count == 0 ?
				0 : (drawCallsOwners.Count - 1) * Material.PassCount;
			FullSavedByBatching += savedByBatching;
			SceneSavedByBatching += isPartOfScene ? savedByBatching : 0;
			var profilingInfo = ProfilingInfo.Acquire(drawCallsOwners, isPartOfScene, Material, 0);
#endif
			PlatformRenderer.SetTexture(0, Texture1);
			PlatformRenderer.SetTexture(1, Texture2);
			for (int i = 0; i < Material.PassCount; i++) {
				Material.Apply(i);
#if !PROFILER_GPU
				Mesh.DrawIndexed(StartIndex, LastIndex - StartIndex);
#else
				profilingInfo.CurrentRenderPassIndex = i;
				Mesh.DrawIndexed(StartIndex, LastIndex - StartIndex, 0, profilingInfo);
#endif
			}
		}

		public static RenderBatch<TVertex> Acquire(RenderBatch<TVertex> origin)
		{
			var batch = batchPool.Count == 0 ? new RenderBatch<TVertex>() : batchPool.Pop();
			if (origin != null) {
				batch.Mesh = origin.Mesh;
				batch.StartIndex = origin.LastIndex;
				batch.LastVertex = origin.LastVertex;
				batch.LastIndex = origin.LastIndex;
			} else {
				batch.ownsMesh = true;
				batch.Mesh = AcquireMesh();
			}
			return batch;
		}

		public void Release()
		{
			Clear();
			batchPool.Push(this);
		}

		private static Mesh<TVertex> AcquireMesh()
		{
			if (meshPool.Count == 0) {
				var mesh = new Mesh<TVertex> {
					Vertices = new TVertex[RenderBatchLimits.MaxVertices],
					Indices = new ushort[RenderBatchLimits.MaxIndices],
					AttributeLocations = new int[] {
						ShaderPrograms.Attributes.Pos1,
						ShaderPrograms.Attributes.Color1,
						ShaderPrograms.Attributes.UV1,
						ShaderPrograms.Attributes.UV2
					}
				};
				return mesh;
			}
			return meshPool.Pop();
		}

		private static void ReleaseMesh(Mesh<TVertex> item)
		{
			meshPool.Push(item);
		}
	}
}
