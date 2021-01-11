#if PROFILER

using Lime;

namespace Tangerine.UI.Timelines
{
	internal partial class Timeline
	{
		private class Ruler : Widget
		{
			/// <summary>
			/// Use to scale.
			/// </summary>
			public float RulerScale = 1.0f;

			/// <summary>
			/// Pixel spacing for small marks for <see cref="RulerScale"/> = 1.0
			/// </summary>
			public float SmallStepSize;

			/// <summary>
			/// Count of small steps in a big step.
			/// </summary>
			public int SmallStepsPerBig;

			/// <summary>
			/// Defines the initial values of the timeline.
			/// </summary>
			public float RulerOffset;

			/// <summary>
			/// Timestamps labels color.
			/// </summary>
			public Color4 TextColor = Color4.White;

			/// <summary>
			/// Timestamps color.
			/// </summary>
			public Color4 TimestampsColor = Color4.White;

			public Ruler(float smallStepSize, int smallStepsPerBig)
			{
				SmallStepSize = smallStepSize;
				SmallStepsPerBig = smallStepsPerBig;
				Presenter = new RulerPresenter(this);
			}

			private class RulerPresenter : IPresenter
			{
				private readonly Ruler ruler;

				public RulerPresenter(Ruler ruler) => this.ruler = ruler;

				public Lime.RenderObject GetRenderObject(Node node)
				{
					var ro = RenderObjectPool<RenderObject>.Acquire();
					ro.CaptureRenderState(ruler);
					ro.ContainerWidth = node.AsWidget.Width;
					ro.RulerScale = ruler.RulerScale;
					ro.SmallStepSize = ruler.SmallStepSize;
					ro.SmallStepsPerBig = ruler.SmallStepsPerBig;
					ro.RulerOffset = ruler.RulerOffset;
					ro.TextColor = ruler.TextColor;
					ro.TimestampsColor = ruler.TimestampsColor;
					return ro;
				}

				public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

				private class RenderObject : WidgetRenderObject
				{
					public float ContainerWidth;
					public float RulerScale;
					public float SmallStepSize;
					public float SmallStepsPerBig;
					public float RulerOffset;
					public Color4 TextColor;
					public Color4 TimestampsColor;

					public override void Render()
					{
						PrepareRenderState();
						var offsetSmall = new Vector2(1, -6);
						var offsetBig = new Vector2(1, -16);
						var offsetText = new Vector2(4, -24);
						float scaledSmallStep = SmallStepSize * RulerScale;
						int stepIndex = (RulerOffset / SmallStepSize).Floor();
						var startPosition = new Vector2(SmallStepSize - RulerOffset % SmallStepSize, 26);
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
}

#endif // PROFILER
