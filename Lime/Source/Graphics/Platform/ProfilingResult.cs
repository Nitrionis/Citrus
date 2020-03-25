using System.Collections.Generic;
using Yuzu;
using Lime.Profilers;

namespace Lime.Graphics.Platform
{
	public class ProfilingResult : ITimePeriod
	{
		private static readonly Stack<ProfilingResult> freeInstances = new Stack<ProfilingResult>();

		public uint Start => StartTime;

		public uint Finish => FinishTime;

		public ProfilingInfo ProfilingInfo;

		/// <summary>
		/// Render Pass index of the material.
		/// </summary>
		[YuzuRequired]
		public int RenderPassIndex;

		/// <summary>
		/// Vertices count.
		/// </summary>
		[YuzuRequired]
		public int VerticesCount;

		/// <summary>
		/// The potential number of polygons.
		/// </summary>
		[YuzuRequired]
		public int TrianglesCount;

		/// <summary>
		/// The time in microseconds from the start of the rendering
		/// of the frame when the command started execution on the GPU.
		/// </summary>
		/// <remarks>
		/// For example, for Vulkan, this is VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT pipeline stage.
		/// </remarks>
		[YuzuRequired]
		public uint StartTime;

		/// <summary>
		/// The time in microseconds since the start of the rendering
		/// of the frame when all previous command completed execution on the GPU.
		/// </summary>
		/// <remarks>
		/// For example, for Vulkan, this is VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT pipeline stage.
		/// When writing to the command buffer, vkCmdWriteTimestamp is called BEFORE the drawing command.
		/// </remarks>
		[YuzuRequired]
		public uint AllPreviousFinishTime;

		/// <summary>
		/// The time in microseconds since the start of the rendering
		/// of the frame when the command completed execution on the GPU.
		/// </summary>
		/// <remarks>
		/// For example, for Vulkan, this is VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT pipeline stage.
		/// When writing to the command buffer, vkCmdWriteTimestamp is called AFTER the drawing command.
		/// </remarks>
		[YuzuRequired]
		public uint FinishTime;

		public ProfilingResult() { }

		public static ProfilingResult Acquire() =>
			freeInstances.Count > 0 ? freeInstances.Pop() : new ProfilingResult();

		public void Free()
		{
			ProfilingInfo?.Free();
			freeInstances.Push(this);
		}
	}
}
