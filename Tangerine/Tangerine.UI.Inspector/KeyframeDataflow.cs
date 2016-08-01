using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Inspector
{
	class KeyframeDataflow : IDataflow<IKeyframe>
	{
		readonly object obj;
		readonly string propertyName;

		int animatorCollectionVersion = int.MinValue;
		int animatorVersion = int.MinValue;
		int animationFrame = int.MinValue;
		string animationId;
		IAnimator animator;

		public IKeyframe Value { get; private set; }
		public bool GotValue { get; private set; }

		public KeyframeDataflow(object obj, string propertyName)
		{
			this.obj = obj;
			this.propertyName = propertyName;
		}

		public void Dispose() { }

		public static IDataflowProvider<R> GetProvider<R>(PropertyEditorContext context, Func<IKeyframe, R> selector)
		{
			IDataflowProvider<R> provider = null;
			foreach (var obj in context.Objects) {
				var p = new DataflowProvider<IKeyframe>(() => new KeyframeDataflow(obj, context.PropertyName)).Select(selector);
				provider = (provider == null) ? p : provider.SameOrDefault(p);
			}
			return provider;
		}

		public void Poll()
		{
			GotValue = false;
			var animable = obj as IAnimable;
			if (animable == null) {
				return;
			}
			if ((GotValue |= Document.Current.AnimationFrame != animationFrame)) {
				animationFrame = Document.Current.AnimationFrame;
			}
			if ((GotValue |= Document.Current.AnimationId != animationId)) {
				animationId = Document.Current.AnimationId;
				animatorCollectionVersion = int.MinValue;
			}
			if ((GotValue |= animatorCollectionVersion != animable.Animators.Version)) {
				animatorCollectionVersion = animable.Animators.Version;
				animator = FindAnimator();
			}
			if ((GotValue |= animator != null && animator.ReadonlyKeys.Version != animatorVersion)) {
				if (animator != null) {
					animatorVersion = animator.ReadonlyKeys.Version;
					Value = FindKeyframe();
				} else {
					animatorVersion = int.MinValue;
					Value = null;
				}
			}
		}

		IAnimator FindAnimator()
		{
			IAnimator animator;
			return (obj as IAnimable).Animators.TryFind(propertyName, out animator, Document.Current.AnimationId) ? animator : null;
		}

		IKeyframe FindKeyframe()
		{
			return FindAnimator()?.ReadonlyKeys.FirstOrDefault(k => k.Frame == Document.Current.AnimationFrame);
		}
	}
}