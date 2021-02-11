#if PROFILER

using Lime;
using System;

namespace Tangerine.UI.Timelines
{
	internal class TimelineRuler : Widget
	{
		/// <summary>
		/// Use to scale.
		/// </summary>
		public float MicrosecondsPerPixel = 1.0f;

		/// <summary>
		/// Pixel spacing for small marks for <see cref="MicrosecondsPerPixel"/> = 1.0
		/// </summary>
		public float SmallStepSize;

		/// <summary>
		/// Count of small steps in a big step.
		/// </summary>
		public int SmallStepsPerBig;

		/// <summary>
		/// Defines the initial values of the timeline.
		/// </summary>
		public float StartTime;

		/// <summary>
		/// Timestamps labels color.
		/// </summary>
		public Color4 TextColor = Color4.White;

		/// <summary>
		/// Timestamps color.
		/// </summary>
		public Color4 TimestampsColor = Color4.White;

		public TimelineRuler(float smallStepSize, int smallStepsPerBig)
		{
			SmallStepSize = smallStepSize;
			SmallStepsPerBig = smallStepsPerBig;
			Presenter = new RulerPresenter(this);
		}

		private class RulerPresenter : IPresenter
		{
			private readonly TimelineRuler timelineRuler;

			public RulerPresenter(TimelineRuler timelineRuler) => this.timelineRuler = timelineRuler;

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(timelineRuler);
				ro.ContainerWidth = node.AsWidget.Width;
				ro.MicrosecondsPerPixel = timelineRuler.MicrosecondsPerPixel;
				ro.SmallStepSize = timelineRuler.SmallStepSize;
				ro.SmallStepsPerBig = timelineRuler.SmallStepsPerBig;
				ro.StartTime = timelineRuler.StartTime;
				ro.TextColor = timelineRuler.TextColor;
				ro.TimestampsColor = timelineRuler.TimestampsColor;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public float ContainerWidth;
				public float MicrosecondsPerPixel;
				public float SmallStepSize;
				public float SmallStepsPerBig;
				public float StartTime;
				public Color4 TextColor;
				public Color4 TimestampsColor;

				public override void Render()
				{
					PrepareRenderState();
					var offsetSmall = new Vector2(1, -6);
					var offsetBig = new Vector2(1, -16);
					var offsetText = new Vector2(4, -24);
					float scaledSmallStep = SmallStepSize * MicrosecondsPerPixel;
					int stepIndex = (StartTime / scaledSmallStep).Truncate();
					Debug.Write($"{StartTime / scaledSmallStep:0.000000}");
					float value = (float)Math.Round(-StartTime / MicrosecondsPerPixel, 2);
					var startPosition = new Vector2(value % SmallStepSize, 26);
					for (; startPosition.X < ContainerWidth; startPosition.X += SmallStepSize, stepIndex++) {
						Vector2 endPosition;
						if (stepIndex % SmallStepsPerBig == 0) {
							endPosition = startPosition + offsetBig;
							var time = $"{stepIndex * scaledSmallStep / 1000:0.0#}";
							Renderer.DrawTextLine(startPosition + offsetText, time, 16, TextColor, 0);
						} else {
							endPosition = startPosition + offsetSmall;
						}
						Renderer.DrawRect(startPosition, endPosition, TimestampsColor);
					}
				}
			}
		}
	}
}

#endif // PROFILER
