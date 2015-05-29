using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime
{
	/// <summary>
	/// ���������� ��������� ��������� ������
	/// </summary>
	public static class CommandLineArgs
	{
		/// <summary>
		/// ���� ���������� �� ���� �����
		/// </summary>
		public static readonly bool MaximizedWindow = CheckFlag("--Maximized");

		/// <summary>
		/// ������������ OpenGL ������ GLES
		/// </summary>
		public static readonly bool OpenGL = CheckFlag("--OpenGL");

		/// <summary>
		/// ���������� FPS 25 ������� � �������
		/// </summary>
		public static readonly bool Limit25FPS = CheckFlag("--Limit25FPS");

		/// <summary>
		/// ���������� � ������������� ������
		/// </summary>
		public static readonly bool FullscreenMode = CheckFlag("--Fullscreen");

		/// <summary>
		/// ����������� ��������� �������� ������ �� ������
		/// </summary>
		public static readonly bool SimulateSlowExternalStorage = CheckFlag("--SimulateSlowExternalStorage");

		/// <summary>
		/// ��������� ���� ���������
		/// </summary>
		public static readonly bool NoAudio = CheckFlag("--NoAudio");

		/// <summary>
		/// ��������� ������
		/// </summary>
		public static readonly bool NoMusic = CheckFlag("--NoMusic");

		/// <summary>
		/// ����� �������
		/// </summary>
		public static readonly bool Debug = CheckFlag("--Debug");

		/// <summary>
		/// ���������� ��������� ��������� ������
		/// </summary>
		public static string[] Get()
		{
#if UNITY_WEB
			return new string[] {};
#else
			return System.Environment.GetCommandLineArgs();
#endif
		}

		/// <summary>
		/// ���������� true, ���� ���������� ��������� ����
		/// </summary>
		public static bool CheckFlag(string name)
		{
			return Array.IndexOf(Get(), name) >= 0;
		}
	}
}
