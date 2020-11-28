using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime.Profiler;
using System.Reflection;
using System.Linq;

namespace ProfilerUnitTests
{
	[TestClass]
	public class UnitTests
	{
		private const int FramesCount = 2000;
		private const int HistorySize = 10;
		private const long UsagesPerFrame = 10000;

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_EmptyMethods()
		{
			PerformanceTest.Run(() => {
				for (int i = 0; i < UsagesPerFrame * FramesCount; i++) {
					EmptyMethod();
				}
			});
		}

		//[TestMethod]
		//[MethodImpl(MethodImplOptions.NoOptimization)]
		//public void TestMethod_GetType()
		//{
		//	var obj = new SimpleProfileableObject();
		//	PerformanceTest.Run(() => {
		//		for (int i = 0; i < UsagesPerFrame * FramesCount; i++) {
		//			GetTypeMethod(obj);
		//		}
		//	});
		//}

		//[TestMethod]
		//[MethodImpl(MethodImplOptions.NoOptimization)]
		//public void TestMethod_GetGuid()
		//{
		//	var obj = new SimpleProfileableObject();
		//	PerformanceTest.Run(() => {
		//		var type = obj.GetType();
		//		for (int i = 0; i < UsagesPerFrame * FramesCount; i++) {
		//			GetGuidMethod(type);
		//		}
		//	});
		//}

		//[TestMethod]
		//[MethodImpl(MethodImplOptions.NoOptimization)]
		//public void TestMethod_GetTypeInfoGuid()
		//{
		//	var obj = new SimpleProfileableObject();
		//	PerformanceTest.Run(() => {
		//		var type = obj.GetType().GetTypeInfo();
		//		for (int i = 0; i < UsagesPerFrame * FramesCount; i++) {
		//			GetTypeInfoGuidMethod(type);
		//		}
		//	});
		//}

		//[TestMethod]
		//[MethodImpl(MethodImplOptions.NoOptimization)]
		//public void TestMethod_GetTypeGuid()
		//{
		//	var obj = new SimpleProfileableObject();
		//	PerformanceTest.Run(() => {
		//		for (int i = 0; i < UsagesPerFrame * FramesCount; i++) {
		//			GetTypeGuidMethod(obj);
		//		}
		//	});
		//}

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_CpuUsages()
		{
			try {
				ReinitializeProfiler();
				var leaf = GenerateHierarchy();
				PerformanceTest.Run(() => {
					for (int i = 0; i < FramesCount; i++) {
						RunUpdate((l) => {
							for (int j = 0; j < UsagesPerFrame; j++) {
								CreateCpuUsage(l);
							}
						}, leaf);
					}
				});
			} catch (Exception e) {
				Console.Write(e);
			}
		}

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_CpuUsages_OwnersLists()
		{
			try {
				ReinitializeProfiler();
				var leaf = GenerateHierarchy();
				PerformanceTest.Run(() => {
					for (int i = 0; i < FramesCount; i++) {
						RunUpdate((l) => {
							for (int j = 0; j < UsagesPerFrame; j++) {
								CreateCpuUsageWithOwnersList(l);
							}
						}, leaf);
					}
				});
			} catch (Exception e) {
				Console.Write(e);
			}
		}

		//[TestMethod]
		//[MethodImpl(MethodImplOptions.NoOptimization)]
		//public void TestMethod_CpuUsages_EmptyGuid()
		//{
		//	try {
		//		ReinitializeProfiler();
		//		var leaf = GenerateHierarchy();
		//		PerformanceTest.Run(() => {
		//			for (int i = 0; i < FramesCount; i++) {
		//				RunUpdate((l) => {
		//					for (int j = 0; j < UsagesPerFrame; j++) {
		//						CreateCpuUsage_EmptyGuid(l);
		//					}
		//				}, leaf);
		//			}
		//		});
		//	} catch (Exception e) {
		//		Console.Write(e);
		//	}
		//}

