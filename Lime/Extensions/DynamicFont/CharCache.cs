﻿using System;
using System.Collections.Generic;

namespace Lime
{
	class CharCache
	{
		private FontRenderer fontRenderer;
		private DynamicTexture texture;
		private IntVector2 position;
		private List<ITexture> textures;
		private int textureIndex;
		private int fontHeight;
		private readonly CharMap charMap = new CharMap();

		public CharCache(int fontHeight, FontRenderer fontRenderer, List<ITexture> textures)
		{
			this.fontHeight = fontHeight;
			this.textures = textures;
			this.fontRenderer = fontRenderer;
		}

		public FontChar Get(char code)
		{
			var c = charMap[code];
			if (c != null) {
				return c;
			}
			c = CreateFontChar(code);
			if (c != null) {
				charMap[code] = c;
				return c;
			}
			return CharMap.TranslateKnownMissingChars(ref code) ?
				Get(code) : (charMap[code] = FontChar.Null);
		}

		private FontChar CreateFontChar(char code)
		{
			var glyph = fontRenderer.Render(code, fontHeight);
			if (texture == null) {
				CreateNewFontTexture();
			}
			if (glyph == null)
				return null;
			// We add 1px left and right padding to each char on the texture and also to the UV
			// so that chars will be blurred correctly after stretching or drawing to float position.
			// And we compensate this padding by ACWidth, so that the text will take the same space.
			const int padding = 1;
			// Space between characters on the texture
			const int spacing = 1;
			var paddedGlyphWidth = glyph.Width + padding * 2;
			if (position.X + paddedGlyphWidth + spacing >= texture.ImageSize.Width) {
				position.X = 0;
				position.Y += fontHeight + spacing;
			}
			if (position.Y + fontHeight + spacing >= texture.ImageSize.Height) {
				CreateNewFontTexture();
				position = IntVector2.Zero;
			}
			CopyGlyphToTexture(glyph, texture, position + new IntVector2(padding, 0));
			var fontChar = new FontChar {
				Char = code,
				UV0 = (Vector2)position / (Vector2)texture.ImageSize,
				UV1 = ((Vector2)position + new Vector2(paddedGlyphWidth, fontHeight)) / (Vector2)texture.ImageSize,
				ACWidths = glyph.ACWidths - Vector2.One * padding,
				Width = paddedGlyphWidth,
				Height = fontHeight,
				KerningPairs = glyph.KerningPairs,
				TextureIndex = textureIndex
			};
			position.X += paddedGlyphWidth + spacing;
			return fontChar;
		}

		private void CreateNewFontTexture()
		{
			texture = new DynamicTexture(CalcTextureSize());
			textureIndex = textures.Count;
			textures.Add(texture);
		}

		private Size CalcTextureSize()
		{
			const int glyphsPerTexture = 30;
			var glyphMaxArea = fontHeight * (fontHeight / 2);
			var size =
				CalcUpperPowerOfTwo((int)Math.Sqrt(glyphMaxArea * glyphsPerTexture))
				.Clamp(64, 1024);
			return new Size(size, size);
		}

		private static int CalcUpperPowerOfTwo(int x)
		{
			x--;
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);
			return (x + 1);
		}

		private static void CopyGlyphToTexture(FontRenderer.Glyph glyph, DynamicTexture texture, IntVector2 position)
		{
			var srcPixels = glyph.Pixels;
			if (srcPixels == null)
				return; // Invisible glyph
			var si = 0;
			var color = Color4.White;
			var dstPixels = texture.Data;
			for (int i = 0; i < glyph.Height; i++) {
				var di = (position.Y + glyph.VerticalOffset + i) * texture.ImageSize.Width + position.X;
				for (int j = glyph.Width; j > 0; j--) {
					color.A = srcPixels[si++];
					dstPixels[di++] = color;
				}
			}
			texture.Invalidated = true;
		}

		// Use inheritance instead of composition in a sake of performance
		private class DynamicTexture : Texture2D
		{
			public readonly Color4[] Data;
			public bool Invalidated;

			public DynamicTexture(Size size)
			{
				ImageSize = SurfaceSize = size;
				Data = new Color4[size.Width * size.Height];
			}

			public override uint GetHandle()
			{
				if (Invalidated) {
					LoadImage(Data, ImageSize.Width, ImageSize.Height);
					Invalidated = false;
				}
				return base.GetHandle();
			}
		}
	}
}
