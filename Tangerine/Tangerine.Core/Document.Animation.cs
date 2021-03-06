using System;
using Lime;
using System.Collections.Generic;
using System.ComponentModel;
using Yuzu.Binary;

namespace Tangerine.Core
{
	public sealed partial class Document
	{
		private Dictionary<Animation, double> savedAnimationsTimes;

		public bool SlowMotion { get; set; }

		public static bool CacheAnimationsStates
		{
			get => CurrentFrameSetter.CacheAnimationsStates;
			set => CurrentFrameSetter.CacheAnimationsStates = value;
		}

		public static void SetCurrentFrameToNode(Animation animation, int frameIndex)
		{
			CurrentFrameSetter.SetCurrentFrameToNode(animation, frameIndex, CoreUserPreferences.Instance.AnimationMode);
		}

		private static void FastForwardToFrame(Animation animation, int frameIndex)
		{
			var node = animation.OwnerNode;
			node.SetTangerineFlag(TangerineFlags.IgnoreMarkers, true);
			try {
				CurrentFrameSetter.FastForwardToFrame(animation, frameIndex);
			} finally {
				node.SetTangerineFlag(TangerineFlags.IgnoreMarkers, false);
			}
		}

		public void TogglePreviewAnimation(bool triggerMarkersBeforeCurrentFrame = false)
		{
			if (PreviewAnimation) {
				PreviewAnimation = false;
				Animation.IsRunning = false;
				CurrentFrameSetter.StopAnimationRecursive(PreviewAnimationContainer);
				if (!CoreUserPreferences.Instance.StopAnimationOnCurrentFrame) {
					foreach ((var animation, var time) in savedAnimationsTimes) {
						animation.Time = CoreUserPreferences.Instance.AnimationMode && CoreUserPreferences.Instance.ResetAnimationsTimes ? 0 : time;
					}
					SetCurrentFrameToNode(Animation, PreviewAnimationBegin);
				}
				AudioSystem.StopAll();
				CurrentFrameSetter.CacheAnimationsStates = true;
				ForceAnimationUpdate();
				CurrentFrameSetter.CacheAnimationsStates = false;
			} else {
				SaveAnimationsTimes();
				foreach (var node in RootNode.Descendants) {
					if (node is ITangerinePreviewAnimationListener t) {
						t.OnStart();
					}
				}
				int savedAnimationFrame = AnimationFrame;
				PreviewAnimation = true;
				CurrentFrameSetter.CacheAnimationsStates = true;
				if (triggerMarkersBeforeCurrentFrame) {
					CurrentFrameSetter.SetCurrentFrameToNode(Animation, 0, animationMode: true);
				}
				Animation.IsRunning = PreviewAnimation;
				if (triggerMarkersBeforeCurrentFrame) {
					FastForwardToFrame(Animation, savedAnimationFrame);
				}
				CurrentFrameSetter.CacheAnimationsStates = false;
				PreviewAnimationBegin = savedAnimationFrame;
				PreviewAnimationContainer = Container;
			}
			Application.InvalidateWindows();
		}


		public void ForceAnimationUpdate()
		{
			SetCurrentFrameToNode(Current.Animation, Current.AnimationFrame);
		}

		private void SaveAnimationsTimes()
		{
			void Save(Node node)
			{
				foreach (var animation in node.Animations) {
					savedAnimationsTimes[animation] = animation.Time;
				}
			}
			savedAnimationsTimes = new Dictionary<Animation, double>();
			foreach (var node in Container.Descendants) {
				Save(node);
			}
			var currentNode = Container;
			do {
				Save(currentNode);
				currentNode = currentNode.Parent;
			} while (currentNode != RootNode.Parent);
		}

		private static class CurrentFrameSetter
		{
			[NodeComponentDontSerialize]
			private class AnimationsStatesComponent : NodeComponent
			{
				private struct AnimationState
				{
					public bool IsRunning;
					public double Time;
					public string AnimationId;
				}

