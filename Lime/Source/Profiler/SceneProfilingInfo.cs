#if PROFILER
namespace Lime.Profiler
{
	public static class SceneProfilingInfo
	{
		/// <summary>
		/// Node manager for scene objects.
		/// </summary>
		public static object NodeManager;
	}
	
	public static class SceneRenderScope
	{
		private static int scopeCount = 0;

		/// <summary>
		/// Indicates whether we are inside the OverdrawMaterialScope or not.
		/// </summary>
#if TANGERINE 
		public static bool IsInside => scopeCount > 0;
#else
		public static bool IsInside => true;
#endif // TANGERINE
		
		public static void Enter() => ++scopeCount;
		public static void Leave() => --scopeCount;
	}
}
#endif // PROFILER
