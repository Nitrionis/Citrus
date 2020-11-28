using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime.Profiler;
using System.Reflection;

namespace ProfilerUnitTests
{
	[TestClass]
	public class SeparateComponentsTests
	{
		private const int IterationCount = 100;
		private const int UsagePerFrame = 10000;

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_Empty()
		{
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; j++) {
					for (uint i = 0; i < 2 * UsagePerFrame; ++i) {
						EmptyMethod();
					}
				}
			}, IterationCount);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void EmptyMethod() { }

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_GetTypeGuid()
		{
			var obj = new SimpleProfileableObject();
			PerformanceTest.Run(() => {
				var sw = Stopwatch.StartNew();
				for (int i = 0; i < IterationCount * UsagePerFrame; i++) {
					sw.Restart();
					GetTypeGuidMethod(obj);
					Console.WriteLine("{0}", sw.Elapsed.TotalMilliseconds);
				}
			}, IterationCount);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Guid GetTypeGuidMethod(IProfileableObject @object) => @object.GetType().GUID;

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_Stopwatch()
		{
			var arr = new long[2 * UsagePerFrame];
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; j++) {
					for (int i = 0; i < 2 * UsagePerFrame; i++) {
						arr[i] = Stopwatch.GetTimestamp();
					}
				}
			}, IterationCount);
			Console.WriteLine(arr.Average());
		}

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_OwnersPool()
		{
			var pool = new RingPool<ReferenceTable.RowIndex>(UsagePerFrame * IterationCount);
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; j++) {
					for (int i = 0; i < UsagePerFrame; i++) {
						pool.AcquireList();
						pool.AddToNewestList(ReferenceTable.RowIndex.Invalid);
					}
				}
			}, IterationCount);
		}

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_RefTable()
		{
			var leaf = GenerateHierarchy();
			var table = new ReferenceTable(1024);
			table.EnsureDescriptionFor(leaf);
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; j++) {
					for (int i = 0; i < UsagePerFrame; i++) {
						Ensure(table, leaf);
					}
				}
			}, IterationCount);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void Ensure(ReferenceTable table, IProfileableObject leaf)
		{
			table.EnsureDescriptionFor(leaf);
			table.UpdateFrameIndexFor(leaf.RowIndex, 0);
		}

		[ThreadStatic]
		private static int threadStaticValue;

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_ThreadStatic()
		{
			var arr = new int[UsagePerFrame];
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; j++) {
					for (int i = 0; i < UsagePerFrame; i++) {
						arr[i] = ReadThreadStatic();
					}
					threadStaticValue %= 100;
				}
			}, IterationCount);
			Console.WriteLine(arr.Average());
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static int ReadThreadStatic() => ++threadStaticValue;

		private static IEnumerable<Type> GetLimeTypes() =>
			AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith("Lime")).First().GetTypes();

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_ConditionalWeakTable()
		{
			var table = new ConditionalWeakTable<Type, TypeId>();
			var types = new Type[1000];
			int counter = 0;
			foreach (var type in GetLimeTypes()) {
				types[counter] = type;
				table.GetOrCreateValue(type).Value = counter;
				if (++counter == 1000) break;
			}
			counter = 0;
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; ++j) {
					for (int i = 0; i < 10; ++i) {
						const int end_k = UsagePerFrame / 10;
						for (int k = 0; k < end_k; ++k) {
							counter += table.GetOrCreateValue(types[k]).Value;
						}
					}
				}
			}, IterationCount);
			Console.WriteLine("Count {0}", counter);
		}

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_ABS()
		{
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; ++j) {
					for (int i = 0; i < UsagePerFrame; ++i) {
						ABS(i);
					}
				}
			}, IterationCount);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static long ABS(long value) => Math.Abs(value);

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_DivisionRemainder_1()
		{
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; ++j) {
					for (uint i = 0; i < UsagePerFrame; ++i) {
						DivisionRemainder_1(i, 1337);
					}
				}
			}, IterationCount);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static uint DivisionRemainder_1(uint value1, uint value2) => value1 % value2;

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_DivisionRemainder_2()
		{
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; ++j) {
					for (uint i = 0; i < UsagePerFrame; ++i) {
						DivisionRemainder_2(i, 0x8000_000u);
					}
				}
			}, IterationCount);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static uint DivisionRemainder_2(uint value1, uint value2) => value1 & value2;

		[TestMethod]
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void TestMethod_Dictionary()
		{
			var dictionary = new Dictionary<Type, int>();
			var types = new Type[1000];
			int counter = 0;
			foreach (var type in GetLimeTypes()) {
				types[counter] = type;
				dictionary.Add(type, counter);
				if (++counter == 1000) break;
			}
			PerformanceTest.Run(() => {
				for (int j = 0; j < IterationCount; ++j) {
					for (int i = 0; i < 10; ++i) {
						const int end_k = UsagePerFrame / 10;
						for (int k = 0; k < end_k; ++k) {
							dictionary.TryGetValue(types[k], out var value);
							counter += value;
						}
					}
				}
			}, IterationCount);
			Console.WriteLine(counter);
		}

		private class TypeId
		{
			public int Value;
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
	}
}
