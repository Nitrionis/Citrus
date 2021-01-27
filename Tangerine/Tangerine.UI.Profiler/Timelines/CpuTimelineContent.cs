#if PROFILER

using System;
using System.Collections.Generic;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	
	internal class CpuTimelineContent : TimelineContent
	{
		private readonly AsyncContentBuilder contentBuilder;
		
		public override IEnumerable<Rectangle> Rectangles => contentBuilder.Rectangles;
		
		public override IEnumerable<TimelineHitTest.ItemInfo> HitTestTargets => contentBuilder.HitTestTargets;

		public CpuTimelineContent(PeriodPositions.SpacingParameters spacingParameters)
		{
			contentBuilder = new AsyncContentBuilder();
		}

		protected override IAsyncContentBuilder GetContentBuilder() => contentBuilder;
		
		private class AsyncContentBuilder : IAsyncContentBuilder
		{
			
			
			public IEnumerable<Rectangle> Rectangles => throw new NotImplementedException();
		
			public IEnumerable<TimelineHitTest.ItemInfo> HitTestTargets => throw new NotImplementedException();
			
			public void RebuildAsync(FrameDataResponse frameData)
			{
				throw new NotImplementedException();
			}
		}
	}
}

#endif // PROFILER