#if PROFILER

namespace Lime.Profiler
{
	/// <summary>
	/// Contains detailed data on the consumption of processor time by each node for various types of tasks.
	/// </summary>
	public struct NodesTiming
	{
		/// <summary>
		/// This is <see cref="ProfiledFrame.Identifier"/>.
		/// </summary>
		public ulong FrmeIdentifier;

		/// <summary>
		/// ???????????????????
		/// </summary>
		public RingPool.ListDescriptor CpuUsages;
	}
}

#endif // PROFILER
