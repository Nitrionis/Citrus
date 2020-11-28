using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ProfilerUnitTests
{
	internal class PerformanceTest
	{
		public static void Run(Action action, long iterationsCount)
		{
			var savedProcessorAffinity = Process.GetCurrentProcess().ProcessorAffinity;
			var savedProcessPriority = Process.GetCurrentProcess().PriorityClass;
			var savedThreadPriority = Thread.CurrentThread.Priority;
			Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(1);
			Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
			Thread.CurrentThread.Priority = ThreadPriority.Highest;
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			var startgc = GetGcCount();
			var sw = Stopwatch.StartNew();
			action?.Invoke();
			Console.WriteLine("PerformanceTest.Run\n Total time {0} ms\n One frame {1} ms",
				sw.Elapsed.TotalMilliseconds, sw.Elapsed.TotalMilliseconds / iterationsCount);
			Console.WriteLine($"{startgc}\n{GetGcCount()}");
			Process.GetCurrentProcess().ProcessorAffinity = savedProcessorAffinity;
			Process.GetCurrentProcess().PriorityClass = savedProcessPriority;
			Thread.CurrentThread.Priority = savedThreadPriority;
		}

		private static string GetGcCount() =>
			$"GC 0:{GC.CollectionCount(0)} GC 1:{GC.CollectionCount(1)} GC 2:{GC.CollectionCount(2)}";
	}
}