				private AnimationState[] animationsStates;

				public int? GetColumn(string animationId)
				{
					foreach (var state in animationsStates) {
						if (state.AnimationId == animationId) {
							return AnimationUtils.SecondsToFrames(state.Time);
						}
					}
					return null;
				}

				public static void Create(Node node, bool initial = false)
				{
					var component = node.Components.Get<AnimationsStatesComponent>();
					if (component == null || component.animationsStates.Length != node.Animations.Count) {
						if (component != null) {
							node.Components.Remove(component);
						}
						component = new AnimationsStatesComponent {
							animationsStates = new AnimationState[node.Animations.Count]
						};
						node.Components.Add(component);
					}
					var i = 0;
					foreach (var animation in node.Animations) {
						var state = new AnimationState {
							IsRunning = !initial && animation.IsRunning,
							Time = animation.Time,
							AnimationId = animation.Id,
						};
						component.animationsStates[i] = state;
						i++;
					}
					foreach (var child in node.Nodes) {
						Create(child);
					}
				}

				internal static bool Restore(Node node)
				{
					var component = node.Components.Get<AnimationsStatesComponent>();
					if (component == null || component.animationsStates.Length != node.Animations.Count) {
						return false;
					}
					var i = 0;
					foreach (var animation in node.Animations) {
						var state = component.animationsStates[i];
						animation.IsRunning = state.IsRunning;
						animation.Time = state.Time;
						i++;
					}
					var result = true;
					foreach (var child in node.Nodes) {
						result &= Restore(child);
					}
					return result;
				}

				public static bool Exists(Node node)
				{
					return node.Components.Contains<AnimationsStatesComponent>();
				}

				public static void Remove(Node node)
				{
					node.Components.Remove<AnimationsStatesComponent>();
					foreach (var child in node.Nodes) {
						Remove(child);
					}
				}
			}

			const int OptimalRollbackForCacheAnimationsStates = 150;
			static bool cacheAnimationsStates;

			internal static bool CacheAnimationsStates
			{
				get { return cacheAnimationsStates; }
				set {
					cacheAnimationsStates = value;
					if (!cacheAnimationsStates && AnimationsStatesComponent.Exists(Current.Container)) {
						AnimationsStatesComponent.Remove(Current.Container);
					}
				}
			}

			internal static void SetCurrentFrameToNode(Animation animation, int frameIndex, bool animationMode)
			{
				Audio.GloballyEnable = false;
				try {
					bool movingBack;
					var doc = Current;
					var node = animation.OwnerNode;
					if (animation.IsLegacy && animationMode) {
						node.SetTangerineFlag(TangerineFlags.IgnoreMarkers, true);
						var cacheFrame = node.Components.Get<AnimationsStatesComponent>()?.GetColumn(animation.Id);
						// Terekhov Dmitry: First time cache creation that does not set IsRunning
						// Terekhov Dmitry: In order not to not reset other animations
						if (CacheAnimationsStates && !cacheFrame.HasValue) {
							if (CoreUserPreferences.Instance.ResetAnimationsTimes) {
								SetTimeRecursive(node, 0);
							} else {
								SetTime(node, animation.Id, 0);
							}
							animation.IsRunning = true;
							FastForwardToFrame(animation, frameIndex);
							AnimationsStatesComponent.Create(node, true);
							cacheFrame = frameIndex;
						}
						if (!cacheFrame.HasValue) {
							if (CoreUserPreferences.Instance.ResetAnimationsTimes) {
								SetTimeRecursive(node, 0);
							} else {
								SetTime(node, animation.Id, 0);
							}
						} else {
							// Terekhov Dmitry: In case we've created a new container that doesn't have a
							// cache component
							if (!AnimationsStatesComponent.Restore(node)) {
								AnimationsStatesComponent.Create(node);
							}
						}
						ClearParticlesRecursive(node);
						animation.IsRunning = true;
						if (cacheFrame.HasValue && ((movingBack = cacheFrame.Value > frameIndex) ||
							frameIndex > cacheFrame.Value + OptimalRollbackForCacheAnimationsStates * 2)) {
							AnimationsStatesComponent.Remove(node);
							if (movingBack) {
								SetTime(node, animation.Id, 0);
								StopAnimationRecursive(node);
								animation.IsRunning = true;
								FastForwardToFrame(animation, (frameIndex - OptimalRollbackForCacheAnimationsStates).Clamp(0, frameIndex));
							} else {
								FastForwardToFrame(animation, frameIndex); // Terekhov Dmitry: Optimization - FF from last saved position
							}
							AnimationsStatesComponent.Create(node);
						}
						FastForwardToFrame(animation, frameIndex);
						StopAnimationRecursive(node);
						node.SetTangerineFlag(TangerineFlags.IgnoreMarkers, false);

						// Force update to reset Animation.NextMarkerOrTriggerTime for parents.
						animation.Frame = doc.Animation.Frame;
						doc.RootNode.Update(0);
					} else {
						animation.Frame = frameIndex;
						node.Update(0);
						ClearParticlesRecursive(node);
					}
				} finally {
					Audio.GloballyEnable = true;
				}
			}

