﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lime
{

	public interface ICaretPresenter : IPresenter
	{
		Vector2 Position { get; set; }
		Color4 Color { get; set; }
		bool Visible { get; set; }
	}

	public interface ICaretParams
	{
		ICaretPresenter CaretPresenter { get; set; }
		float BlinkInterval { get; set; }
		bool FollowTextColor { get; set; }
	}

	public class CaretParams : ICaretParams
	{
		public ICaretPresenter CaretPresenter { get; set; }
		public float BlinkInterval { get; set; } = 0.5f;
		public bool FollowTextColor { get; set; }
	}

	public class VerticalLineCaret : CustomPresenter, ICaretPresenter
	{
		public Vector2 Position { get; set; }
		public Color4 Color { get; set; } = Color4.Black;
		public bool Visible { get; set; }
		public float Thickness { get; set; } = 1.0f;

		public override void Render(Node node)
		{
			if (Visible) {
				var text = (SimpleText)node;
				text.PrepareRendererState();
				Renderer.DrawLine(Position, Position + Vector2.Down * text.FontHeight, Color, Thickness);
			}
		}
	}

	public class CaretDisplay
	{
		private Widget container;
		private ICaretPosition caretPos;
		private ICaretParams caretParams;

		public CaretDisplay(Widget container, ICaretPosition caretPos, ICaretParams caretParams)
		{
			this.container = container;
			this.caretPos = caretPos;
			this.caretParams = caretParams;
			container.CompoundPostPresenter.Add(caretParams.CaretPresenter);
			container.Tasks.Add(CaretDisplayTask());
		}

		private IEnumerator<object> CaretDisplayTask()
		{
			var p = caretParams.CaretPresenter;
			var time = 0f;
			bool blinkOn = true;
			bool wasVisible = false;
			while (true) {
				if (caretPos.IsVisible) {
					time += Task.Current.Delta;
					if (time > caretParams.BlinkInterval && caretParams.BlinkInterval > 0f) {
						time = 0f;
						blinkOn = !blinkOn;
						Window.Current.Invalidate();
					}
					var newPos = caretPos.WorldPos;
					if (!p.Position.Equals(newPos) || !wasVisible) {
						p.Position = newPos;
						time = 0f;
						blinkOn = true;
						Window.Current.Invalidate();
					}
					p.Visible = blinkOn;
					if (caretParams.FollowTextColor) {
						p.Color = container.Color;
					}
				} else if (p.Visible) {
					p.Visible = false;
					Window.Current.Invalidate();
				}
				wasVisible = caretPos.IsVisible;
				yield return null;
			}
		}
	}

	public class SelectionParams
	{
		public Color4 Color { get; set; } = Color4.Yellow;
		public Color4 OutlineColor { get; set; } = Color4.Orange;
		public Thickness Padding { get; set; } = new Thickness(1f);
		public float OutlineThickness { get; set; } = 1f;
	}

	public class SelectionPresenter : CustomPresenter, IPresenter
	{
		private Widget container;
		private ICaretPosition selectionStart;
		private ICaretPosition selectionEnd;
		private SelectionParams selectionParams;

		public SelectionPresenter(
			Widget container, ICaretPosition selectionStart, ICaretPosition selectionEnd,
			SelectionParams selectionParams)
		{
			this.container = container;
			this.selectionStart = selectionStart;
			this.selectionEnd = selectionEnd;
			this.selectionParams = selectionParams;
			container.CompoundPresenter.Add(this);
		}

		private List<Rectangle> PrepareRows(Vector2 s, Vector2 e, float fh)
		{
			var rows = new List<Rectangle>();
			if (s.Y == e.Y) {
				rows.Add(new Rectangle(s, e + Vector2.Down * fh));
			} else { // Multi-line selection.
				rows.Add(new Rectangle(s, new Vector2(float.PositiveInfinity, s.Y + fh)));
				if (s.Y + fh < e.Y)
					rows.Add(new Rectangle(0, s.Y + fh, float.PositiveInfinity, e.Y));
				rows.Add(new Rectangle(new Vector2(0, e.Y), e + Vector2.Down * fh));
			}
			return rows;
		}

		public override void Render(Node node)
		{
			if (!selectionStart.IsVisible || !selectionEnd.IsVisible) return;

			var s = selectionStart.WorldPos;
			var e = selectionEnd.WorldPos;
			if (s == e) return;
			if (s.Y > e.Y || s.Y == e.Y && s.X > e.X) {
				var t = s;
				s = e;
				e = t;
			}
			var text = (SimpleText)node;
			text.PrepareRendererState();

			var th = selectionParams.OutlineThickness;
			var b = text.MeasureText().ShrinkedBy(new Thickness(th));
			var rows = PrepareRows(s, e, text.FontHeight).
				Select(r => Rectangle.Intersect(r.ExpandedBy(selectionParams.Padding), b)).ToList();

			foreach (var r in rows) {
				var r1 = r.ExpandedBy(new Thickness(th));
				Renderer.DrawRectOutline(r1.A, r1.B, selectionParams.OutlineColor, th);
			}
			foreach (var r in rows)
				Renderer.DrawRect(r.A, r.B, selectionParams.Color);
		}
	}

	public class UndoHistory<T> where T : IEquatable<T>
	{

		private List<T> queue = new List<T>();
		private int current;

		public int MaxDepth { get; set; }

		public void Add(T item)
		{
			if (queue.Count > 0 && item.Equals(queue[queue.Count - 1]))
				return;
			if (current < queue.Count)
				queue.RemoveRange(current, queue.Count - current);
			var overflow = queue.Count - MaxDepth + 1;
			if (MaxDepth > 0 && overflow > 0) {
				queue.RemoveRange(0, overflow);
				current -= overflow;
			}
			queue.Add(item);
			current = queue.Count;
		}

		public bool CanUndo() => current > 0;
		public bool CanRedo() => current < queue.Count - 1;

		public T Undo(T item)
		{
			if (!CanUndo())
				throw new InvalidOperationException();
			if (current == queue.Count && !item.Equals(queue[queue.Count - 1]))
				queue.Add(item);
			return queue[--current];
		}

		public T Redo()
		{
			if (!CanRedo())
				throw new InvalidOperationException();
			return queue[++current];
		}

		public void Clear()
		{
			queue.Clear();
			current = 0;
		}

		public T ClearAndRestore()
		{
			if (!CanUndo())
				throw new InvalidOperationException();
			var result = queue[0];
			queue.Clear();
			current = 0;
			return result;
		}
	}

	public interface IEditorParams
	{
		int MaxLength { get; set; }
		int MaxLines { get; set; }
		float MaxHeight { get; set; }
		int MaxUndoDepth { get; set; }
		bool UseSecureString { get; set; }
		char? PasswordChar { get; set; }
		float PasswordLastCharShowTime { get; set; }
		Predicate<string> AcceptText { get; set; }
		ScrollView Scroll { get; set; }
		bool AllowNonDisplayableChars { get; set; }
		float MouseSelectionThreshold { get; set; }
		Func<Vector2, Vector2> OffsetContextMenu { get; set; }

		bool IsAcceptableLength(int length);
		bool IsAcceptableLines(int lines);
		bool IsAcceptableHeight(float height);
	}

	public class EditorParams : IEditorParams
	{
		public int MaxLength { get; set; }
		public int MaxLines { get; set; }
		public float MaxHeight { get; set; }
		public int MaxUndoDepth { get; set; } = 100;
		public bool UseSecureString { get; set; }
		public char? PasswordChar { get; set; }
		public float PasswordLastCharShowTime { get; set; } =
#if WIN || MAC || MONOMAC
			0.0f;
#else
			1.0f;
#endif
		public Predicate<string> AcceptText { get; set; }
		public ScrollView Scroll { get; set; }
		public bool AllowNonDisplayableChars { get; set; }
		public float MouseSelectionThreshold { get; set; } = 3.0f;
		public Func<Vector2, Vector2> OffsetContextMenu { get; set; }

		public bool IsAcceptableLength(int length) => MaxLength <= 0 || length <= MaxLength;
		public bool IsAcceptableLines(int lines) => MaxLines <= 0 || lines <= MaxLines;
		public bool IsAcceptableHeight(float height) => MaxHeight <= 0 || height <= MaxHeight;

		public const NumberStyles numberStyle =
			NumberStyles.AllowDecimalPoint |
			NumberStyles.AllowLeadingSign;

		public static bool AcceptNumber(string s)
		{
			double temp;
			return s == "-" || Double.TryParse(s, numberStyle, CultureInfo.InvariantCulture, out temp);
		}
	}

}
