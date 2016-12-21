﻿using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class DragSplineTangentsProcessor : ITaskProvider
	{
		SceneView sv => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				var points = Document.Current.SelectedNodes().Editable().OfType<SplinePoint>();
				foreach (var point in points) {
					for (int i = 0; i < 2; i++) {
						var p = CalcTangentKnobPosition(point, i);
						if ((sv.MousePosition - p).Length < 5 / sv.Scene.Scale.X) {
							Utils.ChangeCursorIfDefault(MouseCursor.Hand);
							if (sv.Input.ConsumeKeyPress(Key.Mouse0)) {
								yield return Drag(point, i);
							}
						}
					}
				}
				yield return null;
			}
		}

		Vector2 CalcTangentKnobPosition(SplinePoint point, int index)
		{
			var matrix = (Document.Current.Container as Widget).CalcTransitionToSpaceOf(sv.Scene);
			var delta = (index == 0 ? -1 : 1) * 10 * point.TangentWeight * Vector2.CosSin(point.TangentAngle * Mathf.DegToRad);
			return matrix * (point.TransformedPosition + delta);
		}

		IEnumerator<object> Drag(SplinePoint point, int index)
		{
			sv.Input.CaptureMouse();
			Document.Current.History.BeginTransaction();
			try {
				var iniMousePos = sv.MousePosition;
				var matrix = sv.Scene.CalcTransitionToSpaceOf(Document.Current.Container as Widget);
				while (sv.Input.IsMousePressed()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
					var curMousePos = sv.MousePosition;
					if ((curMousePos - iniMousePos).Snap(Vector2.Zero) != Vector2.Zero) {
						var p = matrix * curMousePos;
						var o = point.TransformedPosition;
						var angle = (index == 0 ? o - p : p - o).Atan2Deg;
						var weight = (p - o).Length / 10;
						Core.Operations.SetAnimableProperty.Perform(point, nameof(SplinePoint.TangentAngle), angle);
						Core.Operations.SetAnimableProperty.Perform(point, nameof(SplinePoint.TangentWeight), weight);
					}
					yield return null;
				}
			} finally {
				sv.Input.ReleaseMouse();
				Document.Current.History.EndTransaction();
			}
		}
	}
}