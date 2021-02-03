#if PROFILER

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lime;
using Lime.Profiler;
using Tangerine.UI.Charts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	using SpacingParameters = PeriodPositions.SpacingParameters;
	
	internal class Timeline<TUsage, TLabel> : TimelineWidget 
		where TUsage : struct
		where TLabel : struct, ITimelineItemLabel
	{
		private readonly TimelineFramePreloader preloader;
		private readonly TimelineContent<TUsage, TLabel> timelineContent;
		private readonly TimelineLabels<TLabel> timelineLabels;
		private readonly TimelineMesh timelineMesh;
		private readonly TimelineHitTest timelineHitTest;

		protected readonly Queue<(Task, TimelineState)> contentModificationTasks;
		protected readonly Queue<Task> contentReadingTasks;

		private TimelineState timelineState;
		private long activeFrameIdentifier;
		
		protected Timeline(
			TimelineContent<TUsage, TLabel> timelineContent, 
			TimelineLabels<TLabel> timelineLabels)
		{
			contentModificationTasks = new Queue<(Task, TimelineState)>();
			contentReadingTasks = new Queue<Task>();
			preloader = new TimelineFramePreloader();
			this.timelineContent = timelineContent;
			this.timelineLabels = timelineLabels;
			timelineMesh = new TimelineMesh();
			timelineHitTest = new TimelineHitTest();
			
			contentContainer.Presenter = new TimelinePresenter();
			Updated += (delta) => {
				TimePeriod CalculateVisibleTimePeriod() {
					float scrollPosition = horizontalScrollView.ScrollPosition;
					return new TimePeriod {
						StartTime = Math.Max(0, scrollPosition - ruler.SmallStepSize) * MicrosecondsPerPixel,
						FinishTime = (scrollPosition + horizontalScrollView.Width) * MicrosecondsPerPixel
					};
				}
				if (preloader.IsAttemptCompleted && activeFrameIdentifier != preloader.Frame.Identifier) {
					activeFrameIdentifier = preloader.Frame.Identifier;
					// todo need to create timelineContent.RebuildAsync(activeFrameIdentifier)
					timelineContent.RebuildAsync(activeFrameIdentifier, , );
				}
				var visibleTimePeriod = CalculateVisibleTimePeriod();
				timelineState = new TimelineState {
					VisibleTimePeriod = visibleTimePeriod,
					MicrosecondsPerPixel = ,
					RelativeScale = ,
					TimeIntervalHeight = ItemHeight,
					TimeIntervalVerticalMargin = ItemMargin
				};
				contentContainer.Width = ;
			};
		}

		public void SetFrame(long frameIdentifier) => preloader.Load(frameIdentifier);

		private void SetColors(Color4[] colors)
		{
			var vectors = ((TimelinePresenter)contentContainer.Presenter).RectangleMaterial.Colors;
			for (int i = 0; i < colors.Length; i++) {
				vectors[i] = colors[i].ToVector4();
			}
		}
		
		private class TimelinePresenter : IPresenter
		{
			public readonly ChartsMaterial RectangleMaterial = new ChartsMaterial();

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var timeline = (Timeline<TUsage, TLabel>)node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(node.AsWidget);
				ro.RectangleMaterial = RectangleMaterial;
				var timelineState = timeline.timelineState;
				ro.RelativeScale = timelineState.RelativeScale;
				ro.MeshRenderObject = timeline.timelineMesh.GetRenderObject();
				ro.LabelsRenderObject = timeline.timelineLabels.GetRenderObject(
					new TimePeriod {
						StartTime = timelineState.VisibleTimePeriod.StartTime * timelineState.RelativeScale,
						FinishTime = timelineState.VisibleTimePeriod.FinishTime * timelineState.RelativeScale
					},
					1f / timelineState.MicrosecondsPerPixel * timelineState.RelativeScale);
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public float RelativeScale;
				public ChartsMaterial RectangleMaterial;
				public TimelineMesh.RenderObject MeshRenderObject;
				public TimelineLabels<TLabel>.RenderObject LabelsRenderObject;

				public override void Render()
				{
					Renderer.Flush();
					PrepareRenderState();
					RectangleMaterial.Matrix =  
					    Renderer.FixupWVP(
						    Matrix44.CreateScale(RelativeScale, 1f, 1f) *
						    (Matrix44)LocalToWorldTransform *
						    Renderer.ViewProjection);
					RectangleMaterial.Apply(0);
					MeshRenderObject.Render();
					LabelsRenderObject.Render();
				}
			}
		}
	}
}

#endif // PROFILER