#if PROFILER || OVERDRAW
namespace Lime.Profiler
{
	public interface IProfileableObject
	{
		bool IsPartOfScene { get; }
	}
}
#endif // PROFILER || OVERDRAW
