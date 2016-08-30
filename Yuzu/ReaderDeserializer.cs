﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Yuzu.Metadata;
using Yuzu.Util;

namespace Yuzu.Deserializer
{
	public abstract class AbstractReaderDeserializer : AbstractDeserializer
	{
		public BinaryReader Reader;

		public virtual void Initialize() { }
		public abstract object FromReaderInt();
		public abstract object FromReaderInt(object obj);
		public abstract T FromReaderInt<T>();

		public override object FromReader(object obj, BinaryReader reader)
		{
			Reader = reader;
			Initialize();
			return FromReaderInt(obj);
		}

		public override object FromString(object obj, string source)
		{
			return FromReader(obj, new BinaryReader(new MemoryStream(Encoding.UTF8.GetBytes(source), false)));
		}

		public override object FromStream(object obj, Stream source)
		{
			return FromReader(obj, new BinaryReader(source));
		}

		public override object FromBytes(object obj, byte[] bytes)
		{
			return FromStream(obj, new MemoryStream(bytes, false));
		}

		public override object FromReader(BinaryReader reader)
		{
			Reader = reader;
			Initialize();
			return FromReaderInt();
		}

		public override object FromString(string source)
		{
			return FromReader(new BinaryReader(new MemoryStream(Encoding.UTF8.GetBytes(source), false)));
		}

		public override object FromStream(Stream source)
		{
			return FromReader(new BinaryReader(source));
		}

		public override object FromBytes(byte[] bytes)
		{
			return FromStream(new MemoryStream(bytes, false));
		}

		public override T FromReader<T>(BinaryReader reader)
		{
			Reader = reader;
			Initialize();
			return FromReaderInt<T>();
		}

		public override T FromString<T>(string source)
		{
			return FromReader<T>(new BinaryReader(new MemoryStream(Encoding.UTF8.GetBytes(source), false)));
		}

		public override T FromStream<T>(Stream source)
		{
			return FromReader<T>(new BinaryReader(source));
		}

		public override T FromBytes<T>(byte[] bytes)
		{
			return FromStream<T>(new MemoryStream(bytes, false));
		}

		protected YuzuException Error(string message, params object[] args)
		{
			return new YuzuException(
				String.Format(message, args),
				Options.ReportErrorPosition ? new YuzuPosition(Reader.BaseStream.Position) : null);
		}

		protected Type FindType(string typeName)
		{
			var t = TypeSerializer.Deserialize(typeName);
			if (t == null)
				throw Error("Unknown type '{0}'", typeName);
			return t;
		}

		protected void CheckExpectedType(string typeName, Type expectedType)
		{
			if (FindType(typeName) != expectedType)
				throw Error("Expected type '{0}', but got '{1}'", expectedType.Name, typeName);
		}

		protected Stack<object> objStack = new Stack<object>();

		protected Action<T> GetAction<T>(string name)
		{
			if (String.IsNullOrEmpty(name))
				return null;
			var obj = objStack.Peek();
			var m = obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
			if (m == null)
				throw Error("Unknown action '{0}'", name);
			return (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), obj, m);
		}
	}
}
