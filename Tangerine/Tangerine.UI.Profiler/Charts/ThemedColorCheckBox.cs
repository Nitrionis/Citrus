#if PROFILER

using Lime;

namespace Tangerine.UI.Charts
{
	internal class ThemedColorCheckBox : CheckBox
	{
		public override bool IsNotDecorated() => false;

		public ThemedColorCheckBox(Color4 activeColor, Color4 disabledColor)
		{
			Layout = new StackLayout();
			Presenter = new CheckBoxPresenter(this, activeColor, disabledColor);
			AddNode(new Button {
				Id = "Button",
				LayoutCell = new LayoutCell(Alignment.Center),
				MinMaxSize = new Vector2(12),
				Size = new Vector2(12),
				TabTravesable = null
			});
			MinMaxSize = new Vector2(12);
			Size = new Vector2(12);
			TabTravesable = new TabTraversable();
			LateTasks.Add(Theme.MouseHoverInvalidationTask(this));
			State = CheckBoxState.Checked;
		}

		private class CheckBoxPresenter : IPresenter
		{
			private readonly CheckBox checkBox;
			private readonly Color4 activeColor;
			private readonly Color4 disabledColor;

			public CheckBoxPresenter(CheckBox checkBox, Color4 activeColor, Color4 disabledColor)
			{
				this.checkBox = checkBox;
				this.activeColor = activeColor;
				this.disabledColor = disabledColor;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var widget = (Widget)node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(widget);
				ro.Size = widget.Size;
				ro.Color = checkBox.State == CheckBoxState.Checked ? activeColor : disabledColor;
				return ro;
			}

			public IPresenter Clone() => (IPresenter)MemberwiseClone();

			private class RenderObject : WidgetRenderObject
			{
				public Vector2 Size;
				public Color4 Color;

				public override void Render()
				{
					PrepareRenderState();
					Renderer.DrawRect(Vector2.Zero, Size, Color);
					Renderer.DrawRectOutline(Vector2.Zero, Size, Theme.Colors.ControlBorder);
				}
			}
		}
	}
}

#endif // PROFILER