#if PROFILER

namespace Lime.Profiler
{
	/// <summary>
	/// Describes CPU usage interval.
	/// </summary>
	public struct CpuUsage
	{
		/// <summary>
		/// Reason for using a processor.
		/// </summary>
		public Reasons Reason;

		/// <summary>
		/// List of row indices in a ReferencesTable.
		/// </summary>
		public Owners Owners;

		/// <summary>
		/// The timestamp of the start of the usage interval in microseconds from the start of the frame update.
		/// </summary>
		public uint StartTime;

		/// <summary>
		/// The timestamp of the finish of the usage interval in microseconds from the start of the frame update.
		/// </summary>
		public uint FinishTime;

		public enum Reasons : uint
		{
			Animation,
			Update,
			Gesture,
			RenderPreparation,
			NodeRender,
			BatchRender,
			WaitForPreviousRendering,
			WaitForAcquiringSwapchainBuffer,
			ReferenceTableGarbageCollection,
		}
	}
}

#endif // PROFILER
