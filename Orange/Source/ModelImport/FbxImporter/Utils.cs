﻿using System;
using System.Runtime.InteropServices;


namespace Orange.FbxImporter
{
	public static class ImportConfig
	{
		public const string LibName = "FbxSdk.dll";
	}

	public static class IntPtrExtensions
	{
		public static T To<T>(this IntPtr ptr) {
			if(ptr != IntPtr.Zero) {
				var structure = (T)Marshal.PtrToStructure(ptr, typeof(T));
				Utils.ReleaseNative(ptr);
				return structure;
			}
			return default(T);
		}
		public static T ToStruct<T>(this IntPtr ptr) where T: class
		{
			if (ptr != IntPtr.Zero) {
				var structure = (T)Marshal.PtrToStructure(ptr, typeof(T));
				Utils.ReleaseNative(ptr);
				return structure;
			}
			return null;
		}

		public static int[] ToIntArray(this IntPtr ptr, int size)
		{
			if (size == 0)
				return null;
			int[] result = new int[size];
			Marshal.Copy(ptr, result, 0 ,size);
			Utils.ReleaseNative(ptr);
			return result;
		}


		public static float[] ToFloatArray(this IntPtr ptr, int size)
		{
			if (size == 0)
				return null;
			float[] result = new float[size];
			Marshal.Copy(ptr, result, 0, size);
			Utils.ReleaseNative(ptr);
			return result;
		}

		public static double[] ToDoubleArray(this IntPtr ptr, int size)
		{
			if (size == 0)
				return null;
			double[] result = new double[size];
			Marshal.Copy(ptr, result, 0, size);
			Utils.ReleaseNative(ptr);
			return result;
		}

		public static T[] FromArrayOfPointersToStructArrayUnsafe<T>(this IntPtr ptr, int size)
		{
			if (ptr != IntPtr.Zero) {
				T[] result = new T[size];
				var strucSize = Marshal.SizeOf(typeof(T));
				for (int i = 0; i < size; i++) {
					var pointer = new IntPtr(ptr.ToInt64() + sizeof(long) * i);
					var structPtr = Marshal.ReadIntPtr(pointer);
					if(structPtr == IntPtr.Zero) {
						result[i] = default(T);
					} else {
						result[i] = (T)Marshal.PtrToStructure(structPtr, typeof(T));
						//Can cause crash if array contains simillar pointers
						Utils.ReleaseNative(structPtr);
					}
				}
				Utils.ReleaseNative(ptr);
				return result;
			}
			return null;
		}

		public static T[] ToStructArray<T>(this IntPtr ptr, int size)
		{
			if (ptr != IntPtr.Zero) {
				T[] result = new T[size];
				var strucSize = Marshal.SizeOf(typeof(T));
				for (int i = 0; i < size; i++) {
					var structPtr = new IntPtr(ptr.ToInt64() + strucSize * i);
					result[i] = (T)Marshal.PtrToStructure(structPtr, typeof(T));
				}
				Utils.ReleaseNative(ptr);
				return result;
			}
			return null;
		}

		public static string ToCharArray(this IntPtr ptr)
		{
			if (ptr != IntPtr.Zero) {
				return Marshal.PtrToStringAnsi(ptr);
			}
			Utils.ReleaseNative(ptr);
			return null;
		}
	}

	public static class Utils
	{
		[DllImport(ImportConfig.LibName, CallingConvention = CallingConvention.Cdecl)]
		private static extern void FbxUtilsReleaseMemory(IntPtr ptr);

		public static void ReleaseNative(IntPtr ptr) {
			FbxUtilsReleaseMemory(ptr);
		}
	}
}
