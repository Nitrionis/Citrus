using System;
using System.IO;
using System.Net;
using Gtk;
using Lime;
using System.Runtime.InteropServices;

namespace Orange
{
	public static class TextureConverter
	{
		public static void ToPVR_PVRTC(Gdk.Pixbuf pixbuf, string dstPath, PVRFormat pvrFormat, bool mipMaps)
		{
			int width = pixbuf.Width;
			int height = pixbuf.Height;
			bool hasAlpha = pixbuf.HasAlpha;

			int potWidth = TextureConverterUtils.GetNearestPowerOf2(width, 8, 1024);
			int potHeight = TextureConverterUtils.GetNearestPowerOf2(height, 8, 1024);
			
			int maxDimension = Math.Max(potWidth, potHeight);

			string args = "-shh";
			switch (pvrFormat) {
			case PVRFormat.Compressed:
				args += " -f PVRTC1_4";
				width = height = maxDimension;
				break;
			case PVRFormat.RGB565:
				if (hasAlpha) {
					Console.WriteLine("WARNING: texture has alpha channel. Used 'RGBA4444' format instead of 'RGB565'.");
					args += " -f r4g4b4a4";
					TextureConverterUtils.ReduceTo4BitsPerChannelWithFloydSteinbergDithering(pixbuf);
				} else {
					args += " -f r5g6b5";
				}
				break;
			case PVRFormat.RGBA4:
				args += " -f r4g4b4a4";
				TextureConverterUtils.ReduceTo4BitsPerChannelWithFloydSteinbergDithering(pixbuf);
				break;
			case PVRFormat.ARGB8:
				args += " -f r8g8b8a8";
				break;
			}
			string tga = Path.ChangeExtension(dstPath, ".tga");
			try {
				if (pixbuf.HasAlpha) {
					args += " -l"; // Enable alpha bleed
				}
				TextureConverterUtils.SwapRGBChannels(pixbuf);
				TextureConverterUtils.SaveToTGA(pixbuf, tga);
				if (mipMaps) {
					args += " -m";
				}
				args += String.Format(" -i '{0}' -o '{1}' -r {2},{3}", tga, dstPath, width, height);
				var pvrTexTool = Path.Combine(Toolbox.GetApplicationDirectory(), "Toolchain.Mac", "PVRTexTool");
				Mono.Unix.Native.Syscall.chmod(pvrTexTool, Mono.Unix.Native.FilePermissions.S_IXOTH | Mono.Unix.Native.FilePermissions.S_IXUSR);
				var p = System.Diagnostics.Process.Start(pvrTexTool, args);
				p.WaitForExit();
				if (p.ExitCode != 0) {
					throw new Lime.Exception("Failed to convert '{0}' to PVR format(error code: {1})", tga, p.ExitCode);
				}
			} finally {
				File.Delete(tga);
			}
		}

		public static void ToPVR_ETC1(Gdk.Pixbuf pixbuf, string dstPath, PVRFormat pvrFormat, bool mipMaps)
		{
			string formatArguments;
			switch (pvrFormat) {
			case PVRFormat.Compressed:
				formatArguments = "-f etc1 -q etcfast";
				break;
			case PVRFormat.RGB565:
				if (pixbuf.HasAlpha) {
					Console.WriteLine("WARNING: texture has alpha channel. Used 'RGBA4444' format instead of 'RGB565'.");
					formatArguments = "-f r4g4b4a4";
					TextureConverterUtils.ReduceTo4BitsPerChannelWithFloydSteinbergDithering(pixbuf);
				} else {
					formatArguments = "-f r5g6b5";
				}
				break;
			case PVRFormat.RGBA4:
				formatArguments = "-f r4g4b4a4";
				TextureConverterUtils.ReduceTo4BitsPerChannelWithFloydSteinbergDithering(pixbuf);
				break;
			case PVRFormat.ARGB8:
				formatArguments = "-f r8g8b8a8";
				break;
			default:
				throw new ArgumentException();
			}
			var tga = Path.ChangeExtension(dstPath, ".tga");
			try {
				TextureConverterUtils.SwapRGBChannels(pixbuf);
				TextureConverterUtils.SaveToTGA(pixbuf, tga);
				var pvrTexTool = Path.Combine(Toolbox.GetApplicationDirectory(), "Toolchain.Win", "PVRTexToolCli");
				// -p - premultiply alpha
				// -shh - silent
				tga = MakeAbsolutePath(tga);
				dstPath = MakeAbsolutePath(dstPath);
				var args = String.Format("{0} -i \"{1}\" -o \"{2}\" {3} -p -shh", formatArguments, tga, dstPath, mipMaps ? "-m" : "");
				int result = Process.Start(pvrTexTool, args, Process.Options.RedirectErrors);
				if (result != 0) {
					throw new Lime.Exception("Error converting '{0}'\nCommand line: {1}", tga, pvrTexTool + " " + args);
				}
			} finally {
				try {
					File.Delete(tga);
				} catch { }
			}
		}

