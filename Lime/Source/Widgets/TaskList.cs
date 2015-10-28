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

	/// <summary>
	/// ������ ����� (������) (Tasks). ���������� ������������ List
	/// </summary>
	public class TaskList : List<Task>
	{
		[ThreadStatic]
		private static TaskList current;
		public static TaskList Current { 
			get { return current; } 
			private set { current = value; } 
		}

		public float Time;
		public float Delta;

		/// <summary>
		/// ������������� ��� �����
		/// </summary>
		public void Stop()
		{
			foreach (var i in ToArray()) {
				i.Dispose();
			}
			Clear();
		}

		/// <summary>
		/// ������������� �����, ��������������� ��������� �������
		/// </summary>
		public void Stop(Predicate<Task> match)
		{
			foreach (var i in ToArray()) {
				if (match(i)) {
					i.Dispose();
				}
			}
			RemoveAll(match);
		}

		/// <summary>
		/// ������������� ���� � ��������� ����� (��������� null, ����� ����� ���������� ������ � ����� null)
		/// </summary>
		public void StopByTag(object tag)
		{
			Stop(t => t.Tag == tag);
		}

		/// <summary>
		/// ��������� ���� � ����� ������
		/// </summary>
		public Task Add(IEnumerator<object> e, object tag = null)
		{
			var task = new Task(e, tag);
			Add(task);
			return task;
		}

		/// <summary>
		/// ��������� ���� � ����� ������
		/// </summary>
		public Task Add(Func<IEnumerator<object>> e, object tag = null)
		{
			return Add(e(), tag);
		}

		/// <summary>
		/// ������������� ���� � ��������� �����, ������� ����� � �����-�� ����� � ��������� �� � ����� ������
		/// </summary>
		public Task Replace(IEnumerator<object> e, object tag)
		{
			StopByTag(tag);
			return Add(e, tag);
		}

		private bool isBeingUpdated;

		/// <summary>
		/// ��������� ���� ��������� (������� ����������� ������, ��������� ����� � �.�.)
		/// </summary>
		/// <param name="delta">�����, ��������� � ����������� ������ Update � ��������</param>
		public void Update(float delta)
		{
			if (isBeingUpdated) {
				return;
			}
			isBeingUpdated = true;
			TaskList savedCurrent = Current;
			Current = this;
			try {
				Time += delta;
				Delta = delta;
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

		#region Utility methods

		/// <summary>
		/// ������ (����), ���������� ����� � ��������� ��������� � ������� ���������� ������� �������.
		/// �������� ���������� �� ��������� �� from � to
		/// </summary>
		/// <param name="timePeriod">������ ������� � ��������</param>
		/// <param name="from">��������� ��������</param>
		/// <param name="to">�������� ��������</param>
		public static IEnumerable<float> SinMotion(float timePeriod, float from, float to)
		{
			for (float t = 0; t < timePeriod; t += Current.Delta) {
				float v = Mathf.Sin(t / timePeriod * Mathf.HalfPi);
				yield return Mathf.Lerp(v, from, to);
			}
			yield return to;
		}

		/// <summary>
		/// ������ (����), ���������� ����� � ��������� ��������� � ������� ���������� ������� �������.
		/// �������� ���������� �� ������� ����������� ����� �� from � to
		/// </summary>
		/// <param name="timePeriod">������ ������� � ��������</param>
		/// <param name="from">��������� ��������</param>
		/// <param name="to">�������� ��������</param>
		public static IEnumerable<float> SqrtMotion(float timePeriod, float from, float to)
		{
			for (float t = 0; t < timePeriod; t += Current.Delta) {
				float v = Mathf.Sqrt(t / timePeriod);
				yield return Mathf.Lerp(v, from, to);
			}
			yield return to;
		}

		/// <summary>
		/// ������ (����), ���������� ����� � ��������� ��������� � ������� ���������� ������� �������.
		/// �������� ���������� �� from � to �������
		/// </summary>
		/// <param name="timePeriod">������ ������� � ��������</param>
		/// <param name="from">��������� ��������</param>
		/// <param name="to">�������� ��������</param>
		public static IEnumerable<float> LinearMotion(float timePeriod, float from, float to)
		{
			for (float t = 0; t < timePeriod; t += Current.Delta) {
				yield return Mathf.Lerp(t / timePeriod, from, to);
			}
			yield return to;
		}
		
		#endregion
	}
}
