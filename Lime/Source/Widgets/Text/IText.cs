using System;
using ProtoBuf;

namespace Lime
{
	/// <summary>
	/// Types of horizontal alignment.
	/// </summary>
	[ProtoContract]
	public enum HAlignment
	{
		[ProtoEnum]
		Left,
		[ProtoEnum]
		Center,
		[ProtoEnum]
		Right,
	}

	/// <summary>
	/// Types of vertical alignment.
	/// </summary>
	[ProtoContract]
	public enum VAlignment
	{
		[ProtoEnum]
		Top,
		[ProtoEnum]
		Center,
		[ProtoEnum]
		Bottom,
	}

	/// <summary>
	/// Types of text overflow handling.
	/// </summary>
	[ProtoContract]
	public enum TextOverflowMode
	{
		/// <summary>
		/// Carry overflowed text on the next line.
		/// </summary>
		[ProtoEnum]
		Default,

		/// <summary>
		/// Reduce font size until text is not overflowing.
		/// </summary>
		[ProtoEnum]
		Minify,

		/// <summary>
		/// Add ellipsis ("...") in place of overflowed part of text.
		/// </summary>
		[ProtoEnum]
		Ellipsis,

		/// <summary>
		/// Ignore text overflowing.
		/// </summary>
		[ProtoEnum]
		Ignore
	}

	public interface ICaretPosition
	{
		int Line { get; set; }
		int Pos { get; set; }
		int TextPos { get; set; }
		Vector2 WorldPos { get; set; }
		bool IsVisible { get; set; }
	}

	public delegate void TextProcessorDelegate(ref string text);

	public interface IText
	{
		/// <summary>
		/// Returns the text's bounding box.
		/// </summary>
		Rectangle MeasureText();
		void Invalidate();
		void SyncCaretPosition();

		bool Localizable { get; set; }
		string Text { get; set; }
		string DisplayText { get; }
		event TextProcessorDelegate TextProcessor;
		HAlignment HAlignment { get; set; }
		VAlignment VAlignment { get; set; }
		TextOverflowMode OverflowMode { get; set; }
		bool WordSplitAllowed { get; set; }
		bool TrimWhitespaces { get; set; }
	}
}
