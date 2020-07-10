using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.Charts
{
	internal interface IPartialInvalidationMesh<T> where T : unmanaged
	{
		void FlushInvalidatedData();
		void InvalidateVerticesRange(int offset, int startVertex, int verticesCount);
		void InvalidateIndicesRange(int offset, int startIndex, int indicesCount);
	}

	internal class ChartsMesh<T> : Mesh<T>, IPartialInvalidationMesh<T> where T : unmanaged
	{
		private List<InvalidationRange> verticesRanges = new List<InvalidationRange>();
		private List<InvalidationRange> indicesRanges = new List<InvalidationRange>();

		void IPartialInvalidationMesh<T>.FlushInvalidatedData()
		{
			foreach (var range in verticesRanges) {
				vertexBuffer.SetData(
					range.Offset,
					Vertices,
					range.StartIndex,
					range.ItemsCount);
			}
			verticesRanges.Clear();
			foreach (var range in indicesRanges) {
				indexBuffer.SetData(
					range.Offset,
					Vertices,
					range.StartIndex,
					range.ItemsCount);
			}
			indicesRanges.Clear();
		}

		void IPartialInvalidationMesh<T>.InvalidateIndicesRange(int offset, int startIndex, int indicesCount)
		{
			indicesRanges.Add(new InvalidationRange {
				Offset      = offset,
				StartIndex  = startIndex,
				ItemsCount  = indicesCount
			});
		}

		void IPartialInvalidationMesh<T>.InvalidateVerticesRange(int offset, int startVertex, int verticesCount)
		{
			verticesRanges.Add(new InvalidationRange {
				Offset      = offset,
				StartIndex  = startVertex,
				ItemsCount  = verticesCount
			});
		}

		private struct InvalidationRange
		{
			/// <summary>
			/// Offset from the beginning of the buffer in bytes.
			/// </summary>
			public int Offset;

			/// <summary>
			/// Index of the first element in an array of indices or vertices.
			/// </summary>
			public int StartIndex;

			/// <summary>
			/// The number of elements in the vertex or index array to copy.
			/// </summary>
			public int ItemsCount;
		}
	}
}