		//[TestMethod]
		//[MethodImpl(MethodImplOptions.NoOptimization)]
		//public void TestMethod_CpuUsages_OwnersLists_EmptyGuid()
		//{
		//	try {
		//		ReinitializeProfiler();
		//		var leaf = GenerateHierarchy();
		//		PerformanceTest.Run(() => {
		//			for (int i = 0; i < FramesCount; i++) {
		//				RunUpdate((l) => {
		//					for (int j = 0; j < UsagesPerFrame; j++) {
		//						CreateCpuUsageWithOwnersList_EmptyGuid(l);
		//					}
		//				}, leaf);
		//			}
		//		});
		//	} catch (Exception e) {
		//		Console.Write(e);
		//	}
		//}

		//[TestMethod]
		//[MethodImpl(MethodImplOptions.NoOptimization)]
		//public void TestMethod_CpuUsages_Guid()
		//{
		//	try {
		//		ReinitializeProfiler();
		//		var leaf = GenerateHierarchy();
		//		var type = new SimpleProfileableObject().GetType();
		//		PerformanceTest.Run(() => {
		//			for (int i = 0; i < FramesCount; i++) {
		//				RunUpdate((l) => {
		//					for (int j = 0; j < UsagesPerFrame; j++) {
		//						CreateCpuUsage_Guid(l, type);
		//					}
		//				}, leaf);
		//			}
		//		});
		//	} catch (Exception e) {
		//		Console.Write(e);
		//	}
		//}

		//[TestMethod]
		//[MethodImpl(MethodImplOptions.NoOptimization)]
		//public void TestMethod_CpuUsages_OwnersLists_Guid()
		//{
		//	try {
		//		ReinitializeProfiler();
		//		var leaf = GenerateHierarchy();
		//		var type = new SimpleProfileableObject().GetType();
		//		PerformanceTest.Run(() => {
		//			for (int i = 0; i < FramesCount; i++) {
		//				RunUpdate((l) => {
		//					for (int j = 0; j < UsagesPerFrame; j++) {
		//						CreateCpuUsageWithOwnersList_Guid(l, type);
		//					}
		//				}, leaf);
		//			}
		//		});
		//	} catch (Exception e) {
		//		Console.Write(e);
		//	}
		//}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void EmptyMethod() { }

		//[MethodImpl(MethodImplOptions.NoInlining)]
		//private static Type GetTypeMethod(IProfileableObject @object) => @object.GetType();

		//[MethodImpl(MethodImplOptions.NoInlining)]
		//private static Guid GetGuidMethod(Type type) => type.GUID;

		//[MethodImpl(MethodImplOptions.NoInlining)]
		//private static Guid GetTypeInfoGuidMethod(TypeInfo type) => type.GUID;

