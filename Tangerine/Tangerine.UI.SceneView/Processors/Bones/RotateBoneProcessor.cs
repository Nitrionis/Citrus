﻿using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class RotateBoneProcessor : ITaskProvider
	{
		SceneView sv => SceneView.Instance;


		public IEnumerator<object> Task()
		{
			while (true) {
				var bone = Document.Current.SelectedNodes().Editable().OfType<Bone>().FirstOrDefault();
				if (bone != null) {
					var entry = bone.Parent.AsWidget.BoneArray[bone.Index];
					var t = Document.Current.Container.AsWidget.CalcTransitionToSpaceOf(sv.Scene);
					var hull = BonePresenter.CalcHull(bone) * t;
					if (hull.Contains(sv.MousePosition) && !sv.Input.IsKeyPressed(Key.Control)) {
						Utils.ChangeCursorIfDefault(Cursors.Rotate);
						if (sv.Input.ConsumeKeyPress(Key.Mouse0)) {
							yield return Rotate(bone, entry);
						}
					}
				}
				yield return null;
			}
		}


		IEnumerator<object> Rotate(Bone bone, BoneArray.Entry entry)
		{
			sv.Input.CaptureMouse();
			Document.Current.History.BeginTransaction();
			try {
				float rotation = 0;
				var mousePos = sv.MousePosition;
				var initRotation = bone.Rotation;
				while (sv.Input.IsMousePressed()) {
					var t = sv.Scene.CalcTransitionToSpaceOf(Document.Current.Container.AsWidget);
					Utils.ChangeCursorIfDefault(Cursors.Rotate);
					var a = mousePos * t - entry.Joint;
					var b = sv.MousePosition * t - entry.Joint;
					mousePos = sv.MousePosition;
					if (a.Length > Mathf.ZeroTolerance && b.Length > Mathf.ZeroTolerance) {
						rotation += Mathf.Wrap180(b.Atan2Deg - a.Atan2Deg);
					}
					Core.Operations.SetAnimableProperty.Perform(bone, nameof(Bone.Rotation), initRotation + rotation);
					bone.Parent.Update(0);
					yield return null;
				}
				yield return null;
			} finally {
				sv.Input.ReleaseMouse();
				sv.Input.ConsumeKey(Key.Mouse0);
				Document.Current.History.EndTransaction();
			}
			yield return null;
		}
	}
}