﻿using System;
using CefSharp;
using CefSharp.Internals;
using Lime;
using Exception = Lime.Exception;

namespace ChromiumWebBrowser
{
	/// <summary>
	///	An offscreen instance of Chromium that you can use to render
	///	webpages or evaluate JavaScript.
	/// </summary>
	public class ChromiumWebBrowserLogic : IRenderWebBrowser
	{
		/// <summary>
		///	Object that contains info about last taken snapshot.
		/// </summary>
		public BitmapInfo BitmapInfo;

		/// <summary>
		///	Chromium binder.
		///	Examples of usage: https://github.com/cefsharp/CefSharp
		/// </summary>
		private ManagedCefBrowserAdapter managedCefBrowserAdapter;

		/// <summary>
		///	Size of the Chromium viewport.
		///	This must be set to something other than 0x0 otherwise Chromium will not render.
		/// </summary>
		private Size size;

		static ChromiumWebBrowserLogic()
		{
			Cef.Initialize();
			Application.MainWindow.Closed += () => {
				if (Cef.IsInitialized) {
					Cef.Shutdown();
				}
			};
		}

		/// <summary>
		/// Create a new OffScreen Chromium Browser.
		/// </summary>
		/// <param name="height">Height of browser</param>
		/// <param name="address">Initial address (url) to load</param>
		/// <param name="browserSettings">The browser settings to use. If null, the default settings are used.</param>
		/// <param name="requestcontext">See <see cref="RequestContext"/> for more details. Defaults to null</param>
		/// <param name="width">Width of browser</param>
		public ChromiumWebBrowserLogic(int width = 1366, int height = 768, string address = "", 
			RequestContext requestcontext = null, BrowserSettings browserSettings = null)
		{
			if (!Cef.IsInitialized && !Cef.Initialize()) {
				throw new InvalidOperationException("Cef::Initialize() failed");
			}

			size = new Size(width, height);
			ResourceHandlerFactory = new DefaultResourceHandlerFactory();
			BrowserSettings = browserSettings ?? new BrowserSettings();
			RequestContext = requestcontext;

			Cef.AddDisposable(this);
			Address = address;

			managedCefBrowserAdapter = new ManagedCefBrowserAdapter(this, true);
			managedCefBrowserAdapter.CreateOffscreenBrowser(IntPtr.Zero, BrowserSettings, RequestContext, address);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		// TODO: Implement better way to pass url if !IsBrowserInitialized
		public void Load(string url)
		{
			Address = url;
			if (IsBrowserInitialized) {
				this.GetMainFrame().LoadUrl(url);
			}
			else {
				BrowserInitialized += (sender, args) => { Load(url); };
			}
		}

		public void RegisterJsObject(string name, object objectToBind, bool camelCaseJavascriptNames = true)
		{
			if (IsBrowserInitialized) {
				throw new Exception("Browser is already initialized. RegisterJsObject must be" +
									"called before the underlying CEF browser is created.");
			}

			//Enable WCF if not already enabled
			CefSharpSettings.WcfEnabled = true;

			managedCefBrowserAdapter.RegisterJsObject(name, objectToBind, camelCaseJavascriptNames);
		}

		public void RegisterAsyncJsObject(string name, object objectToBind, bool camelCaseJavascriptNames = true)
		{
			if (IsBrowserInitialized) {
				throw new Exception("Browser is already initialized. RegisterJsObject must be" +
									"called before the underlying CEF browser is created.");
			}
			managedCefBrowserAdapter.RegisterAsyncJsObject(name, objectToBind, camelCaseJavascriptNames);
		}

		public bool Focus()
		{
			return true;
		}

		public IBrowser GetBrowser()
		{
			this.ThrowExceptionIfBrowserNotInitialized();
			return managedCefBrowserAdapter.GetBrowser();
		}

		public IBrowserHost BrowserHost
		{
			get { return GetBrowser().GetHost(); }
		}

		public IDialogHandler DialogHandler { get; set; }
		public IRequestHandler RequestHandler { get; set; }
		public IDisplayHandler DisplayHandler { get; set; }
		public ILoadHandler LoadHandler { get; set; }
		public ILifeSpanHandler LifeSpanHandler { get; set; }
		public IKeyboardHandler KeyboardHandler { get; set; }
		public IJsDialogHandler JsDialogHandler { get; set; }
		public IDragHandler DragHandler { get; set; }
		public IDownloadHandler DownloadHandler { get; set; }
		public IContextMenuHandler MenuHandler { get; set; }
		public IFocusHandler FocusHandler { get; set; }
		public IResourceHandlerFactory ResourceHandlerFactory { get; set; }
		public IGeolocationHandler GeolocationHandler { get; set; }
		public bool IsBrowserInitialized { get; private set; }
		public bool IsLoading { get; private set; }
		public bool CanGoBack { get; private set; }
		public bool CanGoForward { get; private set; }
		public string Address { get; private set; }
		public string TooltipText { get; private set; }
		public BrowserSettings BrowserSettings { get; private set; }
		public RequestContext RequestContext { get; private set; }
		public event EventHandler<ConsoleMessageEventArgs> ConsoleMessage;
		public event EventHandler<StatusMessageEventArgs> StatusMessage;
		public event EventHandler<FrameLoadStartEventArgs> FrameLoadStart;
		public event EventHandler<FrameLoadEndEventArgs> FrameLoadEnd;
		public event EventHandler<LoadErrorEventArgs> LoadError;
		public event EventHandler<LoadingStateChangedEventArgs> LoadingStateChanged;
		public event EventHandler BrowserInitialized;
		public event EventHandler<AddressChangedEventArgs> AddressChanged;
		public event EventHandler<TitleChangedEventArgs> TitleChanged;
		public event EventHandler<PopupOpenArgs> PopupOpen;
		public event EventHandler<PopupTransformArgs> PopupTransformed;

		/// <summary>
		/// Fired by a separate thread when Chrome has re-rendered.
		/// </summary>
		public event EventHandler NewScreenshot;
		
		/// <summary>
		/// Get/set the size of the Chromium viewport, in pixels.
		/// This also changes the size of the next screenshot.
		/// </summary>
		public Size Size
		{
			get { return size; }
			set
			{
				if (size != value) {
					size = value;
					managedCefBrowserAdapter.WasResized();
				}
			}
		}

		public void OnAfterBrowserCreated()
		{
			IsBrowserInitialized = true;

			var handler = BrowserInitialized;
			if (handler != null) {
				handler(this, EventArgs.Empty);
			}
		}

		public void SetAddress(AddressChangedEventArgs args)
		{
			Address = args.Address;

			var handler = AddressChanged;
			if (handler != null) {
				handler(this, args);
			}
		}

		public void SetLoadingStateChange(LoadingStateChangedEventArgs args)
		{
			CanGoBack = args.CanGoBack;
			CanGoForward = args.CanGoForward;
			IsLoading = args.IsLoading;

			var handler = LoadingStateChanged;
			if (handler != null) {
				handler(this, args);
			}
		}

		public void SetTitle(TitleChangedEventArgs args)
		{
			var handler = TitleChanged;
			if (handler != null) {
				handler(this, args);
			}
		}

		public void SetTooltipText(string tooltipText)
		{
			TooltipText = tooltipText;
		}

		public void OnFrameLoadStart(FrameLoadStartEventArgs args)
		{
			var handler = FrameLoadStart;
			if (handler != null) {
				handler(this, args);
			}
		}

		public void OnFrameLoadEnd(FrameLoadEndEventArgs args)
		{
			var handler = FrameLoadEnd;
			if (handler != null) {
				handler(this, args);
			}
		}

		public void OnConsoleMessage(ConsoleMessageEventArgs args)
		{
			var handler = ConsoleMessage;
			if (handler != null) {
				handler(this, args);
			}
		}

		public void OnStatusMessage(StatusMessageEventArgs args)
		{
			var handler = StatusMessage;
			if (handler != null) {
				handler(this, args);
			}
		}

		public void OnLoadError(LoadErrorEventArgs args)
		{
			var handler = LoadError;
			if (handler != null) {
				handler(this, args);
			}
		}

		public IBrowserAdapter BrowserAdapter
		{
			get { return managedCefBrowserAdapter; }
		}

		public bool HasParent { get; set; }

		public IntPtr ControlHandle
		{
			get { return IntPtr.Zero; }
		}

		public ScreenInfo GetScreenInfo()
		{
			return new ScreenInfo {
				ScaleFactor = 1.0F
			};
		}

		public ViewRect GetViewRect()
		{
			return new ViewRect {
				Width = size.Width,
				Height = size.Height
			};
		}

		public BitmapInfo CreateBitmapInfo(bool isPopup)
		{
			return new GdiBitmapInfo { IsPopup = isPopup };
		}

		/// <summary>
		/// Invoked from CefRenderHandler.OnPaint
		/// Locking provided by OnPaint as this method is called in it's lock scope
		/// </summary>
		/// <param name="bitmapInfo">information about the bitmap to be rendered</param>
		public void InvokeRenderAsync(BitmapInfo bitmapInfo)
		{
			BitmapInfo = bitmapInfo;

			var handler = NewScreenshot;
			if (handler != null) {
				handler(this, EventArgs.Empty);
			}
		}

		// TODO: Deal with it
		// There are two ways to deal with it:
		// 1. Find a way to handle with the handle (haha, get it?)
		// Tip: it has type of HCURSOR in WinAPI
		// 2. Implement other cursor types (maybe a bad idea)
		public void SetCursor(IntPtr cursor, CefCursorType type)
		{
			switch (type) {
				case CefCursorType.Pointer:
					Application.MainWindow.Cursor = MouseCursor.Default;
					break;
				case CefCursorType.IBeam:
					Application.MainWindow.Cursor = new MouseCursor("Cursors.IBeam.png", new IntVector2(6, 8), "ChromiumWebBrowser");
					break;
				case CefCursorType.Hand:
					Application.MainWindow.Cursor = new MouseCursor("Cursors.Hand.png", new IntVector2(6, 8), "ChromiumWebBrowser");
					break;
			}
		}

		public bool StartDragging(IDragData dragData, DragOperationsMask mask, int x, int y)
		{
			//TODO: Implement
			return false;
		}

		public void SendMouseWheelEvent(int x, int y, int deltaX, int deltaY, 
			CefEventFlags modifiers = CefEventFlags.None)
		{
			if (!IsBrowserInitialized) {
				return;
			}
			BrowserHost.SendMouseWheelEvent(x, y, deltaX, deltaY, modifiers);
		}

		public void SendMouseClick(int x, int y, MouseButtonType buttonType, bool mouseUp, 
			CefEventFlags modifiers = CefEventFlags.None, int clickCount = 1)
		{
			if (!IsBrowserInitialized) {
				return;
			}
			BrowserHost.SendMouseClickEvent(x, y, buttonType, mouseUp, clickCount, modifiers);
			managedCefBrowserAdapter.SendFocusEvent(true);
		}

		public void SendMouseMove(int x, int y, bool mouseLeave, CefEventFlags modifiers = CefEventFlags.None)
		{
			if (!IsBrowserInitialized) {
				return;
			}
			BrowserHost.SendMouseMoveEvent(x, y, mouseLeave, modifiers);
		}

		public void SendKeyPress(int message, int wParam, CefEventFlags modifiers = CefEventFlags.None)
		{
			managedCefBrowserAdapter.SendKeyEvent(message, wParam, (int)modifiers);
		}

		public void SetPopupIsOpen(bool show)
		{
			var handler = PopupOpen;
			if (handler != null) {
				handler(this, new PopupOpenArgs(show));
			}
		}

		public void SetPopupSizeAndPosition(int width, int height, int x, int y)
		{
			var handler = PopupTransformed;
			if (handler != null) {
				handler(this, new PopupTransformArgs(width, height, x, y));
			}
		}

		private void Dispose(bool disposing)
		{
			ClearHandlers();
			ClearEventListeners();

			Cef.RemoveDisposable(this);

			if (!disposing) {
				return;
			}

			if (BrowserSettings != null) {
				BrowserSettings.Dispose();
				BrowserSettings = null;
			}

			if (managedCefBrowserAdapter != null) {
				if (!managedCefBrowserAdapter.IsDisposed) {
					managedCefBrowserAdapter.Dispose();
				}
				managedCefBrowserAdapter = null;
			}
		}

		private void ClearEventListeners()
		{
			LoadError = null;
			FrameLoadStart = null;
			FrameLoadEnd = null;
			ConsoleMessage = null;
			BrowserInitialized = null;
			StatusMessage = null;
			AddressChanged = null;
		}

		private void ClearHandlers()
		{
			ResourceHandlerFactory = null;
			JsDialogHandler = null;
			DialogHandler = null;
			DownloadHandler = null;
			KeyboardHandler = null;
			LifeSpanHandler = null;
			MenuHandler = null;
			FocusHandler = null;
			RequestHandler = null;
			DragHandler = null;
			GeolocationHandler = null;
		}
	}
}