		//[MethodImpl(MethodImplOptions.NoInlining)]
		//private static Guid GetTypeGuidMethod(IProfileableObject @object) => @object.GetType().GUID;

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void CreateCpuUsage(IProfileableObject @object)
		{
			ProfilerDatabase.EnsureDescriptionFor(@object);
			var usageInfo = ProfilerDatabase.CpuUsageStarted();
			ProfilerDatabase.CpuUsageFinished(usageInfo, @object, CpuUsage.Reasons.NodeUpdate, @object.GetType());
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void CreateCpuUsageWithOwnersList(IProfileableObject @object)
		{
			ProfilerDatabase.EnsureDescriptionFor(@object);
			var list = ProfilerDatabase.OwnersPool.AcquireList();
			ProfilerDatabase.OwnersPool.AddToNewestList(@object.RowIndex);
			var usageInfo = ProfilerDatabase.CpuUsageStarted();
			ProfilerDatabase.CpuUsageFinished(usageInfo, new Owners(list), CpuUsage.Reasons.NodeUpdate, @object.GetType());
		}

		//[MethodImpl(MethodImplOptions.NoInlining)]
		//private static void CreateCpuUsage_EmptyGuid(IProfileableObject @object)
		//{
		//	ProfilerDatabase.EnsureDescriptionFor(@object);
		//	var usageInfo = ProfilerDatabase.CpuUsageStarted(@object, CpuUsage.Reasons.NodeUpdate);
		//	ProfilerDatabase.CpuUsageFinished(usageInfo, null);
		//}

		//[MethodImpl(MethodImplOptions.NoInlining)]
		//private static void CreateCpuUsageWithOwnersList_EmptyGuid(IProfileableObject @object)
		//{
		//	ProfilerDatabase.EnsureDescriptionFor(@object);
		//	var list = ProfilerDatabase.OwnersPool.AcquireList();
		//	ProfilerDatabase.OwnersPool.AddToNewestList(@object.RowIndex);
		//	var usageInfo = ProfilerDatabase.CpuUsageStarted(new Owners(list), CpuUsage.Reasons.NodeUpdate);
		//	ProfilerDatabase.CpuUsageFinished(usageInfo, null);
		//}

		//[MethodImpl(MethodImplOptions.NoInlining)]
		//private static void CreateCpuUsage_Guid(IProfileableObject @object, Type type)
		//{
		//	ProfilerDatabase.EnsureDescriptionFor(@object);
		//	var usageInfo = ProfilerDatabase.CpuUsageStarted(@object, CpuUsage.Reasons.NodeUpdate);
		//	ProfilerDatabase.CpuUsageFinished(usageInfo, null);
		//}

		//[MethodImpl(MethodImplOptions.NoInlining)]
		//private static void CreateCpuUsageWithOwnersList_Guid(IProfileableObject @object, Type type)
		//{
		//	ProfilerDatabase.EnsureDescriptionFor(@object);
		//	var list = ProfilerDatabase.OwnersPool.AcquireList();
		//	ProfilerDatabase.OwnersPool.AddToNewestList(@object.RowIndex);
		//	var usageInfo = ProfilerDatabase.CpuUsageStarted(new Owners(list), CpuUsage.Reasons.NodeUpdate);
		//	ProfilerDatabase.CpuUsageFinished(usageInfo, null);
		//}

		private static string GetGcCount() =>
			$"GC 0:{GC.CollectionCount(0)} GC 1:{GC.CollectionCount(1)} GC 2:{GC.CollectionCount(2)}";

		private static void ReinitializeProfiler()
		{
			ProfilerDatabase.Reinitialize(HistorySize);
			ProfilerDatabase.Updating(true);
			ProfilerDatabase.Updated();
			ProfilerDatabase.SyncStarted();
			ProfilerDatabase.SyncFinishing();
		}

		private static void RunUpdate(Action<IProfileableObject> action, IProfileableObject leaf)
		{
			ProfilerDatabase.Updating(true);
			action.Invoke(leaf);
			ProfilerDatabase.Updated();
			ProfilerDatabase.SyncStarted();
			ProfilerDatabase.SyncFinishing();
		}

		private static IProfileableObject GenerateHierarchy()
		{
			var leaf = new SimpleProfileableObject();
			const int HierarchyDepth = 1000;
			var root = leaf;
			for (int i = 0; i < HierarchyDepth; i++) {
				var newRoot = new SimpleProfileableObject();
				root.Parent = newRoot;
				root = newRoot;
			}
			return leaf;
		}

		private class SimpleProfileableObject : IProfileableObject
		{
			public string Name => nameof(SimpleProfileableObject);
			public bool IsPartOfScene => false;
			public bool IsOverdrawForeground => false;
			public IProfileableObject Parent { get; set; }
			public ReferenceTable.RowIndex RowIndex { get; set; } = ReferenceTable.RowIndex.Invalid;
		}

		private class PerformanceTest
		{
			public static void Run(Action action)
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
				var gcCount = GetGcCount();
				var sw = Stopwatch.StartNew();
				action?.Invoke();
				Console.WriteLine("PerformanceTest.Run\n Total time {0} ms\n One frame {1} ms",
					sw.Elapsed.TotalMilliseconds, sw.Elapsed.TotalMilliseconds / FramesCount);
				Console.WriteLine(gcCount);
				Console.WriteLine(GetGcCount());
				Process.GetCurrentProcess().ProcessorAffinity = savedProcessorAffinity;
				Process.GetCurrentProcess().PriorityClass = savedProcessPriority;
				Thread.CurrentThread.Priority = savedThreadPriority;
			}
		}
	}
}
