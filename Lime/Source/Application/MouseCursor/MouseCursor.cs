﻿#if WIN
using NativeCursor = System.Windows.Forms.Cursor;
#elif MAC || MONOMAC
using NativeCursor = AppKit.NSCursor;
#elif ANDROID || iOS || UNITY
using NativeCursor = System.Object;
#endif

namespace Lime
{
	public class MouseCursor
	{
		private static readonly IStockCursors cursors = new StockCursors();
		private readonly MouseCursorImplementation implementation;

		internal MouseCursor(MouseCursorImplementation implementation)
		{
			this.implementation = implementation;
		}

		public MouseCursor(Bitmap bitmap, IntVector2 hotSpot)
		{
			implementation = new MouseCursorImplementation(bitmap, hotSpot);
		}

		internal NativeCursor NativeCursor
		{
			get { return implementation.NativeCursor; }
		}

		public static MouseCursor Default { get { return cursors.Default; } }
		public static MouseCursor Empty { get { return cursors.Empty; } }
		public static MouseCursor Hand { get { return cursors.Hand; } }
		public static MouseCursor IBeam { get { return cursors.IBeam; } }

		/// <summary>
		/// Gets the two-headed vertical (north/south) sizing cursor.
		/// </summary>
		public static MouseCursor SizeNS { get { return cursors.SizeNS; } }

		/// <summary>
		/// Gets the two-headed horizontal(west/east) sizing cursor.
		/// </summary>
		public static MouseCursor SizeWE { get { return cursors.SizeWE; } }
	}
}