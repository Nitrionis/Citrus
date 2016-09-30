﻿using System;
using System.Collections.Generic;

namespace Lime
{
	/// <summary>
	/// Sequence of actions, based on IEnumerators.
	/// </summary>
	public class Task : IDisposable
	{
		[ThreadStatic]
		private static Task current;
		public static bool SkipFrameOnTaskCompletion;
		private Stack<IEnumerator<object>> stack = new Stack<IEnumerator<object>>();
		private WaitPredicate waitPredicate;
		private float waitTime;

		/// <summary>
		/// Invoked on every Task update. Useful for disposing of the Task on some condition.
		/// </summary>
		public Action Updating;
		
		public Task(IEnumerator<object> e, object tag = null)
		{
			Tag = tag;
			stack.Push(e);
		}
		
		/// <summary>
		/// Time delta since last Update.
		/// </summary>
		public float Delta { get; private set; }

		/// <summary>
		/// Total time accumulated via Update.
		/// </summary>
		public float LifeTime { get; private set; }

		public static Task Current { get { return current; } }

		public object Tag { get; set; }

		public bool Completed { get { return stack.Count == 0; } }

		public override string ToString()
		{
			return Completed ? "Completed" : stack.Peek().GetType().ToString();
		}

		/// <summary>
		/// Advances task to the next step of enumerator.
		/// </summary>
		public void Advance(float delta)
		{
			if (Completed) {
				return;
			}
			var savedCurrent = current;
			current = this;
			Delta = delta;
			LifeTime += delta;
			var e = stack.Peek();
			try {
				if (Updating != null) {
					Updating();
					if (Completed) {
						return;
					}
				}
				if (waitTime > 0) {
					waitTime -= delta;
					if (waitTime >= 0) {
						return;
					}
					Delta = -waitTime;
				}
				if (waitPredicate != null) {
					waitPredicate.TotalTime += delta;
					if (waitPredicate.Evaluate()) {
						return;
					}
					waitPredicate = null;
				}
				if (e.MoveNext()) {
					HandleYieldedResult(e.Current);
				} else if (!Completed) {
					stack.Pop();
					if (!SkipFrameOnTaskCompletion && !Completed) {
						Advance(0);
					}
				}
			} finally {
				current = savedCurrent;
			}
		}

		/// <summary>
		/// Exits from all IEnumerators, sets Updating to null.
		/// </summary>
		public void Dispose()
		{
			while (stack.Count > 0) {
				var e = stack.Pop();
				e.Dispose();
			}
			waitPredicate = null;
			Updating = null;
		}

		private void HandleYieldedResult(object result)
		{
			if (result == null) {
				waitTime = 0;
			} else if (result is int) {
				waitTime = (int)result;
			} else if (result is float) {
				waitTime = (float)result;
			} else if (result is IEnumerator<object>) {
				stack.Push((IEnumerator<object>) result);
				Advance(0);
			} else if (result is WaitPredicate) {
				waitPredicate = (WaitPredicate) result;
			} else if (result is Node) {
				waitPredicate = WaitForAnimation((Node) result);
			} else if (result is IEnumerable<object>) {
				throw new InvalidOperationException("Use IEnumerator<object> instead of IEnumerable<object> for " + result);
			} else {
				throw new InvalidOperationException("Invalid object yielded " + result);
			}
		}

		/// <summary>
		/// Proceeds while specified predicate returns true.
		/// </summary>
		public static WaitPredicate WaitWhile(Func<bool> predicate)
		{
			return new BooleanWaitPredicate(predicate);
		}

		/// <summary>
		/// Proceeds while specified predicate returns true. Argument of the predicate is
		/// time, that accumulates on Advance.
		/// </summary>
		public static WaitPredicate WaitWhile(Func<float, bool> timePredicate)
		{
			return new TimeWaitPredicate(timePredicate);
		}

		/// <summary>
		/// Proceeds while specified node is running animation.
		/// </summary>
		public static WaitPredicate WaitForAnimation(Node node)
		{
			return new AnimationWaitPredicate(node);
		}

		/// <summary>
		/// Proceeds while there is no keystroke on the current window.
		/// </summary>
		public static WaitPredicate WaitForInput()
		{
			return InputWaitPredicate.Instance;
		}

		/// <summary>
		/// Proceeds asynchronously in separate thread. Returns null while specified action is incomplete.
		/// </summary>
		public static IEnumerator<object> ExecuteAsync(Action action)
		{
#if UNITY
			throw new NotImplementedException();
#else
			var t = new System.Threading.Tasks.Task(action);
			t.Start();
			while (!t.IsCompleted && !t.IsCanceled && !t.IsFaulted) {
				yield return null;
			}
#endif
		}
		
		/// <summary>
		/// Sets stopping condition for current task.
		/// </summary>
		public static void StopIf(Func<bool> pred)
		{
			Current.Updating = () => {
				if (pred()) {
					Current.Dispose();
				}
			};
		}

		/// <summary>
		/// Returns a sequence of numbers, interpolated as sine in specified time period.
		/// Advances by using Current.Delta.
		/// </summary>
		public static IEnumerable<float> SinMotion(float timePeriod, float from, float to)
		{
			for (float t = 0; t < timePeriod; t += Current.Delta) {
				float v = Mathf.Sin(t / timePeriod * Mathf.HalfPi);
				yield return Mathf.Lerp(v, from, to);
			}
			yield return to;
		}

		/// <summary>
		/// Returns a sequence of numbers, interpolated as square root in specified time period.
		/// Advances by using Current.Delta.
		/// </summary>
		public static IEnumerable<float> SqrtMotion(float timePeriod, float from, float to)
		{
			for (float t = 0; t < timePeriod; t += Current.Delta) {
				float v = Mathf.Sqrt(t / timePeriod);
				yield return Mathf.Lerp(v, from, to);
			}
			yield return to;
		}

		/// <summary>
		/// Returns a sequence of numbers, linear interpolated in specified time period.
		/// Advances by using Current.Delta.
		/// </summary>
		public static IEnumerable<float> LinearMotion(float timePeriod, float from, float to)
		{
			for (float t = 0; t < timePeriod; t += Current.Delta) {
				yield return Mathf.Lerp(t / timePeriod, from, to);
			}
			yield return to;
		}
	}
}
