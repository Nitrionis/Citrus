using System;

namespace Lime.Profiler
{
	/// <summary>
	/// Describes GPU usage period.
	/// </summary>
	public struct GpuUsage
	{
		/// <summary>
		/// List of row indices in a ReferencesTable.
		/// </summary>
		public Owners Owners;

		/// <summary>
		/// Material type GUID.
		/// </summary>
		public Guid MaterialGuid;

		/// <summary>
		/// Render Pass index of the material.
		/// </summary>
		public int RenderPassIndex;

		/// <summary>
		/// Vertices count.
		/// </summary>
		public int VerticesCount;

		/// <summary>
		/// The potential number of polygons.
		/// </summary>
		public int TrianglesCount;

		/// <summary>
		/// The time in microseconds from the start of the rendering
		/// of the frame when the command started execution on the GPU.
		/// </summary>
		/// <remarks>
		/// For example, for Vulkan, this is VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT pipeline stage.
		/// </remarks>
		public uint StartTime;

		/// <summary>
		/// The time in microseconds since the start of the rendering
		/// of the frame when all previous command completed execution on the GPU.
		/// </summary>
		/// <remarks>
		/// For example, for Vulkan, this is VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT pipeline stage.
		/// When writing to the command buffer, vkCmdWriteTimestamp is called BEFORE the drawing command.
		/// </remarks>
		public uint AllPreviousFinishTime;

		/// <summary>
		/// The time in microseconds since the start of the rendering
		/// of the frame when the command completed execution on the GPU.
		/// </summary>
		/// <remarks>
		/// For example, for Vulkan, this is VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT pipeline stage.
		/// When writing to the command buffer, vkCmdWriteTimestamp is called AFTER the drawing command.
		/// </remarks>
		public uint FinishTime;
	}
}
