using System.Collections.Generic;
using Yuzu;

namespace Lime.Graphics.Platform.Profiling
{
	/// <summary>
	/// Describes GPU usage period.
	/// </summary>
	public class GpuUsage : ITimePeriod
	{
		private static readonly Stack<GpuUsage> freeInstances = new Stack<GpuUsage>();

		public uint Start
		{
			get => StartTime;
			set => StartTime = value;
		}

		public uint Finish
		{
			get => FinishTime;
			set => FinishTime = value;
		}

		[YuzuRequired]
		public GpuCallInfo GpuCallInfo;

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

		public GpuUsage() { }

		public static GpuUsage Acquire() =>
			freeInstances.Count > 0 ? freeInstances.Pop() : new GpuUsage();

		public void Free()
		{
			GpuCallInfo?.Free();
			freeInstances.Push(this);
		}
	}
}
