﻿using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class CreateWidgetProcessor : ITaskProvider
	{
		SceneView sv => SceneView.Instance;

		public IEnumerator<object> Task()
		{
			while (true) {
				Type nodeType = null;
				if (CreateNodeRequestComponent.Consume<Widget>(sv.Components, out nodeType)) {
					yield return CreateWidgetTask(nodeType);
				}
				yield return null;
			}
		}

		IEnumerator<object> CreateWidgetTask(Type nodeType)
		{
			while (true) {
				if (sv.InputArea.IsMouseOver()) {
					Utils.ChangeCursorIfDefault(MouseCursor.Hand);
				}
				var container = Document.Current.Container as Widget;
				CreateNodeRequestComponent.Consume<Node>(sv.Components);
				if (sv.Input.WasMousePressed() && container != null) {
					sv.Input.CaptureMouse();
					sv.Input.ConsumeKey(Key.Mouse0);
					var t = sv.Scene.CalcTransitionToSpaceOf(container);
					var rect = new Rectangle(sv.MousePosition * t, sv.MousePosition * t);
					var presenter = new DelegatePresenter<Widget>(w => {
						w.PrepareRendererState();
						var t2 = container.CalcTransitionToSpaceOf(sv.Frame);
						DrawRectOutline(rect.A, (rect.A + rect.B) / 2, t2);
						DrawRectOutline(rect.A, rect.B, t2);
					});
					sv.Frame.CompoundPostPresenter.Add(presenter);
					while (sv.Input.IsMousePressed()) {
						rect.B = sv.MousePosition * t;
						CommonWindow.Current.Invalidate();
						yield return null;
					}
					sv.Input.ReleaseMouse();
					sv.Frame.CompoundPostPresenter.Remove(presenter);
					try {
						var widget = (Widget)Core.Operations.CreateNode.Perform(nodeType);
						Core.Operations.SetProperty.Perform(widget, nameof(Widget.Size), rect.B - rect.A);
						Core.Operations.SetProperty.Perform(widget, nameof(Widget.Position), rect.A + widget.Size / 2);
						Core.Operations.SetProperty.Perform(widget, nameof(Widget.Pivot), Vector2.Half);
					} catch (InvalidOperationException e) {
						AlertDialog.Show(e.Message);
					}
					break;
				}
				yield return null;
			}
			Utils.ChangeCursorIfDefault(MouseCursor.Default);
		}

		static void DrawRectOutline(Vector2 a, Vector2 b, Matrix32 t)
		{
			var c = ColorTheme.Current.SceneView.MouseSelection;
			Renderer.DrawLine(a * t, new Vector2(b.X, a.Y) * t, c, 1, LineCap.Square);
			Renderer.DrawLine(new Vector2(b.X, a.Y) * t, b * t, c, 1, LineCap.Square);
			Renderer.DrawLine(b * t, new Vector2(a.X, b.Y) * t, c, 1, LineCap.Square);
			Renderer.DrawLine(new Vector2(a.X, b.Y) * t, a * t, c, 1, LineCap.Square);
		}
	}
}
