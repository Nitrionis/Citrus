#if PROFILER
namespace Lime.Profiler
{
	public interface IProfileableObject
	{
		string Name { get; }
		bool IsPartOfScene { get; }
		bool IsOverdrawForeground { get; }
		IProfileableObject Parent { get; }
		ReferenceTable.RowIndex RowIndex { get; set; }
	}

	public static class ProfileableObjectExtension
	{
		public static long LastProfiledFrameIdentifier;

		public static void UpdateRowIndex(this IProfileableObject @object)
		{
			if (@object.RowIndex.IsValid) {
				
			} else {
				
			}
			throw new System.NotImplementedException();
		}
	}
}
#endif // PROFILER
