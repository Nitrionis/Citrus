using System;
#if iOS
using Foundation;
#endif

namespace Lime
{
	/// <summary>
	/// �����, ��������������� ������� �����������
	/// </summary>
	public static class Localization
	{
		private static bool useNumericKeys;

		/// <summary>
		/// ����������. ������������ ������ ��� ������������� �� ������� ���������.
		/// ������ � ������� � �������� ������ �������������� �����. ������ - ��� ������. ���� ���� �������� ������ �����
		/// </summary>
		public static bool UseNumericKeys { get { return useNumericKeys; } set { useNumericKeys = value; } }

		/// <summary>
		/// ������� ������� �����������
		/// </summary>
		public static LocalizationDictionary Dictionary = new LocalizationDictionary();

		/// <summary>
		/// ���������� ��� ����� ��� ����� �������� �����.
		/// �������� "en" ��� English, "es" ��� Spanish, "de" ��� Deutch � �.�.
		/// ��� ����� ��������� ���������� �� ������ (�������� ������ 639-1)
		/// http://en.wikipedia.org/wiki/List_of_ISO_639-1_codes
		/// </summary>
		public static string GetCurrentLanguage()
		{
#if iOS
			string language = NSLocale.PreferredLanguages[0];
			language = language.Substring(0, 2);
				return language;
#else
			return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
#endif
		}

		/// <summary>
		/// ���������� �������������� ������ �� �������� ������� �� �� �����
		/// </summary>
		public static string GetString(string format, params object[] args)
		{
			string s = GetString(format);
			for (int i = 0; i < args.Length; i++) {
				if (args[i] is string) {
					args[i] = GetString((string)args[i]);
				}
			}
			return string.Format(s, args);
		}

		/// <summary>
		/// ���������� �������������� ������ �� �������� ������� �� �� �����
		/// </summary>
		public static string GetString(string key)
		{
			if (string.IsNullOrEmpty(key)) {
				return key;
			}
			if (useNumericKeys) {
				return GetStringForNumericKey(key);
			} else {
				return GetStringHelper(key);
			}
		}

		/// <summary>
		/// Used to mark a tagged string to be ignored in Orange's DictionaryExtractor
		/// </summary>
		/// <param name="str">A tagged string to be ignored</param>
		public static string Ignore(string str)
		{
			return str;
		}

		private static string GetStringForNumericKey(string taggedString)
		{
			if (taggedString[0] == '[') {
				int closeBrackedPos = 0;
				for (int i = 1; i < taggedString.Length; i++) {
					if (taggedString[i] == ']') {
						closeBrackedPos = i;
						break;
					}
					if (!char.IsDigit(taggedString, i)) {
						break;
					}
				}
				if (closeBrackedPos >= 1) {
					string text;
					if (closeBrackedPos > 1) {
						var key = taggedString.Substring(1, closeBrackedPos - 1);
						if (Dictionary.TryGetText(key, out text)) {
							return text;
						}
					}
					// key/value pair not defined or key is empty ("[]" case).
					text = taggedString.Substring(closeBrackedPos + 1);
					return text;
				}
			}
			return taggedString;
		}

		private static string GetStringHelper(string key)
		{
			if (key == "") {
				return key;
			}
			if (key.Length >= 2 && key[0] == '[' && key[1] == ']') {
				key = key.Substring(2);
			}
			string text;
			if (Dictionary.TryGetText(key, out text)) {
				return text;
			}
			// Leave selector in debug build to help translators identify string from the UI.
#if DEBUG
			return key;
#else
			if (key.Length > 0 && key[0] != '[') {
				return key;
			}
			int index = key.IndexOf(']');
			if (index != -1) {
				return key.Substring(+1);
			}
			return key;
#endif
		}
	}
}
