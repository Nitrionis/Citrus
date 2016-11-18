using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime
{
	public class TaskResult<T>
	{
		public T Value;
	}

	public class TaskResult<T1, T2>
	{
		public T1 Value1;
		public T2 Value2;
	}

	public class TaskList : List<Task>
	{
		[ThreadStatic]
		private static TaskList current;

		/// <summary>
		/// Currently processing TaskList.
		/// </summary>
		public static TaskList Current {
			get { return current; }
			private set { current = value; }
		}

		/// <summary>
		/// Stops all tasks.
		/// </summary>
		public void Stop()
		{
			foreach (var i in ToArray()) {
				i.Dispose();
			}
			Clear();
		}

		/// <summary>
		/// Stops all tasks that match the conditions defined by the specified predicate.
		/// </summary>
		public void Stop(Predicate<Task> match)
		{
			foreach (var i in ToArray()) {
				if (match(i)) {
					i.Dispose();
					Remove(i);
				}
			}
		}

		/// <summary>
		/// Stops all tasks with specified tag (null is also a tag).
		/// </summary>
		public void StopByTag(object tag)
		{
			Stop(t => t.Tag == tag);
		}

		/// <summary>
		/// Adds task to the end of this list.
		/// </summary>
		public Task Add(IEnumerator<object> e, object tag = null)
		{
			var task = new Task(e, tag);
			Add(task);
			return task;
		}

		public Task AddLoop(Action action, object tag = null)
		{
			return Add(ActionLoop(action));
		}

		private static IEnumerator<object> ActionLoop(Action action)
		{
			while (true) {
				action();
				yield return null;
			}
		}

		public static IEnumerator<object> SequenceEnumerator(params IEnumerator<object>[] args)
		{
			return args.Cast<object>().GetEnumerator();
		}

		public Task AddSequence(params IEnumerator<object>[] args)
		{
			var task = new Task(SequenceEnumerator(args), null);
			Add(task);
			return task;
		}

		/// <summary>
		/// Adds task to the end of this list.
		/// </summary>
		public Task Add(Func<IEnumerator<object>> e, object tag = null)
		{
			return Add(e(), tag);
		}

		private bool isBeingUpdated;

		/// <summary>
		/// Advances tasks by provided delta and removes completed ones.
		/// </summary>
		/// <param name="delta">Time delta since last Update.</param>
		public void Update(float delta)
		{
			if (isBeingUpdated) {
				return;
			}
			isBeingUpdated = true;
			TaskList savedCurrent = Current;
			Current = this;
			try {
				for (int i = 0; i < Count; ) {
					var task = this[i];
					if (task.Completed) {
						Remove(task);
					} else {
						task.Advance(delta);
						i++;
					}
				}
			} finally {
				isBeingUpdated = false;
				Current = savedCurrent;
			}
		}
	}
}
