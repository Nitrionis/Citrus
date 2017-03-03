using System;
using System.Collections.Generic;
using Yuzu;

namespace Lime
{
	public class SimpleText : Widget, IText
	{
		private SpriteList spriteList;
		private SerializableFont font;
		private string text;
		private Rectangle extent;
		private float fontHeight;
		private float spacing;
		private HAlignment hAlignment;
		private VAlignment vAlignment;
		private Color4 textColor;
		private bool minSizeValid;
		private string displayText;
		private TextOverflowMode overflowMode;
		private bool wordSplitAllowed;
		private TextProcessorDelegate textProcessor;
		private ITexture texture;

		[YuzuMember]
		public override sealed ITexture Texture
		{
			get { return texture; }
			set
			{
				if (texture != value) {
					texture = value;
					Window.Current?.Invalidate();
				}
			}
		}

		public event TextProcessorDelegate TextProcessor
		{
			add
			{
				textProcessor += value;
				Invalidate();
			}
			remove
			{
				textProcessor -= value;
				Invalidate();
			}
		}

		[YuzuMember]
		public SerializableFont Font
		{
			get { return font; }
			set
			{
				if (value != font) {
					font = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		public override string Text
		{
			get { return text ?? ""; }
			set
			{
				if (value != text) {
					text = value;
					Invalidate();
				}
			}
		}

		public string DisplayText
		{
			get
			{
				if (displayText != null) return displayText;
				displayText = Localizable ? Text.Localize() : Text;
				if (textProcessor != null)
					textProcessor(ref displayText);
				return displayText;
			}
		}

		[YuzuMember]
		public float FontHeight
		{
			get { return fontHeight; }
			set
			{
				if (value != fontHeight) {
					fontHeight = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		public float Spacing
		{
			get { return spacing; }
			set
			{
				if (value != spacing) {
					spacing = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		public HAlignment HAlignment
		{
			get { return hAlignment; }
			set
			{
				if (value != hAlignment) {
					hAlignment = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		public VAlignment VAlignment
		{
			get { return vAlignment; }
			set
			{
				if (value != vAlignment) {
					vAlignment = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		public TextOverflowMode OverflowMode
		{
			get { return overflowMode; }
			set
			{
				if (overflowMode != value) {
					overflowMode = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		public bool WordSplitAllowed
		{
			get { return wordSplitAllowed; }
			set
			{
				if (wordSplitAllowed != value) {
					wordSplitAllowed = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		public Color4 TextColor
		{
			get { return textColor; }
			set { textColor = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="Lime.RichText"/> calculates MinSize and MaxSize automatically.
		/// </summary>
		public bool AutoSizeConstraints { get; set; }

		public override Vector2 MinSize
		{
			get
			{
				if (AutoSizeConstraints && !minSizeValid) {
					base.MinSize = CalcContentSize() + Padding;
					minSizeValid = true;
				}
				return base.MinSize;
			}
			set
			{
				if (!AutoSizeConstraints) {
					base.MinSize = value;
				}
			}
		}

		public override Vector2 MaxSize
		{
			get { return AutoSizeConstraints ? MinSize : base.MaxSize; }
			set
			{
				if (!AutoSizeConstraints) {
					base.MaxSize = value;
				}
			}
		}

		public bool TrimWhitespaces { get; set; }

		public ICaretPosition Caret { get; set; } = DummyCaretPosition.Instance;

		public event Action<string> Submitted;

		public bool Localizable { get; set; }

		public SimpleText()
		{
			Theme.Current.Apply(this);
		}

		public SimpleText(string text) : this()
		{
			Text = text;
		}

		void IText.Submit()
		{
			if (Submitted != null) {
				Submitted(Text);
			}
		}

		public bool CanDisplay(char ch)
		{
			return Font.Chars.Get(ch, fontHeight) != FontChar.Null;
		}

		public override Vector2 CalcContentSize()
		{
			return Renderer.MeasureTextLine(Font, DisplayText, FontHeight);
		}

		protected override void OnSizeChanged(Vector2 sizeDelta)
		{
			base.OnSizeChanged(sizeDelta);
			Invalidate();
		}

		public override void Render()
		{
			if (!IsAwoken) {
				return;
			}
			PrepareSpriteListAndSyncCaret();
			Renderer.Transform1 = LocalToWorldTransform;
			Renderer.Blending = GlobalBlending;
			Renderer.Shader = GlobalShader;
			spriteList.Render(GlobalColor * textColor);
		}

		void IText.SyncCaretPosition()
		{
			PrepareSpriteListAndSyncCaret();
		}

		private void PrepareSpriteListAndSyncCaret()
		{
			if (!Caret.IsValid) {
				spriteList = null;
			}
			PrepareSpriteListAndExtent();
		}

		private void PrepareSpriteListAndExtent()
		{
			if (spriteList != null) {
				return;
			}
			if (OverflowMode == TextOverflowMode.Minify) {
				var savedSpacing = spacing;
				var savedHeight = fontHeight;
				FitTextInsideWidgetArea();
				spriteList = new SpriteList();
				extent = RenderHelper(spriteList, Caret);
				spacing = savedSpacing;
				fontHeight = savedHeight;
			} else {
				spriteList = new SpriteList();
				extent = RenderHelper(spriteList, Caret);
			}
		}

		/// <summary>
		/// Gets the text's bounding box.
		/// </summary>
		public Rectangle MeasureText()
		{
			PrepareSpriteListAndExtent();
			return extent;
		}

		public override void StaticScale(float ratio, bool roundCoordinates)
		{
			fontHeight *= ratio;
			spacing *= ratio;
			base.StaticScale(ratio, roundCoordinates);
		}

		private static CaretPosition dummyCaret = new CaretPosition();

		/// <summary>
		/// Changes FontHeight and Spacing to make the text inside widget's area.
		/// </summary>
		public void FitTextInsideWidgetArea(float minFontHeight = 10)
		{
			var minH = minFontHeight;
			var maxH = FontHeight;
			if (maxH <= minH) {
				return;
			}
			var bestHeight = minH;
			var spacingKoeff = Spacing / FontHeight;
			while (maxH - minH > 1) {
				Rectangle rect = RenderHelper(null, dummyCaret);
				var fit = (rect.Width <= ContentWidth && rect.Height <= ContentHeight);
				if (fit) {
					minH = FontHeight;
					bestHeight = Mathf.Max(bestHeight, FontHeight);
				} else {
					maxH = FontHeight;
				}
				FontHeight = (minH + maxH) / 2;
				Spacing = FontHeight * spacingKoeff;
			}
			FontHeight = bestHeight.Floor();
			Spacing = bestHeight * spacingKoeff;
		}

		private Rectangle RenderHelper(SpriteList spriteList, ICaretPosition caret)
		{
			var lines = SplitText(DisplayText);
			if (TrimWhitespaces) {
				TrimLinesWhitespaces(lines);
			}
			var pos = new Vector2(0, Padding.Top + CalcVerticalTextPosition(lines));
			caret.StartSync();
			if (String.IsNullOrEmpty(DisplayText)) {
				pos.X = CalcXByAlignment(lineWidth: 0);
				caret.EmptyText(pos);
				return Rectangle.Empty;
			}
			caret.ClampTextPos(DisplayText.Length);
			caret.ClampLine(lines.Count);
			Rectangle rect = new Rectangle(Vector2.PositiveInfinity, Vector2.NegativeInfinity);
			int i = 0;
			foreach (var line in lines) {
				bool lastLine = ++i == lines.Count;
				caret.ClampCol(line.Length - (lastLine ? 0 : 1));
				float lineWidth = MeasureTextLine(line).X;
				pos.X = CalcXByAlignment(lineWidth);
				if (spriteList != null) {
					Renderer.DrawTextLine(
						Font, pos, line, Color4.White, FontHeight, 0, line.Length, spriteList, caret.Sync, -1, Texture);
				}
				Rectangle lineRect = new Rectangle(pos.X, pos.Y, pos.X + lineWidth, pos.Y + FontHeight);
				if (lastLine) {
					// There is no end-of-text character, so simulate it.
					caret.Sync(line.Length, new Vector2(lineRect.Right, lineRect.Top), Vector2.Down * fontHeight);
				}
				pos.Y += Spacing + FontHeight;
				caret.NextLine();
				rect = Rectangle.Bounds(rect, lineRect);
			}
			caret.FinishSync();
			return rect;
		}

		private static void TrimLinesWhitespaces(List<string> lines)
		{
			for (int i = 0; i < lines.Count; i++) {
				lines[i] = lines[i].Trim();
			}
		}

		private float CalcVerticalTextPosition(List<string> lines)
		{
			var totalHeight = CalcTotalHeight(lines.Count);
			if (VAlignment == VAlignment.Bottom) {
				return ContentSize.Y - totalHeight;
			} else if (VAlignment == VAlignment.Center) {
				return ((ContentSize.Y - totalHeight) * 0.5f).Round();
			}
			return 0;
		}

		private float CalcTotalHeight(int numLines)
		{
			return Math.Max(FontHeight * numLines + Spacing * (numLines - 1), FontHeight);
		}

		private float CalcXByAlignment(float lineWidth)
		{
			switch (HAlignment) {
				case HAlignment.Left:
					return Padding.Left;
				case HAlignment.Right:
					return Size.X - Padding.Right - lineWidth;
				case HAlignment.Center:
					return ((ContentSize.X - lineWidth) * 0.5f + Padding.Left).Round();
				default:
					throw new InvalidOperationException();
			}
		}

		private List<string> SplitText(string text)
		{
			var strings = new List<string>(text.Split('\n'));
			// Add linebreaks to make editor happy.
			for (int i = 0; i < strings.Count - 1; i++) {
				strings[i] += '\n';
			}
			if (OverflowMode == TextOverflowMode.Ignore) {
				return strings;
			}
			for (var i = 0; i < strings.Count; i++) {
				if (OverflowMode == TextOverflowMode.Ellipsis) {
					// Clipping the last line of the text.
					if (CalcTotalHeight(i + 2) > ContentHeight) {
						strings[i] = ClipLineWithEllipsis(strings[i]);
						while (strings.Count > i + 1) {
							strings.RemoveAt(strings.Count - 1);
						}
						break;
					}
				}
				// Trying to split long lines. If a line can't be split it gets clipped.
				while (MeasureTextLine(strings[i]).X > Math.Abs(ContentWidth)) {
					if (!TextLineSplitter.CarryLastWordToNextLine(strings, i, WordSplitAllowed, IsTextLinePartFitToWidth)) {
						if (OverflowMode == TextOverflowMode.Ellipsis) {
							strings[i] = ClipLineWithEllipsis(strings[i]);
						}
						break;
					}
				}
			}
			return strings;
		}

		private bool IsTextLinePartFitToWidth(string line, int start, int count)
		{
			return Renderer.MeasureTextLine(Font, line, FontHeight, start, count).X <= ContentWidth;
		}

		public Vector2 MeasureTextLine(string line)
		{
			return Renderer.MeasureTextLine(Font, line, FontHeight);
		}

		private string ClipLineWithEllipsis(string line)
		{
			var lineWidth = MeasureTextLine(line).X;
			if (lineWidth <= ContentWidth) {
				return line;
			}
			while (line.Length > 0 && lineWidth > ContentWidth) {
				lineWidth = MeasureTextLine(line + "...").X;
				line = line.Substring(0, line.Length - 1);
			}
			line += "...";
			return line;
		}

		public void Invalidate()
		{
			displayText = null;
			Caret.InvalidatePreservingTextPos();
			spriteList = null;
			minSizeValid = false;
			InvalidateParentConstraintsAndArrangement();
			Window.Current?.Invalidate();
		}

		public override Node Clone()
		{
			var clone = base.Clone() as SimpleText;
			clone.Caret = clone.Caret.Clone();
			return clone;
		}
	}
}
