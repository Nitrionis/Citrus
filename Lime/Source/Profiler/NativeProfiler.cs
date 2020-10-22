using System;
using System.Threading;

namespace Lime.Profiler
{
	public class NativeProfiler
	{
		[ThreadStatic] private static bool isEnabled;

		/// <summary>
		/// Used to determine if the profiler is enabled in the current thread.
		/// </summary>
		/// <remarks>
		/// Profiling is enabled for the main (scene, game) window only.
		/// </remarks>
		public static bool IsEnabled => isEnabled;

		public static void ConfigureForCurrentThread(bool enabled)
		{
			isEnabled = enabled;
		}
	}
}
