﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Yuzu.Deserializer;
using Yuzu.Metadata;
using Yuzu.Util;

namespace Yuzu.Json
{
	public class JsonDeserializer : AbstractReaderDeserializer
	{
		public static JsonDeserializer Instance = new JsonDeserializer();
		public JsonSerializeOptions JsonOptions = new JsonSerializeOptions();

		private char? buf;

		public override void Initialize() { buf = null; }

		private char Next()
		{
			if (!buf.HasValue)
				return Reader.ReadChar();
			var result = buf.Value;
			buf = null;
			return result;
		}

		protected void KillBuf()
		{
			if (buf != null)
				throw Error("Unconsumed character: {0}", buf);
		}

		private void PutBack(char ch)
		{
			if (buf.HasValue)
				throw new YuzuAssert();
			buf = ch;
		}

		private char SkipSpaces()
		{
			char ch = Next();
			while (ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r')
				ch = Reader.ReadChar();
			return ch;
		}

		protected char SkipSpacesCarefully()
		{
			if (buf.HasValue)
				throw new YuzuAssert();
			while (true) {
				var v = Reader.PeekChar();
				if (v < 0)
					return '\0';
				var ch = (char)v;
				if (ch != ' ' && ch != '\t' && ch != '\n' && ch != '\r')
					return ch;
				Reader.ReadChar();
			}
		}

		protected char Require(params char[] chars)
		{
			var ch = SkipSpaces();
			if(Array.IndexOf(chars, ch) < 0)
				throw Error("Expected '{0}' but found '{1}'", String.Join("','", chars), ch);
			return ch;
		}

		protected void Require(string s)
		{
			foreach (var ch in s) {
				var r = Reader.ReadChar();
				if (r != ch)
					throw Error("Expected '{0}', but found '{1}'", ch, r);
			}
		}

		protected string RequireUnescapedString()
		{
			sb.Clear();
			if (RequireOrNull('"')) return null;
			while (true) {
				var ch = Reader.ReadChar();
				if (ch == '"')
					break;
				sb.Append(ch);
			}
			return sb.ToString();
		}

		// Optimization: avoid re-creating StringBuilder.
		private StringBuilder sb = new StringBuilder();

		protected string RequireString()
		{
			sb.Clear();
			if (RequireOrNull('"')) return null;
			while (true) {
				// Optimization: buf is guaranteed to be empty after Require, so no need to call Next.
				var ch = Reader.ReadChar();
				if (ch == '"')
					break;
				if (ch == '\\') {
					ch = Reader.ReadChar();
					if (ch == 'u') {
						int code = 0;
						for (int i = 0; i < 4; ++i) {
							ch = Reader.ReadChar();
							int h = ch <= 'f' ? JsonEscapeData.hexDigits[ch] : -1;
							if (h < 0)
								throw Error("Bad hexadecimal digit in unicode escape: '{0}'", ch);
							code = code * 16 + h;
						}
						ch = (char)code;
					}
					else {
						var escaped = ch <= 't' ? JsonEscapeData.unescapeChars[ch] : '\0';
						if (escaped == 0)
							throw Error("Unexpected escape chararcter: '{0}'", ch);
						ch = escaped;
					}
				}
				sb.Append(ch);
			}
			return sb.ToString();
		}

		protected char RequireChar()
		{
			var s = RequireString();
			if (s.Length != 1)
				throw Error("Expected single char but found: '{0}'", s);
			return s[0];
		}

		protected bool RequireBool()
		{
			var ch = SkipSpaces();
			if (ch == 't') {
				Require("rue");
				return true;
			}
			if (ch == 'f') {
				Require("alse");
				return false;
			}
			throw Error("Expected 'true' or 'false', but found: {0}", ch);
		}

		// Some code duplication within integer parsers to speed up hot path.

		protected uint RequireUInt()
		{
			var ch = SkipSpaces();
			uint result = 0;
			while ('0' <= ch && ch <= '9') {
				var d = (uint)ch - (uint)'0';
				checked { result = result * 10 + d; }
				ch = Reader.ReadChar();
			}
			PutBack(ch);
			return result;
		}

		protected int RequireInt()
		{
			var ch = SkipSpaces();
			int result = 0;
			if (ch == '-') {
				ch = Reader.ReadChar();
				while ('0' <= ch && ch <= '9') {
					var d = (int)'0' - (int)ch;
					checked { result = result * 10 + d; }
					ch = Reader.ReadChar();
				}
			}
			else {
				while ('0' <= ch && ch <= '9') {
					var d = (int)ch - (int)'0';
					checked { result = result * 10 + d; }
					ch = Reader.ReadChar();
				}
			}
			PutBack(ch);
			return result;
		}

		protected ulong RequireULong()
		{
			var ch = SkipSpaces();
			if (JsonOptions.Int64AsString) {
				if (ch != '"')
					throw Error("Expected '\"' but found '{0}'", ch);
				ch = Reader.ReadChar();
			}
			ulong result = 0;
			while ('0' <= ch && ch <= '9') {
				var d = (ulong)ch - (ulong)'0';
				checked { result = result * 10 + d; }
				ch = Reader.ReadChar();
			}
			if (JsonOptions.Int64AsString) {
				if (ch != '"')
					throw Error("Expected '\"' but found '{0}'", ch);
			}
			else
				PutBack(ch);
			return result;
		}

		protected long RequireLong()
		{
			var ch = SkipSpaces();
			if (JsonOptions.Int64AsString) {
				if (ch != '"')
					throw Error("Expected '\"' but found '{0}'", ch);
				ch = Reader.ReadChar();
			}
			int sign = 1;
			if (ch == '-') {
				sign = -1;
				ch = Reader.ReadChar();
			}
			long result = 0;
			while ('0' <= ch && ch <= '9') {
				var d = sign * ((long)ch - (long)'0');
				checked { result = result * 10 + d; }
				ch = Reader.ReadChar();
			}
			if (JsonOptions.Int64AsString) {
				if (ch != '"')
					throw Error("Expected '\"' but found '{0}'", ch);
			}
			else
				PutBack(ch);
			return result;
		}

		private string ParseFloat()
		{
			// Optimization: Do not extract helper methods.
			sb.Clear();
			var ch = SkipSpaces();
			if (ch == '-') {
				sb.Append(ch);
				ch = Reader.ReadChar();
			}
			while ('0' <= ch && ch <= '9') {
				sb.Append(ch);
				ch = Reader.ReadChar();
			}
			if (ch == '.') {
				sb.Append(ch);
				ch = Reader.ReadChar();
				while ('0' <= ch && ch <= '9') {
					sb.Append(ch);
					ch = Reader.ReadChar();
				}
			}
			if (ch == 'e'|| ch == 'E') {
				sb.Append(ch);
				ch = Reader.ReadChar();
				if (ch == '+' || ch == '-') {
					sb.Append(ch);
					ch = Reader.ReadChar();
				}
				while ('0' <= ch && ch <= '9') {
					sb.Append(ch);
					ch = Reader.ReadChar();
				}
			}
			PutBack(ch);
			return sb.ToString();
		}

		protected double RequireDouble()
		{
			return Double.Parse(ParseFloat(), CultureInfo.InvariantCulture);
		}

		protected float RequireSingle()
		{
			return Single.Parse(ParseFloat(), CultureInfo.InvariantCulture);
		}

		protected decimal RequireDecimal()
		{
			return Decimal.Parse(ParseFloat(), CultureInfo.InvariantCulture);
		}

		protected decimal RequireDecimalAsString()
		{
			return Decimal.Parse(RequireUnescapedString(), CultureInfo.InvariantCulture);
		}

		protected DateTime RequireDateTime()
		{
			var s = JsonOptions.DateFormat == "O" ? RequireUnescapedString() : RequireString();
			return DateTime.ParseExact(
				s, JsonOptions.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		}

		protected TimeSpan RequireTimeSpan()
		{
			var s = JsonOptions.TimeSpanFormat == "c" ? RequireUnescapedString() : RequireString();
			return TimeSpan.ParseExact(s, JsonOptions.TimeSpanFormat, CultureInfo.InvariantCulture);
		}

		protected string GetNextName(bool first)
		{
			var ch = SkipSpaces();
			if (ch == ',') {
				if (first)
					throw Error("Expected name, but got ','");
				ch = SkipSpaces();
			}
			PutBack(ch);
			if (ch == '}')
				return "";
			var result = RequireUnescapedString();
			Require(':');
			return result;
		}

		protected bool RequireOrNull(char ch)
		{
			if (Require(ch, 'n') == ch)
				return false;
			Require("ull");
			return true;
		}

		protected char RequireBracketOrNull()
		{
			var ch = Require('{', '[', 'n');
			if (ch == 'n')
				Require("ull");
			return ch;
		}

		protected void ReadIntoCollection<T>(ICollection<T> list)
		{
			// ReadValue might invoke a new serializer, so we must not rely on PutBack.
			if (SkipSpacesCarefully() == ']') {
				Require(']');
				return;
			}
			var rf = ReadValueFunc(typeof(T));
			do {
				list.Add((T)rf());
			} while (Require(']', ',') == ',');
		}

		protected List<T> ReadList<T>()
		{
			if (RequireOrNull('['))
				return null;
			var list = new List<T>();
			ReadIntoCollection(list);
			return list;
		}

		protected static Dictionary<Type, Func<string, object>> keyParsers = new Dictionary<Type, Func<string, object>> {
			{ typeof(int), s => int.Parse(s) },
			{ typeof(string), s => s },
		};

		public static void RegisterKeyParser(Type t, Func<string, object> parser)
		{
			keyParsers.Add(t, parser);
		}

		protected void ReadIntoDictionary<K, V>(Dictionary<K, V> dict)
		{
			// ReadValue might invoke a new serializer, so we must not rely on PutBack.
			if (SkipSpacesCarefully() == '}') {
				Require('}');
				return;
			}
			Func<string, object> rk;
			if (typeof(K).IsEnum)
				rk = s => Enum.Parse(typeof(K), s);
			else if (!keyParsers.TryGetValue(typeof(K), out rk))
				throw new YuzuAssert("Unable to find key parser for type: " + typeof(K).Name);

			var rf = ReadValueFunc(typeof(V));
			do {
				var key = RequireString();
				Require(':');
				dict.Add((K)rk(key), (V)rf());
			} while (Require('}', ',') == ',');
		}

		protected Dictionary<K, V> ReadDictionary<K, V>()
		{
			if (RequireOrNull('{'))
				return null;
			var dict = new Dictionary<K, V>();
			ReadIntoDictionary(dict);
			return dict;
		}

		private T[] ReadArray<T>()
		{
			var lst = ReadList<T>();
			return lst == null ? null : lst.ToArray();
		}

		private T[] ReadArrayWithLengthPrefix<T>()
		{
			if (RequireOrNull('[')) return null;
			// ReadValue might invoke a new serializer, so we must not rely on PutBack.
			if (SkipSpacesCarefully() == ']') {
				Require(']');
				return new T[0];
			}
			var array = new T[RequireUInt()];
			var rf = ReadValueFunc(typeof(T));
			for (int i = 0; i < array.Length; ++i) {
				Require(',');
				array[i] = (T)rf();
			}
			Require(']');
			return array;
		}

		private Action<T> ReadAction<T>() { return GetAction<T>(RequireUnescapedString()); }

		protected object ReadAnyObject() {
			var ch = SkipSpaces();
			PutBack(ch);
			switch (ch) {
				case '\"':
					return RequireString();
				case 't': case 'f':
					return RequireBool();
				case 'n':
					Next();
					Require("ull");
					return null;
				case '{':
					Next();
					var name = GetNextName(first: true);
					if (name != JsonOptions.ClassTag) {
						var any = new Dictionary<string, object>();
						if (name != "") {
							var val = ReadAnyObject();
							any.Add(name, val);
							if (Require(',', '}') == ',')
								ReadIntoDictionary<string, object>(any);
						}
						return any;
					}
					var typeName = RequireUnescapedString();
					return ReadFields(Activator.CreateInstance(FindType(typeName)), GetNextName(first: false));
				case '[':
					return ReadList<object>();
				default:
					return RequireDouble();
			}
		}

		// Optimization: Avoid creating trivial closures.
		private object RequireIntObj() { return RequireInt(); }
		private object RequireUIntObj() { return RequireUInt(); }
		private object RequireLongObj() { return RequireLong(); }
		private object RequireULongObj() { return RequireULong(); }
		private object RequireShortObj() { return checked((short)RequireInt()); }
		private object RequireUShortObj() { return checked((ushort)RequireUInt()); }
		private object RequireSByteObj() { return checked((sbyte)RequireInt()); }
		private object RequireByteObj() { return checked((byte)RequireInt()); }
		private object RequireCharObj() { return RequireChar(); }
		private object RequireStringObj() { return RequireString(); }
		private object RequireBoolObj() { return RequireBool(); }
		private object RequireSingleObj() { return RequireSingle(); }
		private object RequireDoubleObj() { return RequireDouble(); }
		private object RequireDecimalObj() { return RequireDecimal(); }
		private object RequireDecimalAsStringObj() { return RequireDecimalAsString(); }
		private object RequireDateTimeObj() { return RequireDateTime(); }
		private object RequireTimeSpanObj() { return RequireTimeSpan(); }

		private Dictionary<Type, Func<object>> readerCache = new Dictionary<Type, Func<object>>();
		private Dictionary<Type, Action<object>> mergerCache = new Dictionary<Type, Action<object>>();
		private int jsonOptionsGeneration = 0;

		private Func<object> ReadValueFunc(Type t)
		{
			if (jsonOptionsGeneration != JsonOptions.Generation) {
				readerCache.Clear();
				jsonOptionsGeneration = JsonOptions.Generation;
			}
			Func<object> f;
			if (readerCache.TryGetValue(t, out f))
				return f;
			return readerCache[t] = MakeReaderFunc(t);
		}

		private Action<object> MergeValueFunc(Type t)
		{
			if (jsonOptionsGeneration != JsonOptions.Generation) {
				mergerCache.Clear();
				jsonOptionsGeneration = JsonOptions.Generation;
			}
			Action<object> f;
			if (mergerCache.TryGetValue(t, out f))
				return f;
			return mergerCache[t] = MakeMergerFunc(t);
		}

		private Func<object> MakeReaderFunc(Type t)
		{
			if (t == typeof(int))
				return RequireIntObj;
			if (t == typeof(uint))
				return RequireUIntObj;
			if (t == typeof(long))
				return RequireLongObj;
			if (t == typeof(ulong))
				return RequireULongObj;
			if (t == typeof(short))
				return RequireShortObj;
			if (t == typeof(ushort))
				return RequireUShortObj;
			if (t == typeof(sbyte))
				return RequireSByteObj;
			if (t == typeof(byte))
				return RequireByteObj;
			if (t == typeof(char))
				return RequireCharObj;
			if (t == typeof(string))
				return RequireStringObj;
			if (t == typeof(bool))
				return RequireBoolObj;
			if (t == typeof(float))
				return RequireSingleObj;
			if (t == typeof(double))
				return RequireDoubleObj;
			if (t == typeof(decimal)) {
				if (JsonOptions.DecimalAsString)
					return RequireDecimalAsStringObj;
				else
					return RequireDecimalObj;
			}
			if (t == typeof(DateTime))
				return RequireDateTimeObj;
			if (t == typeof(TimeSpan))
				return RequireTimeSpanObj;
			if (t.IsEnum) {
				if (JsonOptions.EnumAsString)
					return () => Enum.Parse(t, RequireString());
				else
					return () => Enum.ToObject(t, RequireInt());
			}
			if (t.IsGenericType) {
				var g = t.GetGenericTypeDefinition();
				if (g == typeof(List<>)) {
					var m = Utils.GetPrivateCovariantGeneric(GetType(), "ReadList", t);
					return () => m.Invoke(this, Utils.ZeroObjects);
				}
				if (g == typeof(Dictionary<,>)) {
					var m = Utils.GetPrivateCovariantGenericAll(GetType(), "ReadDictionary", t);
					return () => m.Invoke(this, Utils.ZeroObjects);
				}
				if (g == typeof(Action<>)) {
					var p = t.GetGenericArguments();
					var m = Utils.GetPrivateCovariantGeneric(GetType(), "ReadAction", t);
					return () => m.Invoke(this, Utils.ZeroObjects);
				}
			}
			if (t.IsArray) {
				var n = JsonOptions.ArrayLengthPrefix ? "ReadArrayWithLengthPrefix" : "ReadArray";
				var m = Utils.GetPrivateCovariantGeneric(GetType(), n, t);
				return () => m.Invoke(this, new object[] { });
			}
			var icoll = t.GetInterface(typeof(ICollection<>).Name);
			if (icoll != null) {
				var m = Utils.GetPrivateCovariantGeneric(GetType(), "ReadIntoCollection", icoll);
				return () => {
					if (RequireOrNull('['))
						return null;
					var list = Activator.CreateInstance(t);
					m.Invoke(this, new object[] { list });
					return list;
				};
			}
			if (t == typeof(object))
				return ReadAnyObject;
			if (t.IsClass && !t.IsAbstract) {
				var m = Utils.GetPrivateGeneric(GetType(), "ReadObject", t);
				return (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), this, m);
			}
			if (t.IsInterface || t.IsAbstract) {
				var m = Utils.GetPrivateGeneric(GetType(), "ReadInterface", t);
				return (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), this, m);
			}
			if (Utils.IsStruct(t))
				return () => FromReaderInt(Activator.CreateInstance(t));
			throw new NotImplementedException(t.Name);
		}

