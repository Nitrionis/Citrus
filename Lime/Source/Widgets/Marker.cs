using System;
using System.Text;
using ProtoBuf;

namespace Lime
{
	/// <summary>
	/// ���� �������� ��������
	/// </summary>
	[ProtoContract]
	public enum MarkerAction
	{
		/// <summary>
		/// ������ ��������
		/// </summary>
		[ProtoEnum]
		Play,

		/// <summary>
		/// ����� ��������
		/// </summary>
		[ProtoEnum]
		Stop,

		/// <summary>
		/// ������� �� ������ ������
		/// </summary>
		[ProtoEnum]
		Jump,

		/// <summary>
		/// ����������� �������
		/// </summary>
		[ProtoEnum]
		Destroy
	}

	/// <summary>
	/// ������ ��������
	/// </summary>
	[ProtoContract]
	public class Marker
	{
		/// <summary>
		/// �������� �������
		/// </summary>
		[ProtoMember(1)]
		public string Id { get; set; }

		/// <summary>
		/// ����� �����, �� ������� ���������� ������
		/// </summary>
		[ProtoMember(2)]
		public int Frame { get; set; }

		/// <summary>
		/// ����� �����, ������������ � ������������
		/// </summary>
		public int Time { get { return AnimationUtils.FramesToMsecs(Frame); } }

		/// <summary>
		/// ��� �������
		/// </summary>
		[ProtoMember(3)]
		public MarkerAction Action { get; set; }

		/// <summary>
		/// Id �������, �� ������� ����� ����������� ������� (������ ���� Action == MarkerAction.Jump)
		/// </summary>
		[ProtoMember(4)]
		public string JumpTo { get; set; }

		/// <summary>
		/// ������������ ��������, ����������� �������������. ����������, ����� ����� ��������� ���� ������
		/// </summary>
		public Action CustomAction { get; set; }

		internal Marker Clone()
		{
			return (Marker)MemberwiseClone();
		}

		public override string ToString()
		{
			return string.Format("{1} '{0}'", Id, Action);
		}
	}
}
