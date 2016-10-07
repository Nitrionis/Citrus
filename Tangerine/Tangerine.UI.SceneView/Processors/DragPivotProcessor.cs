using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class DragPivotProcessor : IProcessor
	{
		SceneView sv => SceneView.Instance;

		public IEnumerator<object> Loop()
		{
			var widgets = Document.Current.SelectedNodes().Editable().OfType<Widget>();
			while (true) {
				Quadrangle hull;
				Vector2 pivot;
				if (
					sv.Input.IsKeyPressed(Key.Control) &&
					Utils.CalcHullAndPivot(widgets, sv.Scene, out hull, out pivot) &&
					(pivot - sv.MousePosition).Length < 10 / sv.Scene.Scale.X)
				{
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
					if (sv.Input.ConsumeKeyPress(Key.Mouse0)) {
						yield return Drag();
					}
				}
				yield return null;
			}
		}

		enum DragDirection
		{
			Any, Horizontal, Vertical
		}

		IEnumerator<object> Drag()
		{
			sv.Input.CaptureMouse();
			Document.Current.History.BeginTransaction();
			try {
				var iniMousePos = sv.MousePosition;
				var widgets = Document.Current.SelectedNodes().Editable().OfType<Widget>().ToList();
				var dragDirection = DragDirection.Any;
				var positions = widgets.Select(i => i.Position).ToList();
				var pivots = widgets.Select(i => i.Pivot).ToList();
				Quadrangle hull;
				Vector2 iniPivot;
				Utils.CalcHullAndPivot(widgets, sv.Scene, out hull, out iniPivot);
				while (sv.Input.IsMousePressed()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
					var curMousePos = sv.MousePosition;
					var shiftPressed = sv.Input.IsKeyPressed(Key.Shift);
					if (shiftPressed && dragDirection != DragDirection.Any) {
						if (dragDirection == DragDirection.Horizontal) {
							curMousePos.Y = iniMousePos.Y;
						} else if (dragDirection == DragDirection.Vertical) {
							curMousePos.X = iniMousePos.X;
						}
					}
					curMousePos = SnapMousePosToSpecialPoints(hull, curMousePos, iniMousePos - iniPivot);
					if (shiftPressed && dragDirection == DragDirection.Any && (curMousePos - iniMousePos).Length > 5) {
						var d = curMousePos - iniMousePos;
						dragDirection = d.X.Abs() > d.Y.Abs() ? DragDirection.Horizontal : DragDirection.Vertical;
					}
					for (int i = 0; i < widgets.Count; i++) {
						var widget = widgets[i];
						var transform = sv.Scene.CalcTransitionToSpaceOf(widget);
						var dragDelta = curMousePos * transform - iniMousePos * transform;
						var deltaPivot = dragDelta / widget.Size;
						var deltaPos = Vector2.RotateDeg(dragDelta * widget.Scale, widget.Rotation);
						deltaPivot = deltaPivot.Snap(Vector2.Zero);
						if (deltaPivot != Vector2.Zero) {
							Core.Operations.SetAnimableProperty.Perform(widget, "Pivot", pivots[i] + deltaPivot);
							Core.Operations.SetAnimableProperty.Perform(widget, "Position", positions[i] + deltaPos.Snap(Vector2.Zero));
						}
					}
					yield return null;
				}
			} finally {
				sv.Input.ReleaseMouse();
				Document.Current.History.EndTransaction();
			}
		}

		Vector2 SnapMousePosToSpecialPoints(Quadrangle hull, Vector2 mousePos, Vector2 correction)
		{
			var md = float.MaxValue;
			var mp = Vector2.Zero;
			foreach (var p in GetSpecialPoints(hull)) {
				var d = (mousePos - p).Length;
				if (d < md) {
					mp = p;
					md = d;
				}
			}
			var r = ((hull[0] - hull[2]).Length / 20) / sv.Scene.Scale.X;
			if (md < r) {
				return mp + correction;
			}
			return mousePos;
		}

		IEnumerable<Vector2> GetSpecialPoints(Quadrangle hull)
		{
			for (int i = 0; i < 4; i++) {
				yield return hull[i];
				yield return (hull[i] + hull[(i + 1) % 4]) / 2;
			}
			yield return (hull[0] + hull[2]) / 2;
		}
	}
}