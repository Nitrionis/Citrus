using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lime
{
	/// <summary>
	/// ������ ������� �����������
	/// </summary>
	public class LocalizationEntry
	{
		/// <summary>
		/// ����� ��������
		/// </summary>
		public string Text;

		/// <summary>
		/// ��������. ������ ����������� ��� �����������
		/// </summary>
		public string Context;
	}

	/// <summary>
	/// ��������� ������������, ���������������� ������� ������ � ������ ������� � ����
	/// </summary>
	public interface ILocalizationDictionarySerializer
	{
		string GetFileExtension();
		void Read(LocalizationDictionary dictionary, Stream stream);
		void Write(LocalizationDictionary dictionary, Stream stream);
	}

	/// <summary>
	/// ������� �����������. ������������ ��� �������� ������ �� ������ ����.
	/// �������� ���� ����-��������. ������, �������� � HotStudio �������� ������,
	/// ���� ���������� � ���������� ������ []. ������� ��������� �� �� ����� ��� ����������� �����
	/// </summary>
	public class LocalizationDictionary : Dictionary<string, LocalizationEntry>
	{
		/// <summary>
		/// ������� ����������� ������������. ����� ����� �������� � ������� ������ ����������� � ���������� ����
		/// </summary>
		private int commentsCounter;

		/// <summary>
		/// ������� ����� ��� ������������
		/// </summary>
		private const string commentKeyPrefix = "_COMMENT";

		/// <summary>
		/// �������� �������� �� �����
		/// </summary>
		public LocalizationEntry GetEntry(string key)
		{
			LocalizationEntry e;
			if (TryGetValue(key, out e)) {
				return e;
			} else {
				e = new LocalizationEntry();
				Add(key, e);
				return e;
			}
		}

		/// <summary>
		/// ��������� ����� ������ � �������. ���� ������ � ����� ������ ��� ����, �������� ��
		/// </summary>
		/// <param name="key">����, �� �������� ����� ����� �������� ������</param>
		/// <param name="text">�����</param>
		/// <param name="context">��������. ������ ����������� ��� �����������</param>
		public void Add(string key, string text, string context)
		{
			var e = GetEntry(key);
			e.Text = text;
			e.Context = context;
		}

		/// <summary>
		/// ��������� �� �������� �� ���� ����������� ������ ��� ������������
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public static bool IsComment(string key)
		{
			return key.StartsWith(commentKeyPrefix);
		}

		/// <summary>
		/// ��������� � ������� ������ �����������
		/// </summary>
		/// <param name="comment">����� �����������</param>
		public void AddComment(string comment)
		{
			var e = GetEntry(commentKeyPrefix + commentsCounter.ToString());
			e.Context = comment;

			commentsCounter += 1;
		}

		/// <summary>
		/// �������� ����� �������� �� �����. ���������� true � ������ �������� ��������
		/// </summary>
		/// <param name="key">����</param>
		/// <param name="value">����������, � ������� ����� ������� ���������</param>
		public bool TryGetText(string key, out string value)
		{
			value = null;
			LocalizationEntry e;
			if (TryGetValue(key, out e)) {
				value = e.Text;
			}
			return value != null;
		}

		/// <summary>
		/// ��������� ������� �� ������
		/// </summary>
		public void ReadFromStream(Stream stream)
		{
			new LocalizationDictionaryTextSerializer().Read(this, stream);
		}

		/// <summary>
		/// ���������� ������� � �����
		/// </summary>
		public void WriteToStream(Stream stream)
		{
			new LocalizationDictionaryTextSerializer().Write(this, stream);
		}

		/// <summary>
		/// ��������� ������� �� ������
		/// </summary>
		/// <param name="serializer">�����������, ��������������� ������� ������ � ������ ������� � ����</param>
		public void ReadFromStream(ILocalizationDictionarySerializer serializer, Stream stream)
		{
			serializer.Read(this, stream);
		}

		/// <summary>
		/// ���������� ������� � �����
		/// </summary>
		/// <param name="serializer">�����������, ��������������� ������� ������ � ������ ������� � ����</param>
		public void WriteToStream(ILocalizationDictionarySerializer serializer, Stream stream)
		{
			serializer.Write(this, stream);
		}
	}
}
