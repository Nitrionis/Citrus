using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime
{
	/// <summary>
	/// ������������� ���������� ������
	/// </summary>
	public static class Debug
	{
		/// <summary>
		/// ���������� ���������� ���������, ����� ����� ������ ������ (������ ���� Button) (���������� �����������)
		/// </summary>
		public static bool BreakOnButtonClick { get; set; }
		
		/// <summary>
		/// ������� ��������� � ���
		/// </summary>
		public static void Write(string message)
		{
			Logger.Write(message);
		}

		/// <summary>
		/// ������� ��������� � ���
		/// </summary>
		/// <param name="value">����� ������������� � ������</param>
		public static void Write(object value)
		{
			Write(value.ToString());
		}

		/// <summary>
		/// ������� ��������� � ���
		/// </summary>
		public static void Write(string msg, params object[] args)
		{
			Logger.Write(msg, args);
		}
	}
}
