#if PROFILER
namespace Lime.Profiler
{
	public struct TypeIdentifier
	{
		public readonly int Value;

		public bool IsEmpty => Value == 0;

		internal TypeIdentifier(int value) => Value = value;

		public static readonly TypeIdentifier Empty = new TypeIdentifier(0);
	}

	/// <summary>
	/// Provides an identifier of type that implements this interface, which is unique within a session.
	/// </summary>
	public interface ITypeIdentifierProvider
	{
		TypeIdentifier Identifier { get; }
	}
}
#endif // PROFILER
