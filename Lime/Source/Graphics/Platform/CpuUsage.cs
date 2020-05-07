using System;
using System.Collections.Generic;
using Lime.Profilers;
using Yuzu;

namespace Lime.Graphics.Platform
{
	public static class CpuUsageReasonsExtensions
	{
		public static bool Include(this CpuUsage.UsageReasons self, CpuUsage.UsageReasons flag) => (self & flag) == flag;
	}

	public class CpuUsage : ITimePeriod
	{
		private static Stack<CpuUsage> updatePool = new Stack<CpuUsage>();
		private static Stack<CpuUsage> renderPool = new Stack<CpuUsage>();

		/// <remarks>
		/// Order is important!
		/// </remarks>
		[Flags]
		public enum UsageReasons : int
		{
			UpdateThreadFlag  = 1 << 0,
			RenderThreadFlag  = 1 << 1,
			Animation         = 1 << 2 | UpdateThreadFlag,
			Update            = 1 << 3 | UpdateThreadFlag,
			Gesture           = 1 << 4 | UpdateThreadFlag,
			RenderPreparation = 1 << 5 | UpdateThreadFlag,
			NodeRender        = 1 << 6 | RenderThreadFlag,
			BatchRender       = 1 << 7 | RenderThreadFlag,
			ReasonBits      = Animation | Update | Gesture | RenderPreparation | NodeRender | BatchRender
		}

		/// <summary>
		/// Reason for using a processor.
		/// </summary>
		[YuzuRequired]
		public UsageReasons Reasons;

		/// <summary>
		/// if <see cref="Reasons"/> is BatchRender:
		/// <list type="bullet">
		/// <item><description>
		/// If <see cref="CpuUsage"/> created on the local device, this can be List of Node or null.
		/// </description></item>
		/// <item><description>
		/// If <see cref="CpuUsage"/> received from outside, this can be List of Node.Id or null.
		/// </description></item>
		/// </list>
		/// else this can be Node or Node.Id or null.
		/// </summary>
		[YuzuRequired]
		public object Owners;

		/// <summary>
		/// Indicates whether the owner is part of the scene.
		/// </summary>
		[YuzuRequired]
		public bool IsPartOfScene;

		/// <summary>
		/// The timestamp of the start of the usage interval in microseconds from the start of the frame update.
		/// </summary>
		[YuzuRequired]
		public uint Start { get; set; }

		/// <summary>
		/// The timestamp of the finish of the usage interval in microseconds from the start of the frame update.
		/// </summary>
		[YuzuRequired]
		public uint Finish { get; set; }

		/// <summary>
		/// Acquire CpuUsage from a pool. Not fully thread safe.
		/// </summary>
		public static CpuUsage Acquire(UsageReasons reason)
		{
			var usage = ((reason & UsageReasons.RenderThreadFlag) != 0)
				? renderPool.Count > 0 ? renderPool.Pop() : new CpuUsage()
				: updatePool.Count > 0 ? updatePool.Pop() : new CpuUsage();
			usage.Reasons = reason;
			return usage;
		}

		/// <summary>
		/// Puts CpuUsage in the corresponding pool for reuse. Not fully thread safe.
		/// </summary>
		public void Free()
		{
			Owners = null;
			if ((Reasons & UsageReasons.RenderThreadFlag) != 0) {
				renderPool.Push(this);
			} else {
				updatePool.Push(this);
			}
		}
	}
}
