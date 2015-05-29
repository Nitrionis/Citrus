using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime
{
	public static class StringExtensions
	{
		/// <summary>
		/// �������, ����������� String.Format, �� ��������� � ���� �������-����������
		/// </summary>
		public static string Format(this string format, params object[] args)
		{
			return string.Format(format, args);
		}

		/// <summary>
		/// ���������� ������ ��� �������� ����� (� �������� ����� ��� ������� ������������ ��� ������)
		/// </summary>
		public static string Localize(this string text)
		{
			return Localization.GetString(text);
		}

		/// <summary>
		/// ���������� ������ ��� �������� ����� (� �������� ����� ��� ������� ������������ ��� ������)
		/// </summary>
		public static string Localize(this string format, params object[] args)
		{
			return Localization.GetString(format, args);
		}

		public static bool HasJapaneseSymbols(this string text, int start = 0, int length = -1)
		{
			int end = (length < 0) ? text.Length : Math.Min(text.Length, start + length);
			for (int i = start; i < end; i++) {
				char c = text[i];
				if ((c >= 0x3040 && c <= 0x309f) /* Hiragana */ || (c >= 0x30a0 && c <= 0x30ff) /* Katakana */ || (c >= 0x4e00 && c <= 0x9faf) /* Kanji */)
					return true;
			}
			return false;
		}
	}
}
