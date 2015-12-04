﻿#if UNITY
using System;

namespace Lime
{
	public class Window : CommonWindow, IWindow
	{
		private FPSCounter fpsCounter;
		
		public bool Active { get; private set; }
		public bool Fullscreen { get { return true; } set {} }
		public string Title { get; set; }
		public bool Visible { get { return true; } set {} }
		public Input Input { get { return input; } }
		public MouseCursor Cursor { get; set; }
		public WindowState State { get { return WindowState.Fullscreen; } set {} }
		public IntVector2 ClientPosition { get { return IntVector2.Zero; } set {} }
		public IntVector2 DecoratedPosition { get { return IntVector2.Zero; } set {} }
		public Size ClientSize { get { return new Size(UnityEngine.Screen.width, UnityEngine.Screen.height); } set {} }
		public Size DecoratedSize { get { return ClientSize; } set {} }
		public Size MinimumDecoratedSize { get { return Size.Zero; } set {} }
		public Size MaximumDecoratedSize { get { return Size.Zero; } set {} }
		
		public float CalcFPS() { return fpsCounter.FPS; }
		public void Center() {}
		public void Close() {}

		private Input input;
		public Window(WindowOptions options) 
		{
			if (Application.MainWindow != null) {
				throw new Lime.Exception("Attempt to set Application.MainWindow twice");
			}
			Application.MainWindow = this;
			Active = true;
			fpsCounter = new FPSCounter();
			input = new Input();

			UnityApplicationDelegate.Instance.Updating += delta => {
				Input.Refresh();
				RaiseUpdating(delta);
				AudioSystem.Update();
				Input.TextInput = null;
				Input.CopyKeysState();
			};
			UnityApplicationDelegate.Instance.Rendering += () => {
				RaiseRendering();
				fpsCounter.Refresh();
			};
			UnityApplicationDelegate.Instance.Destroying += () => {
				RaiseClosed();
			};
		}

	}
}
#endif