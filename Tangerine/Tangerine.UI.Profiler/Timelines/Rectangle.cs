#if PROFILER

using Lime;

namespace Tangerine.UI.Timelines
{
	internal struct Rectangle
	{
		public const int VertexCount = 4;
		public const int IndexCount = 6;

		public Vector2 Position;
		public Vector2 Size;
		public int ColorIndex;

		public void WriteVerticesTo(Vector3[] buffer, int offset)
		{
			buffer[offset + 0] = new Vector3(Position.X,          Position.Y,          ColorIndex);
			buffer[offset + 1] = new Vector3(Position.X,          Position.Y + Size.Y, ColorIndex);
			buffer[offset + 2] = new Vector3(Position.X + Size.X, Position.Y + Size.Y, ColorIndex);
			buffer[offset + 3] = new Vector3(Position.X + Size.X, Position.Y,          ColorIndex);
		}

		public static void WriteIndicesTo(ushort[] buffer, int offset, int startVertex)
		{
			buffer[offset + 0] = (ushort)(startVertex + 0);
			buffer[offset + 1] = (ushort)(startVertex + 1);
			buffer[offset + 2] = (ushort)(startVertex + 2);
			buffer[offset + 3] = (ushort)(startVertex + 0);
			buffer[offset + 4] = (ushort)(startVertex + 2);
			buffer[offset + 5] = (ushort)(startVertex + 3);
		}
	}
}

#endif // PROFILER