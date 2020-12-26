#if PROFILER

using Lime;

namespace Tangerine.UI.Timelines
{
	internal class TimelineRuler : Widget
	{
		/// <summary>
		/// Use to scale.
		/// </summary>
		public float RulerScale = 1.0f;

		/// <summary>
		/// Pixel spacing for small marks for <see cref="RulerScale"/> = 1.0
		/// </summary>
		public float SmallStep;

		/// <summary>
		/// Changes the number of small steps in a big.
		/// </summary>
		public int SmallStepsPerBig;

		/// <summary>
		/// Defines the initial values of the timeline.
		/// </summary>
		public float Offset;

		/// <summary>
		/// Timestamps labels color.
		/// </summary>
		public Color4 TextColor = Color4.White;

		/// <summary>
		/// Timestamps color.
		/// </summary>
		public Color4 TimestampsColor = Color4.White;

		public TimelineRuler(float smallStep, int smallStepsPerBig)
		{
			SmallStep = smallStep;
			SmallStepsPerBig = smallStepsPerBig;
			Presenter = new RulerPresenter(this);
		}

		private class RulerPresenter : IPresenter
		{
			private readonly TimelineRuler ruler;

			public RulerPresenter(TimelineRuler ruler) => this.ruler = ruler;

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.Ruler = ruler;
				ro.CaptureRenderState(ruler);
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public TimelineRuler Ruler;

				public override void Render()
				{
					PrepareRenderState();
					float smallStep = Ruler.SmallStep * Ruler.RulerScale;
					float offset = Ruler.Offset * Ruler.RulerScale;
					int stepIndex = (offset / smallStep).Floor();
					var startPosition = new Vector2(Ruler.SmallStep - Ruler.Offset % Ruler.SmallStep, 26);
					var offsetSmall = new Vector2(1, -6);
					var offsetBig = new Vector2(1, -16);
					var offsetText = new Vector2(4, -24);
					for (; startPosition.X < Ruler.Width; startPosition.X += Ruler.SmallStep, stepIndex++) {
						Vector2 endPosition;
						if (stepIndex % Ruler.SmallStepsPerBig == 0) {
							endPosition = startPosition + offsetBig;
							var time = $"{stepIndex * smallStep / 1000:0.0#}";
							Renderer.DrawTextLine(startPosition + offsetText, time, 16, Ruler.TextColor, 0);
						} else {
							endPosition = startPosition + offsetSmall;
						}
						Renderer.DrawRect(startPosition, endPosition, Ruler.TimestampsColor);
					}
				}
			}
		}
	}
}

#endif // PROFILER