		public static void ToDDS_DXTi(Gdk.Pixbuf pixbuf, string dstPath, DDSFormat format, bool mipMaps)
		{
			bool compressed = format == DDSFormat.DXTi;
			if (pixbuf.HasAlpha) {
				TextureConverterUtils.BleedAlpha(pixbuf);
			}
			if (compressed) {
				TextureConverterUtils.SwapRGBChannels(pixbuf);
			}
			var tga = Path.ChangeExtension(dstPath, ".tga");
			try {
				TextureConverterUtils.SaveToTGA(pixbuf, tga);
				ToDDSTextureHelper(tga, dstPath, pixbuf.HasAlpha, compressed, mipMaps);
			} finally {
				File.Delete(tga);
			}
		}
		
		private static void ToDDSTextureHelper(string srcPath, string dstPath, bool hasAlpha, bool compressed, bool mipMaps)
		{
			string mipsFlag = mipMaps ? "" : "-nomips";
			string compressionFlag = compressed ? (hasAlpha ? "-bc3" : "-bc1") : "-rgb";
#if WIN
			string nvcompress = Path.Combine(Toolbox.GetApplicationDirectory(), "Toolchain.Win", "nvcompress.exe");
			srcPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), srcPath);
			dstPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), dstPath);
			string args = String.Format("{0} {1} \"{2}\" \"{3}\"", mipsFlag, compressionFlag, srcPath, dstPath);
			int result = Process.Start(nvcompress, args, Process.Options.RedirectErrors);
			if (result != 0) {
				throw new Lime.Exception("Failed to convert '{0}' to DDS format(error code: {1})", srcPath, result);
			}
#else
			string nvcompress = Path.Combine(Toolbox.GetApplicationDirectory(), "Toolchain.Mac", "nvcompress");
			Mono.Unix.Native.Syscall.chmod(nvcompress, Mono.Unix.Native.FilePermissions.S_IXOTH | Mono.Unix.Native.FilePermissions.S_IXUSR);
			string args = String.Format("{0} {1} '{2}' '{3}'", mipsFlag, compressionFlag, srcPath, dstPath);
			Console.WriteLine(nvcompress);
			var psi = new System.Diagnostics.ProcessStartInfo(nvcompress, args);
			var p = System.Diagnostics.Process.Start(psi);
			while (!p.HasExited) {
				p.WaitForExit();
			}
			if (p.ExitCode != 0) {
				throw new Lime.Exception("Failed to convert '{0}' to DDS format(error code: {1})", srcPath, p.ExitCode);
			}
#endif
		}

		public static void ToJPG(Gdk.Pixbuf pixbuf, string dstPath, bool mipMaps)
		{
			if (pixbuf.HasAlpha) {
				TextureConverterUtils.PremultiplyAlpha(pixbuf, false);
			}
			pixbuf.Savev(dstPath, "jpeg", new string[] { "quality" }, new string[] { "80" });
		}

		public static void ToGrayscaleAlphaPNG(Gdk.Pixbuf pixbuf, string dstPath, bool mipMaps)
		{
			TextureConverterUtils.ConvertBitmapToGrayscaleAlphaMask(pixbuf);
			pixbuf.Save(dstPath, "png");
			var pngcrushTool = Path.Combine(Toolbox.GetApplicationDirectory(), "Toolchain.Win", "pngcrush_1_7_83_w32");
			// -ow - overwrite
			// -s - silent
			// -c 0 - change color type to greyscale
			dstPath = MakeAbsolutePath(dstPath);
			var args = String.Format("-ow -s -c 0 \"{0}\"", dstPath);
			int result = Process.Start(pngcrushTool, args, Process.Options.RedirectErrors);
			if (result != 0) {
				throw new Lime.Exception("Error converting '{0}'\nCommand line: {1}", dstPath, pngcrushTool + " " + args);
			}
		}

		private static string MakeAbsolutePath(string path)
		{
			if (!Path.IsPathRooted(path)) {
				path = Path.Combine(System.IO.Directory.GetCurrentDirectory(), path);
			}
			return path;
		}
	}
}
