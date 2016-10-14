#if WIN || MAC || MONOMAC
using System;

namespace Lime
{
	public class DummyWindow : CommonWindow, IWindow
	{
		public bool Active { get; set; }
		public string Title { get; set; }
		public WindowState State { get; set; }
		public bool Fullscreen { get; set; }
		public Vector2 ClientPosition { get; set; }
		public Vector2 ClientSize { get; set; }
		public Vector2 DecoratedPosition { get; set; }
		public Vector2 DecoratedSize { get; set; }
		public Vector2 MinimumDecoratedSize { get; set; }
		public Vector2 MaximumDecoratedSize { get; set; }
		public bool Visible { get; set; }
		public Input Input { get; set; }
		public float FPS { get; set; }
		public float CalcFPS()
		{
			throw new NotImplementedException();
		}
		public void Center()
		{
			throw new NotImplementedException();
		}
		public MouseCursor Cursor { get; set; }
		public void Close()
		{
			throw new NotImplementedException();
		}
		public void Invalidate() {}
#if MAC
		public Platform.NSGameView NSGameView { get; set; }
#elif WIN
		public System.Windows.Forms.Form Form { get; set; }
#endif
		public float PixelScale { get; set; }

		public void ShowModal() {}
	}
}
#endif
