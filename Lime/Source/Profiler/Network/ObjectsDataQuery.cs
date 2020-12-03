#if PROFILER

using System;
using System.IO;
using Yuzu;

namespace Lime.Profiler.Network
{
	/// <summary>
	/// Total time spent on processing objects that satisfy the condition for each frame.
	/// </summary>
	internal class ObjectsDataQuery : IDataSelectionCommand
	{
		[YuzuMember]
		public float[] RenderTimeForEachFrame;
		[YuzuMember]
		public float[] UpdateTimeForEachFrame;
		[YuzuMember]
		public float[] DrawTimeForEachFrame;

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database, BinaryWriter writer)
		{
			
			throw new NotImplementedException();
		}
	}
}

#endif // PROFILER
