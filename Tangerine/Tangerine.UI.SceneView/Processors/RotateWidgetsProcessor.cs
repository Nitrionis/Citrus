﻿using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
	
namespace Tangerine.UI.SceneView
{
	public class RotateWidgetsProcessor : ITaskProvider
	{
		SceneView sv => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				Quadrangle hull;
				Vector2 pivot;
				var widgets = Document.Current.SelectedNodes().Editable().OfType<Widget>();
				if (Utils.CalcHullAndPivot(widgets, sv.Scene, out hull, out pivot)) {
					for (int i = 0; i < 4; i++) {
						if (HitTestControlPoint(hull[i])) {
							Utils.ChangeCursorIfDefault(MouseCursor.Hand);
							if (sv.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Rotate(pivot);
							}
						}
					}
				}
				yield return null;
			}
		}

		bool HitTestControlPoint(Vector2 controlPoint)
		{
			return (controlPoint - sv.MousePosition).Length < 10 / sv.Scene.Scale.X;
		}

		IEnumerator<object> Rotate(Vector2 pivot)
		{
			sv.Input.CaptureMouse();
			Document.Current.History.BeginTransaction();
			try {
				var widgets = Document.Current.SelectedNodes().Editable().OfType<Widget>().ToList();
				foreach (var widget in widgets) {
					SetWidgetPivot(widget, pivot);
				}
				var rotations = widgets.Select(i => i.Rotation).ToList();
				float rotation = 0;
				var mousePos = sv.MousePosition;
				var t = sv.Scene.CalcTransitionToSpaceOf(Document.Current.Container.AsWidget);
				while (sv.Input.IsMousePressed()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
					var a = mousePos * t - pivot * t;
					var b = sv.MousePosition * t - pivot * t;
					mousePos = sv.MousePosition;
					if (a.Length > Mathf.ZeroTolerance && b.Length > Mathf.ZeroTolerance) {
						rotation += WrapAngle(b.Atan2Deg - a.Atan2Deg);
					}
					for (int i = 0; i < widgets.Count; i++) {
						SetWidgetRotation(widgets[i], rotations[i] + GetSnappedRotation(rotation));
					}
					yield return null;
				}
			} finally {
				sv.Input.ReleaseMouse();
				Document.Current.History.EndTransaction();
			}
		}

		static float WrapAngle(float angle)
		{
			if (angle > 180) {
				return angle - 360;
			}
			if (angle < -180) {
				return angle + 360;
			}
			return angle;
		}

		float GetSnappedRotation(float rotation)
		{
			if (sv.Input.IsKeyPressed(Key.Shift)) {
				return ((rotation / 15f).Round() * 15f).Snap(0);
			} else {
				return rotation.Snap(0);
			}
		}

		void SetWidgetPivot(Widget widget, Vector2 pivot)
		{
			var transform = sv.Scene.CalcTransitionToSpaceOf(widget);
			var newPivot = ((transform * pivot) / widget.Size).Snap(widget.Pivot);
			var deltaPos = Vector2.RotateDeg((newPivot - widget.Pivot) * (widget.Scale * widget.Size), widget.Rotation);
			var newPos = widget.Position + deltaPos.Snap(Vector2.Zero);
			Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Position), newPos);
			Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Pivot), newPivot);
		}

		void SetWidgetRotation(Widget widget, float rotation)
		{
			Core.Operations.SetAnimableProperty.Perform(widget, nameof(Widget.Rotation), rotation);
		}
	}
}