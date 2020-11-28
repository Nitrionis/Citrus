#if PROFILER

using System.IO;

namespace Lime.Profiler.Formatting
{
	/// <summary>
	/// Interrupts the serialization or deserialization process.
	/// </summary>
	/// <remarks>
	/// The capabilities of this interface are useful when a piece of data is
	/// represented in the Yuzu binary format, and part - to some others.
	/// In general, these methods are asynchronous.
	/// </remarks>
	public interface IInterrupter
	{
		/// <summary>
		/// Will be called immediately after the object is serialized.
		/// </summary>
		/// <param name="writer">BinaryWriter used by serializer.</param>
		void AfterSerialization(BinaryWriter writer, object @object);

		/// <summary>
		/// Will be called immediately after the object is deserialized.
		/// </summary>
		/// <param name="reader">BinaryReader used by deserializer.</param>
		void AfterDeserialization(BinaryReader reader, object @object);
	}
}

#endif // PROFILER
