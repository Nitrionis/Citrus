#if PROFILER
namespace Lime.Profiler
{
	/// <remarks>
	/// Since we store a hierarchy of objects for each object that the profiler touched, the implementation of
	/// this interface should call <see cref="ReferenceTable.ObjectDetachedFromMainHierarchy(object)"/> method,
	/// when object detached from the main hierarchy.
	/// </remarks>
	public interface IProfileableObject
	{
		string Name { get; }
		bool IsPartOfScene { get; }
		bool IsOverdrawForeground { get; }
		IProfileableObject Parent { get; }
		ReferenceTable.RowIndex RowIndex { get; set; }
	}
}
#endif // PROFILER
