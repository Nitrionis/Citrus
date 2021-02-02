#if PROFILER

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lime;
using Tangerine.UI.Charts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	
	internal class TimelineMesh
	{
		private readonly RectanglesMesh rectanglesMesh;
		private readonly List<RectanglesMesh.Chunk> chunks;
		
		private RebuildRequest pendingRequest;
		private long newestTaskId;
		private Task<RebuildingInfo> rebuildingTask;
		private TaskCompletionSource<bool> taskCompletionSource;
		
		public TimelineMesh()
		{
			rectanglesMesh = new RectanglesMesh();
			chunks = new List<RectanglesMesh.Chunk>();
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
			Window.Current.Invalidate();
			taskCompletionSource?.SetResult(true);
			taskCompletionSource = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			pendingRequest = new RebuildRequest {
				CurrentTaskId = Interlocked.Increment(ref newestTaskId),
				Rectangles = rectangles,
				TaskCompletionSource = taskCompletionSource
			};
			return taskCompletionSource.Task;
		}

		public RenderObject GetRenderObject()
		{
			var ro = new RenderObject(this);
			taskCompletionSource = null;
			return ro;
		}
		
		private void Draw(bool isRebuildRequired, RebuildRequest request)
		{
			if (rebuildingTask != null && rebuildingTask.IsCompleted) {
				var meshData = rebuildingTask.Result.ChangeableMeshData;
				if (!rebuildingTask.Result.IsCanceled) {
					rectanglesMesh.Chunks = meshData;
				}
				rebuildingTask = null;
			}
			rectanglesMesh.Draw();
			// At this point the data has already been copied to another buffer.
			if (isRebuildRequired) {
				rebuildingTask = Task.Run(() => {
					bool isNotCanceled = 
						request.CurrentTaskId != Interlocked.Read(ref newestTaskId);
					var rebuildingInfo = new RebuildingInfo {
						IsCanceled = !isNotCanceled,
						RebuildRequest = request,
						ChangeableMeshData = chunks
					};
					if (isNotCanceled) {
						RebuildMesh(rebuildingInfo);
					}
					request.TaskCompletionSource.SetResult(true);
					return rebuildingInfo;
				});
			}
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
		
		public struct RenderObject
		{
			private readonly TimelineMesh timelineMesh;
			private readonly RebuildRequest request;
			private readonly bool hasRequest;
			
			public RenderObject(TimelineMesh timelineMesh)
			{
				this.timelineMesh = timelineMesh;
				request = timelineMesh.pendingRequest;
				hasRequest = timelineMesh.taskCompletionSource != null;
			}

			public void Render() => timelineMesh.Draw(hasRequest, request);
		}
		
		private struct RebuildRequest
		{
			public long CurrentTaskId;
			public IEnumerable<Rectangle> Rectangles;
			public TaskCompletionSource<bool> TaskCompletionSource;
		}

		private struct RebuildingInfo
		{
			public bool IsCanceled;
			public RebuildRequest RebuildRequest;
			public List<RectanglesMesh.Chunk> ChangeableMeshData;
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
