using System;
using System.Collections.Generic;
using Lime.Profilers;
using Yuzu;

namespace Lime.Graphics.Platform
{
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
			NoOwnerFlag       = 1 << 0,
			UpdateThreadFlag  = 1 << 1,
			RenderThreadFlag  = 1 << 2,
			Animation         = 1 << 3 | UpdateThreadFlag,
			Update            = 1 << 4 | UpdateThreadFlag,
			Gesture           = 1 << 5 | UpdateThreadFlag,
			RenderPreparation = 1 << 6 | UpdateThreadFlag,
			NodeRender        = 1 << 7 | RenderThreadFlag,
			BatchRender       = 1 << 8 | RenderThreadFlag | NoOwnerFlag,
		}

		/// <summary>
		/// Reason for using a processor.
		/// </summary>
		[YuzuRequired]
		public UsageReasons Reasons;

		/// <summary>
		/// <list type="bullet">
		/// <item><description>This is Node, if <see cref="CpuUsage"/> created on the local device.</description></item>
		/// <item><description>This is Node.Id, if <see cref="CpuUsage"/> received from outside.</description></item>
		/// </list>
		/// </summary>
		[YuzuRequired]
		public object Owner;

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
			Owner = null;
			if ((Reasons & UsageReasons.RenderThreadFlag) != 0) {
				renderPool.Push(this);
			} else {
				updatePool.Push(this);
			}
		}
	}
}
