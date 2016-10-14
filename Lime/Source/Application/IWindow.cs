﻿using System;


namespace Lime
{
	/// <summary>
	/// Delegate used by <see cref="IWindow.Resized"/> event.
	/// </summary>
	/// <param name="deviceRotated">Defines whether resize is triggered by device rotation or not.</param>
	public delegate void ResizeDelegate(bool deviceRotated);

	/// <summary>
	/// Enumerates available window states.
	/// </summary>
	public enum WindowState
	{
		/// <summary>
		/// The window is in its normal state.
		/// </summary>
		Normal = 0,
		/// <summary>
		/// The window is minimized to the taskbar (also known as 'iconified').
		/// </summary>
		Minimized,
		/// <summary>
		/// The window covers the whole working area, which includes the desktop but not the taskbar and/or panels.
		/// </summary>
		Maximized,
		/// <summary>
		/// The window covers the whole screen, including all taskbars and/or panels.
		/// </summary>
		Fullscreen
	}

	/// <summary>
	/// Defines the interface for a window.
	/// </summary>
	public interface IWindow
	{
		/// <summary>
		/// Indicates whether the window is active.
		/// For PC, Mac it means that the window has the input focus.
		/// On mobile platforms it indicates that the application is on screen and running.
		/// </summary>
		bool Active { get; }

		/// <summary>
		/// Gets or sets the title of the window.
		/// </summary>
		string Title { get; set; }

		/// <summary>
		/// Gets or sets the window state.
		/// </summary>
		WindowState State { get; set; }

		/// <summary>
		/// Gets or sets the fullscreen window state.
		/// </summary>
		bool Fullscreen { get; set; }

		/// <summary>
		/// Gets or sets an upper-left corner of the client area on the desktop.
		/// </summary>
		Vector2 ClientPosition { get; set; }

		/// <summary>
		/// Gets or sets a client size of this window.
		/// </summary>
		Vector2 ClientSize { get; set; }

		/// <summary>
		/// Gets or sets a position of this window on the desktop.
		/// </summary>
		Vector2 DecoratedPosition { get; set; }

		/// <summary>
		/// Gets or sets an decorated size (including title and border) of this window.
		/// </summary>
		Vector2 DecoratedSize { get; set; }

		// Gets or sets the minimum size to which the window (including title and border) can be sized.
		Vector2 MinimumDecoratedSize { get; set; }

		// Gets or sets the maximum size to which the window (including title and border) can be sized.
		Vector2 MaximumDecoratedSize { get; set; }

		/// <summary>
		/// Gets or sets a value indicates whether the window is displayed.
		/// </summary>
		bool Visible { get; set; }

		/// <summary>
		/// Gets <see cref="Lime.Input"/> for this window.
		/// </summary>
		Input Input { get; }

		/// <summary>
		/// Gets current FPS for the window.
		/// </summary>
		float FPS { get; }

		[Obsolete("Use FPS property instead", true)]
		float CalcFPS();

		/// <summary>
		/// Centers the game window on the current display.
		/// </summary>
		void Center();

		/// <summary>
		/// Gets or sets the cursor for this window.
		/// </summary>
		MouseCursor Cursor { get; set; }

		/// <summary>
		/// Closes this window.
		/// </summary>
		void Close();

		/// <summary>
		/// Occurs when the <see cref="Active"/> property of the window becomes true.
		/// </summary>
		event Action Activated;

		/// <summary>
		/// Occurs when the <see cref="Active"/> property of the window becomes false.
		/// </summary>
		event Action Deactivated;

		/// <summary>
		/// Occurs when the window is about to close.
		/// Returns false to cancel closing.
		/// </summary>
		event Func<bool> Closing;

		/// <summary>
		/// Occurs when the window is about to show or hide.
		/// </summary>
		event Action<bool> VisibleChanging;

		/// <summary>
		/// Occurs after the window has closed.
		/// </summary>
		event Action Closed;

		/// <summary>
		/// Occurs whenever the window is moved.
		/// </summary>
		event Action Moved;

		/// <summary>
		/// Occurs whenever the window is resized or a device orientation has changed.
		/// </summary>
		event ResizeDelegate Resized;

		/// <summary>
		/// Occurs when it is time to update a frame.
		/// </summary>
		event Action<float> Updating;

		/// <summary>
		/// Occurs when it is time to render a frame.
		/// </summary>
		event Action Rendering;

		/// <summary>
		/// Sets a flag indicating whether the current frame should be rendered.
		/// </summary>
		void Invalidate();

#if iOS
		GameController UIViewController { get; }
#elif MAC
		Platform.NSGameView NSGameView { get; }
#elif WIN
		System.Windows.Forms.Form Form { get; }
#endif
		/// <summary>
		/// Gets the scale factor which translates virtual units to the physical pixels.
		/// </summary>
		float PixelScale { get; }

		IContext Context { get; set; }

		object Tag { get; set; }

		/// <summary>
		/// You can use this method to display a modal dialog box in your application. 
		/// When this method is called, the code following it is not executed until after the dialog box is closed.
		/// The window must be hidden before calling this method.
		/// </summary>
		void ShowModal();
	}
}
