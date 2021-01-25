#if PROFILER

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lime;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	
	internal class TimelineContent
	{
		
		
		public async Task RebuildAsync(long frameIndex, Task waitingTask)
		{
			await waitingTask;
			throw new NotImplementedException();
		}
	}
}

#endif // PROFILER