		private Action<object> MakeMergerFunc(Type t)
		{
			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
				var m = Utils.GetPrivateCovariantGenericAll(GetType(), "ReadIntoDictionary", t);
				return obj => { Require('{'); m.Invoke(this, new object[] { obj }); };
			}
			var icoll = t.GetInterface(typeof(ICollection<>).Name);
			if (icoll != null) {
				var m = Utils.GetPrivateCovariantGeneric(GetType(), "ReadIntoCollection", icoll);
				return obj => { Require('['); m.Invoke(this, new object[] { obj }); };
			}
			if ((t.IsClass || t.IsInterface) && t != typeof(object)) {
				var m = Utils.GetPrivateGeneric(GetType(), "ReadIntoObject", t);
				return (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), this, m);
			}
			throw Error("Unable to merge field of type {0}", t.Name);
		}

		protected void IgnoreNewFieldsTail(string name)
		{
			while (name != "") {
				ReadAnyObject();
				name = GetNextName(false);
			}
		}

		protected int IgnoreNewFields(string tag, ref string name)
		{
			var cmp = String.CompareOrdinal(tag, name);
			if (Options.IgnoreUnknownFields && Options.TagMode != TagMode.Names)
				while (cmp > 0 && name != "") {
					ReadAnyObject();
					name = GetNextName(false);
					cmp = String.CompareOrdinal(tag, name);
				}
			return cmp;
		}

		protected virtual object ReadFields(object obj, string name)
		{
			var meta = Meta.Get(obj.GetType(), Options);
			objStack.Push(obj);
			try {
				// Optimization: duplicate loop to extract options check.
				if (JsonOptions.Unordered) {
					while (name != "") {
						Meta.Item yi;
						if (!meta.TagToItem.TryGetValue(name, out yi)) {
							if (!Options.IgnoreUnknownFields)
								throw Error("Unknown field '{0}'", name);
							ReadAnyObject();
							name = GetNextName(false);
							continue;
						}
						if (yi.SetValue != null)
							yi.SetValue(obj, ReadValueFunc(yi.Type)());
						else
							MergeValueFunc(yi.Type)(yi.GetValue(obj));
						name = GetNextName(false);
					}
				}
				else if (Options.IgnoreUnknownFields && Options.TagMode != TagMode.Names) {
					foreach (var yi in meta.Items) {
						if (IgnoreNewFields(yi.Tag(Options), ref name) != 0) {
							if (!yi.IsOptional)
								throw Error("Expected field '{0}', but found '{1}'", yi.NameTagged(Options), name);
							continue;
						}
						if (yi.SetValue != null)
							yi.SetValue(obj, ReadValueFunc(yi.Type)());
						else
							MergeValueFunc(yi.Type)(yi.GetValue(obj));
						name = GetNextName(false);
					}
				}
				else {
					foreach (var yi in meta.Items) {
						if (yi.Tag(Options) != name) {
							if (!yi.IsOptional)
								throw Error("Expected field '{0}', but found '{1}'", yi.NameTagged(Options), name);
							continue;
						}
						if (yi.SetValue != null)
							yi.SetValue(obj, ReadValueFunc(yi.Type)());
						else
							MergeValueFunc(yi.Type)(yi.GetValue(obj));
						name = GetNextName(false);
					}
				}
			}
			finally {
				objStack.Pop();
			}
			if (Options.IgnoreUnknownFields)
				IgnoreNewFieldsTail(name);
			Require('}');
			meta.RunAfterDeserialization(obj);
			return obj;
		}

		protected virtual object ReadFieldsCompact(object obj)
		{
			var meta = Meta.Get(obj.GetType(), Options);
			if (!meta.IsCompact)
				throw Error("Attempt to read non-compact type '{0}' from compact format", obj.GetType().Name);
			bool isFirst = true;
			objStack.Push(obj);
			try {
				foreach (var yi in meta.Items) {
					if (!isFirst)
						Require(',');
					isFirst = false;
					yi.SetValue(obj, ReadValueFunc(yi.Type)());
				}
			}
			finally {
				objStack.Pop();
			}
			Require(']');
			meta.RunAfterDeserialization(obj);
			return obj;
		}

		protected void CheckClassTag(string name)
		{
			if (name != JsonOptions.ClassTag)
				throw Error("Expected class tag, but found '{0}'", name);
		}

		// T is neither a collection nor a bare object.
		private T ReadObject<T>() where T: class, new() {
			KillBuf();
			switch (RequireBracketOrNull()) {
				case 'n':
					return null;
				case '{':
					var name = GetNextName(first: true);
					if (name != JsonOptions.ClassTag)
						return (T)ReadFields(new T(), name);
					var typeName = RequireUnescapedString();
					var t = FindType(typeName);
					if (!typeof(T).IsAssignableFrom(t))
						throw Error("Expected type '{0}', but got '{1}'", typeof(T).Name, typeName);
					return (T)ReadFields(Activator.CreateInstance(t), GetNextName(first: false));
				case '[':
					return (T)ReadFieldsCompact(new T());
				default:
					throw new YuzuAssert();
			}
		}

		// T is neither a collection nor a bare object.
		private void ReadIntoObject<T>(object obj) where T : class, new()
		{
			KillBuf();
			switch (Require('{', '[')) {
				case '{':
					var name = GetNextName(first: true);
					if (name != JsonOptions.ClassTag) {
						ReadFields(obj, name);
					}
					else {
						CheckExpectedType(RequireUnescapedString(), typeof(T));
						ReadFields(obj, GetNextName(first: false));
					}
					return;
				case '[':
					ReadFieldsCompact(obj);
					return;
				default:
					throw new YuzuAssert();
			}
		}

		private T ReadInterface<T>() where T : class
		{
			KillBuf();
			if (RequireOrNull('{')) return null;
			CheckClassTag(GetNextName(first: true));
			var typeName = RequireUnescapedString();
			var t = FindType(typeName);
			if (!typeof(T).IsAssignableFrom(t))
				throw Error("Expected interface '{0}', but got '{1}'", typeof(T).Name, typeName);
			return (T)ReadFields(Activator.CreateInstance(t), GetNextName(first: false));
		}

		public override object FromReaderInt() { return ReadAnyObject(); }

		public override object FromReaderInt(object obj)
		{
			KillBuf();
			var expectedType = obj.GetType();
			if (expectedType == typeof(object))
				throw Error("Unable to read into bare object");
			switch (RequireBracketOrNull()) {
				case 'n':
					return null;
				case '{':
					if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
						var m = Utils.GetPrivateCovariantGenericAll(GetType(), "ReadIntoDictionary", expectedType);
						m.Invoke(this, new object[] { obj });
						return obj;
					}
					var name = GetNextName(first: true);
					if (name != JsonOptions.ClassTag)
						return ReadFields(obj, name);
					CheckExpectedType(RequireUnescapedString(), expectedType);
					return ReadFields(obj, GetNextName(first: false));
				case '[':
					var icoll = expectedType.GetInterface(typeof(ICollection<>).Name);
					if (icoll != null) {
						var m = Utils.GetPrivateCovariantGeneric(GetType(), "ReadIntoCollection", icoll);
						m.Invoke(this, new object[] { obj });
						return obj;
					}
					return ReadFieldsCompact(obj);
				default:
					throw new YuzuAssert();
			}
		}

		public override T FromReaderInt<T>() { return (T)ReadValueFunc(typeof(T))(); }
	}
}
