using ProtoBuf;

namespace Lime
{
	/// <summary>
	/// ������ ������������ ����� ������� � ��������� �������� ������
	/// </summary>
	public enum KeyFunction
	{
		/// <summary>
		/// �������� ������������
		/// </summary>
		Linear,

		/// <summary>
		/// �������� �� ���������������, ��� ������ ����������, ���� �� ����� ��������� ��������� �������� ����
		/// </summary>
		Steep,

		/// <summary>
		/// ������������ �� �������. ������� ������ �������� ��������� ��������
		/// </summary>
		Spline,

		/// <summary>
		/// ���������� ������������ �� �������, �� ��� ����������� �������� ���� ���� ����� ������� ��������
		/// ��-�� ����, �������� �����������
		/// </summary>
		ClosedSpline
	}

	/// <summary>
	/// ��������� ��������� �����
	/// </summary>
	public interface IKeyframe
	{
		/// <summary>
		/// ����� �����, �� ������� ���������� �������� ����
		/// </summary>
		int Frame { get; set; }

		/// <summary>
		/// ������ ������������ ����� ������� � ��������� �������� ������
		/// </summary>
		KeyFunction Function { get; set; }

		/// <summary>
		/// �������� ��������
		/// </summary>
		object Value { get; set; }

		IKeyframe Clone();
	}

	/// <summary>
	/// �������� ����� (�����) ������������ ��� �������� ������� ��������.
	/// �������� ���� ��������������� �� ������������ ����� � ������ �������� ��������.
	/// ��� �������� ������ 2 ����� ������� �������� ����� � ���������� �������� ����� ���� � ����������� �� �������� �����
	/// </summary>
	/// <typeparam name="T">��� ��������, ����������� �������� ������</typeparam>
	[ProtoContract]
	public class Keyframe<T> : IKeyframe
	{
		/// <summary>
		/// ����� �����, �� ������� ���������� �������� ����
		/// </summary>
		[ProtoMember(1)]
		public int Frame { get; set; }

		/// <summary>
		/// ������ ������������ ����� ������� � ��������� �������� ������
		/// </summary>
		[ProtoMember(2)]
		public KeyFunction Function { get; set; }

		/// <summary>
		/// �������� ��������
		/// </summary>
		[ProtoMember(3)]
		public T Value;

		object IKeyframe.Value
		{
			get { return (object)this.Value; }
			set { this.Value = (T)value; }
		}

		public Keyframe() { }

		/// <summary>
		/// �����������
		/// </summary>
		/// <param name="frame">����� �����, �� ������� ���������� �������� ����</param>
		/// <param name="value">�������� ��������</param>
		/// <param name="function">������ ������������ ����� ������� � ��������� �������� ������</param>
		public Keyframe(int frame, T value, KeyFunction function)
		{			
			this.Frame = frame;
			this.Value = value;
			this.Function = function;
		}

		/// <summary>
		/// �����������
		/// </summary>
		/// <param name="frame">����� �����, �� ������� ���������� �������� ����</param>
		/// <param name="value">�������� ��������</param>
		public Keyframe(int frame, T value)
		{
			this.Frame = frame;
			this.Value = value;
		}

		/// <summary>
		/// ������� ���� ��������� �����
		/// </summary>
		public Keyframe<T> Clone()
		{
			return new Keyframe<T>() {
				Frame = Frame,
				Function = Function,
				Value = Value
			};
		}

		IKeyframe IKeyframe.Clone()
		{
			return Clone();
		}
	}
}
