#if OPENGL
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if WIN || MAC
using OpenTK.Graphics.OpenGL;
#elif ANDROID
using OpenTK.Graphics.ES20;
#endif

namespace Lime
{
	public partial class Texture2D : CommonTexture, ITexture
	{
#if WIN || MAC || ANDROID
		enum DDSFourCC
		{
			DXT1 = ('D' | ('X' << 8) | ('T' << 16) | ('1' << 24)),
			DXT3 = ('D' | ('X' << 8) | ('T' << 16) | ('3' << 24)),
			DXT5 = ('D' | ('X' << 8) | ('T' << 16) | ('5' << 24)),
		}

		[Flags]
		enum DDSPFFlags
		{
			Alpha = 0x01,
			FourCC = 0x04,
			RGB = 0x40
		}

		const UInt32 DDSMagic = 0x20534444;

		private void InitWithDDSBitmap(BinaryReader reader)
		{
			UInt32 magic = reader.ReadUInt32();
			UInt32 size = reader.ReadUInt32();
			if (magic != DDSMagic || size != 124) {
				throw new Lime.Exception("Invalid DDS file header");
			}
			// UInt32 flags =
			reader.ReadUInt32();
			int height = reader.ReadInt32();
			int width = reader.ReadInt32();
			int pitchOrLinearSize = (int)reader.ReadUInt32();
			// UInt32 depth =
			reader.ReadUInt32();
			UInt32 mipMapCount = reader.ReadUInt32();
			reader.ReadBytes(11 * 4);
			// Read pixel format
			reader.ReadUInt32(); // Structure size (32 bytes)
			DDSPFFlags pfFlags = (DDSPFFlags)reader.ReadUInt32();
			UInt32 pfFourCC = reader.ReadUInt32();
			// UInt32 pfRGBBitCount =
			reader.ReadUInt32();
			// UInt32 pfRBitMask =
			reader.ReadUInt32();
			// UInt32 pfGBitMask =
			reader.ReadUInt32();
			// UInt32 pfBBitMask =
			reader.ReadUInt32();
			// UInt32 pfABitMask =
			reader.ReadUInt32();
			// read the rest of header
			// UInt32 caps =
			reader.ReadUInt32();
			// UInt32 caps2 =
			reader.ReadUInt32();
			// UInt32 caps3 =
			reader.ReadUInt32();
			// UInt32 caps4 =
			reader.ReadUInt32();
			// UInt32 reserved2 =
			reader.ReadUInt32();

			SurfaceSize = ImageSize = new Size(width, height);
			mipMapCount = 1;
			Action glCommands = PrepareOpenGLTexture;
			MemoryUsed = 0;
			for (int level = 0; level < mipMapCount; level++) {
				if (width < 8 || height < 8) {
					break;
				}
				if ((pfFlags & DDSPFFlags.RGB) != 0) {
					ReadRGBAImage(ref glCommands, reader, level, width, height, pitchOrLinearSize);
				} else if ((pfFlags & DDSPFFlags.FourCC) != 0) {
					ReadCompressedImage(ref glCommands, reader, level, width, height, pitchOrLinearSize, pfFourCC);
				} else {
					throw new Lime.Exception("Error reading DDS");
				}
				pitchOrLinearSize /= 4;
				width /= 2;
				height /= 2;
			}
			Application.InvokeOnMainThread(glCommands);
		}

		private void ReadRGBAImage(ref Action glCommands, BinaryReader reader, int level, int width, int height, int pitch)
		{
			if (pitch != width * 4) {
				throw new Lime.Exception("Error reading RGBA texture. Must be 32 bit rgba");
			}
			var buffer = ReadTextureData(reader, pitch * height);
			glCommands += () => {
				PlatformRenderer.PushTexture(handle, 0);
				GL.TexImage2D(TextureTarget.Texture2D, level, PixelInternalFormat.Rgba, width, height, 0,
					PixelFormat.Rgba, PixelType.UnsignedByte, buffer);
				PlatformRenderer.PopTexture(0);
				PlatformRenderer.CheckErrors();
			};
		}

		private void ReadCompressedImage(ref Action glCommands, BinaryReader reader, int level, int width, int height, int linearSize, UInt32 pfFourCC)
		{
#if ANDROID
			throw new NotSupportedException();
#else
			var pif = PixelInternalFormat.CompressedRgbS3tcDxt1Ext;
			switch ((DDSFourCC)pfFourCC) {
				case DDSFourCC.DXT1:
					pif = PixelInternalFormat.CompressedRgbS3tcDxt1Ext;
					break;
				case DDSFourCC.DXT3:
					pif = PixelInternalFormat.CompressedRgbaS3tcDxt3Ext;
					break;
				case DDSFourCC.DXT5:
					pif = PixelInternalFormat.CompressedRgbaS3tcDxt5Ext;
					break;
				default:
					throw new Lime.Exception("Unsupported texture format");
			}
			var buffer = ReadTextureData(reader, linearSize);
			glCommands += () => {
				PlatformRenderer.PushTexture(handle, 0);
				GL.CompressedTexImage2D(TextureTarget.Texture2D, level, pif, width, height, 0, buffer.Length, buffer);
				PlatformRenderer.PopTexture(0);
				PlatformRenderer.CheckErrors();
			};
#endif
		}
#endif
	}
}
#endif