using System;
using Yuzu;

namespace Lime.Graphics.Platform.Profiling
{
	public static class CpuUsageReasonsExtensions
	{
		public static bool Include(this CpuUsage.UsageReasons self, CpuUsage.UsageReasons flag) => (self & flag) == flag;
	}

	/// <summary>
	/// Describes CPU usage interval.
	/// </summary>
	public struct CpuUsage
	{
		[Flags]
		public enum UsageReasons : int
		{
			UpdateThreadFlag   = 1 << 0,
			RenderThreadFlag   = 1 << 1,
			Animation          = 1 << 2 | UpdateThreadFlag,
			Update             = 1 << 3 | UpdateThreadFlag,
			Gesture            = 1 << 4 | UpdateThreadFlag,
			RenderPreparation  = 1 << 5 | UpdateThreadFlag,
			NodeRender         = 1 << 6 | RenderThreadFlag,
			BatchRender        = 1 << 7 | RenderThreadFlag,
			ReasonBits         = Animation | Update | Gesture | RenderPreparation | NodeRender | BatchRender
		}

		/// <summary>
		/// Reason for using a processor.
		/// </summary>
		[YuzuRequired]
		public UsageReasons Reasons;

		/// <summary>
		/// The indexes of the objects in the ReferencesTable to which the usage interval belongs.
		/// </summary>
		[YuzuRequired]
		public Owners Owners;

		/// <summary>
		/// Indicates whether the owner is part of the scene.
		/// </summary>
		[YuzuRequired]
		public bool IsPartOfScene;

		/// <summary>
		/// The timestamp of the start of the usage interval in microseconds from the start of the frame update.
		/// </summary>
		[YuzuRequired]
		public uint StartTime;

		/// <summary>
		/// The timestamp of the finish of the usage interval in microseconds from the start of the frame update.
		/// </summary>
		[YuzuRequired]
		public uint FinishTime;
	}
}
