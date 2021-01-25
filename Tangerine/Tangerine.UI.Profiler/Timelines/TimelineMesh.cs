#if PROFILER

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lime;
using Tangerine.UI.Charts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	
	internal class TimelineMesh
	{
		private readonly RectanglesMesh rectanglesMesh;
		private readonly ConcurrentQueue<RebuildRequest> pendingRequests;
		private readonly Queue<List<RectanglesMesh.Chunk>> unusedResources;

		private Task<RebuildingInfo> rebuildingTask;
		
		public TimelineMesh()
		{
			rectanglesMesh = new RectanglesMesh();
			unusedResources = new Queue<List<RectanglesMesh.Chunk>>();
			unusedResources.Enqueue(new List<RectanglesMesh.Chunk>());
			pendingRequests = new ConcurrentQueue<RebuildRequest>();
		}

		/// <summary>
		/// Creates a request for an asynchronous timeline rebuild.
		/// </summary>
		/// <param name="rectangles">
		/// List of rectangles to draw.
		/// Rectangles should not change until the request completes.
		/// </param>
		/// <returns>
		/// The task by which you can find out when the request is completed.
		/// </returns>
		/// <remarks>
		/// Must be called from the update thread only.
		/// </remarks>
		public Task RebuildAsync(IEnumerable<Rectangle> rectangles)
		{
			var taskCompletionSource = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			pendingRequests.Enqueue(new RebuildRequest {
				Rectangles = rectangles,
				TaskCompletionSource = taskCompletionSource
			});
			return taskCompletionSource.Task;
		}

		public void Draw()
		{
			if (rebuildingTask != null && rebuildingTask.IsCompleted) {
				var meshData = rebuildingTask.Result.ChangeableMeshData;
				unusedResources.Enqueue(meshData);
				rectanglesMesh.Chunks = meshData;
				rebuildingTask = null;
			}
			rectanglesMesh.Draw();
			// At this point the data has already been copied to another buffer.
			int pendingRequestsCount = pendingRequests.Count;
			while (pendingRequestsCount > 1) {
				if (pendingRequests.TryDequeue(out RebuildRequest request)) {
					request.TaskCompletionSource.SetResult(true);
					pendingRequestsCount--;
				}
			}
			if (pendingRequestsCount == 1 && unusedResources.Count > 0) {
				while (true) {
					if (pendingRequests.TryDequeue(out RebuildRequest request)) {
						var meshData = unusedResources.Dequeue();
						rebuildingTask = Task.Run<RebuildingInfo>(() => {
							RebuildMesh(new RebuildingInfo {
								RebuildRequest = request,
								ChangeableMeshData = meshData
							});
							request.TaskCompletionSource.SetResult(true);
							return null;
						});
						break;
					}
				}
			}
		}

		private struct RebuildRequest
		{
			public IEnumerable<Rectangle> Rectangles;
			public TaskCompletionSource<bool> TaskCompletionSource;
		}

		private struct RebuildingInfo
		{
			public RebuildRequest RebuildRequest;
			public List<RectanglesMesh.Chunk> ChangeableMeshData;
		}
		
		private static void RebuildMesh(RebuildingInfo rebuildingInfo)
		{
			var chunks = rebuildingInfo.ChangeableMeshData;
			void SetVisibleRectanglesCount(int chunk, int visibleRectanglesCount) {
				var changedStruct = chunks[chunk];
				changedStruct.VisibleRectanglesCount = visibleRectanglesCount;
				chunks[chunk] = changedStruct;
			}
			int chunkIndex = -1;
			int verticesOffset = RectanglesMesh.Chunk.MaxVerticesCount;
			int indicesOffset = RectanglesMesh.Chunk.MaxIndicesCount;
			foreach (var rectangle in rebuildingInfo.RebuildRequest.Rectangles) {
				if (verticesOffset == RectanglesMesh.Chunk.MaxVerticesCount) {
					if (chunkIndex >= 0) {
						SetVisibleRectanglesCount(chunkIndex, RectanglesMesh.Chunk.MaxRectanglesCount);
					}
					verticesOffset = 0;
					indicesOffset = 0;
					++chunkIndex;
					if (chunkIndex == chunks.Count) {
						chunks.Add(new RectanglesMesh.Chunk {
							Vertices = new Vector3[RectanglesMesh.Chunk.MaxVerticesCount],
							Indices = new ushort[RectanglesMesh.Chunk.MaxIndicesCount]
						});
					}
				}
				var chunk = chunks[chunkIndex];
				rectangle.WriteVerticesTo(chunk.Vertices, verticesOffset);
				Rectangle.WriteIndicesTo(chunk.Indices, indicesOffset, verticesOffset);
				verticesOffset += Rectangle.VertexCount;
				indicesOffset += Rectangle.IndexCount;
			}
			if (chunkIndex >= 0) {
				SetVisibleRectanglesCount(chunkIndex, verticesOffset / Rectangle.VertexCount);
			}
			for (int i = ++chunkIndex; i < chunks.Count; i++) {
				SetVisibleRectanglesCount(i, 0);
			}
		}
		
		private class RectanglesMesh
		{
			private readonly List<Mesh<Vector3>> meshes = new List<Mesh<Vector3>>();
			
			private List<Chunk> chunks;

			public List<Chunk> Chunks
			{
				get { return chunks; }
				set {
					chunks = value;
					if (chunks == null) {
						throw new NullReferenceException();
					}
					while (meshes.Count < chunks.Count) {
						meshes.Add(new Mesh<Vector3> {
							Indices = null,
							Vertices = null,
							Topology = PrimitiveTopology.TriangleList,
							AttributeLocations = ChartsMaterial.ShaderProgram.MeshAttribLocations,
						});
					}
					for (int i = 0; i < chunks.Count; i++) {
						var chunk = chunks[i];
						chunk.MeshDirtyFlags |= MeshDirtyFlags.VerticesIndices;
						chunks[i] = chunk;
					}
				}
			}

			public struct Chunk
			{
				public const int MaxRectanglesCount = 65536 / Rectangle.VertexCount;
				public const int MaxVerticesCount = (65536 / Rectangle.VertexCount) * Rectangle.VertexCount;
				public const int MaxIndicesCount = (65536 / Rectangle.VertexCount) * Rectangle.IndexCount;

				public Vector3[] Vertices;
				public ushort[] Indices;
				public int VisibleRectanglesCount;
				public MeshDirtyFlags MeshDirtyFlags;
			}

			public void Draw()
			{
				if (chunks != null) {
					for (int i = 0; i < chunks.Count; i++) {
						var chunk = chunks[i];
						if (chunk.VisibleRectanglesCount != 0) {
							var mesh = meshes[i];
							mesh.DirtyFlags = chunk.MeshDirtyFlags;
							mesh.Vertices = chunk.Vertices;
							mesh.Indices = chunk.Indices;
							mesh.DrawIndexed(0, Rectangle.IndexCount * chunk.VisibleRectanglesCount);
						}
					}
				}
			}
		}
	}
}

#endif // PROFILER
