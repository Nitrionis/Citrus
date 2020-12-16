#if PROFILER

using Lime.Profiler.Network;
using Yuzu;

namespace Lime.Profiler.Contexts
{
	public struct ProfilerOptions : IMessage
	{
		[YuzuMember]
		public bool ProfilerEnabled;
		[YuzuMember]
		public bool BatchBreakReasonsRequired;
		[YuzuMember]
		public bool IsSceneUpdateFrozen;
		[YuzuMember]
		public bool OverdrawModeEnabled;
	}
}

#endif // PROFILER
