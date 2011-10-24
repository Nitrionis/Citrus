using System;

namespace Lime
{
	[Flags]
	public enum DeviceOrientation
	{
		Portrait = 1,
		PortraitUpsideDown = 2,
		LandscapeLeft = 4,
		LandscapeRight = 8,
	}
	
	public interface IGameWindow
	{
		Size WindowSize { get; set; }
		bool FullScreen { get; set; }
		float FrameRate { get; }
		DeviceOrientation CurrentDeviceOrientation { get; }
	}

	public enum MouseButton
	{
		Left, Right
	}
	
	public interface IGameApp
	{
		void OnCreate (IGameWindow gameWindow);
		void OnGLCreate ();
		void OnGLDestroy ();
		void OnUpdateFrame (double delta);
		void OnRenderFrame ();
		void OnDeviceRotated (DeviceOrientation deviceOrientation);
		
		void OnMouseUp (MouseButton button, Vector2 pointer);
		void OnMouseDown (MouseButton button, Vector2 pointer);
		
		DeviceOrientation SupportedDeviceOrientations { get; }
	}
}