			private static void SafeUpdate(Animation animation, float delta)
			{
				var remainDelta = delta;
				do {
					delta = Mathf.Min(remainDelta, Application.MaxDelta);
					animation.OwnerNode.Update(delta);
					remainDelta -= delta;
				} while (remainDelta > 0f);
			}

			internal static void FastForwardToFrame(Animation animation, int frame)
			{
				// Try to decrease error in node.AnimationTime by call node.Update several times
				const float OptimalDelta = 10;
				float forwardDelta;
				do {
					forwardDelta = CalcDeltaToFrame(animation, frame);
					var delta = Mathf.Min(forwardDelta, OptimalDelta);
					SafeUpdate(animation, delta);
				} while (forwardDelta > OptimalDelta);
			}

			static float CalcDeltaToFrame(Animation animation, int frame)
			{
				var forwardDelta = AnimationUtils.SecondsPerFrame * frame - animation.Time;
				// Make sure that animation engine will invoke triggers on last frame
				forwardDelta += 0.00001;
				// Hack: CompatibilityAnimationEngine workaround
				if (Current.Format == DocumentFormat.Scene) {
					forwardDelta *= 2f;
				}
				return (float) forwardDelta;
			}

			internal static void SetTimeRecursive(Node node, double time)
			{
				void SetTime(Node n, double t)
				{
					foreach (var animation in n.Animations) {
						animation.Time = t;
					}
				}
				SetTime(node, time);
				foreach (var descendant in node.Descendants) {
					SetTime(descendant, time);
				}
			}

			internal static void SetTimeRecursive(Node node, string animationId, double time)
			{
				void SetTime(Node n, string id, double t)
				{
					if (n.Animations.TryFind(id, out var animation)) {
						animation.Time = t;
					}
				}
				SetTime(node, animationId, time);
				foreach (var descendant in node.Descendants) {
					SetTime(descendant, animationId, time);
				}
			}

			internal static void SetTime(Node node, string animationId, double time)
			{
				if (node.Animations.TryFind(animationId, out var animation)) {
					animation.Time = time;
				}
			}

			internal static void StopAnimationRecursive(Node node)
			{
				void StopAnimation(Node n)
				{
					foreach (var animation in n.Animations) {
						animation.IsRunning = false;
					}
				}
				StopAnimation(node);
				foreach (var descendant in node.Descendants) {
					StopAnimation(descendant);
				}
			}

			static void ClearParticlesRecursive(Node node)
			{
				if (node is ParticleEmitter) {
					var e = (ParticleEmitter)node;
					e.ClearParticles();
				}
				foreach (var child in node.Nodes) {
					ClearParticlesRecursive(child);
				}
			}

		}

	}
}
