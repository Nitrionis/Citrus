using System.Collections.Generic;
using Yuzu;

namespace Lime.Profilers
{
	public class CpuUsage : ITimePeriod
	{
		private static Stack<CpuUsage> updatePool = new Stack<CpuUsage>();
		private static Stack<CpuUsage> renderPool = new Stack<CpuUsage>();

		public enum UsageReason
		{
			Animation,
			Update,
			RenderPreparation,
			Render
		}

		/// <summary>
		/// Reason for using a processor.
		/// </summary>
		[YuzuRequired]
		public UsageReason Reason;

		/// <summary>
		/// <list type="bullet">
		/// <item><description>This is Node, if <see cref="CpuUsage"/> created on the local device.</description></item>
		/// <item><description>This is Node.Id, if <see cref="CpuUsage"/> received from outside.</description></item>
		/// </list>
		/// </summary>
		[YuzuRequired]
		public object Owner;

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
		/// Not fully thread safe.
		/// </summary>
		public static CpuUsage Acquire(UsageReason reason)
		{
			// Rendering thread can run in parallel with update thread.
			if (reason == UsageReason.Render) {
				return renderPool.Count > 0 ? renderPool.Pop() : new CpuUsage();
			} else {
				return updatePool.Count > 0 ? updatePool.Pop() : new CpuUsage();
			}
		}

		/// <summary>
		/// Not fully thread safe.
		/// </summary>
		public void Free()
		{
			Owner = null;
			if (Reason == UsageReason.Render) {
				renderPool.Push(this);
			} else {
				updatePool.Push(this);
			}
		}
	}
}
