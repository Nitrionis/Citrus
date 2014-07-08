#if WIN || MAC
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System;
using ProtoBuf;

using SD = System.Drawing;

namespace Lime
{
	class BitmapImplementation : IBitmapImplementation
	{
		private SD.Bitmap bitmap;

		public int GetWidth()
		{
			return bitmap.Width;
		}

		public int GetHeight()
		{
			return bitmap.Height;
		}

		public void LoadFromStream(Stream stream)
		{
			// System.Drawing.Bitmap �������, ����� stream ��������� �������� �� ����� ������������� �������.
			// http://stackoverflow.com/questions/336387/image-save-throws-a-gdi-exception-because-the-memory-stream-is-closed
			// ��� ��� �� �� ����� ���� �������, ��� ������� ����� �� ���������, �������� ���.
			var streamClone = new MemoryStream();
			Toolbox.CopyStream(stream, streamClone);

			InitWithPngOrJpgBitmap(streamClone);
		}

		public void SaveToStream(Stream stream)
		{
			if (bitmap != null) {
				bitmap.Save(stream, SD.Imaging.ImageFormat.Png);
			}
		}

		public IBitmapImplementation Crop(Rectangle cropArea)
		{
			var rect = new SD.RectangleF(cropArea.Left, cropArea.Top, cropArea.Width, cropArea.Height);
			var croppedBitmap = bitmap.Clone(rect, bitmap.PixelFormat);
			var result = new BitmapImplementation();
			result.bitmap = croppedBitmap;
			return result;
		}

		public void Dispose()
		{
			if (bitmap != null) {
				bitmap.Dispose();
			}
		}

		private void InitWithPngOrJpgBitmap(Stream stream)
		{
			bitmap = new SD.Bitmap(stream);
		}
	}
}
#endif