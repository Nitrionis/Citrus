using System;
using Lime;
using ProtoBuf;

namespace Lime
{
	/// <summary>
	/// ����� �������
	/// </summary>
	[ProtoContract]
	public class SplinePoint : PointObject
	{
		[ProtoMember(1)]
		public bool Straight { get; set; }

		/// <summary>
		/// �� ���� ����������� ������� ����������� ����������� �������
		/// </summary>
		[ProtoMember(2)]
		public float TangentAngle { get; set; }

		/// <summary>
		/// �� ���� ����������� ������� ������ ����������� �������
		/// </summary>
		[ProtoMember(3)]
		public float TangentWeight { get; set; }

		/// <summary>
		/// ��������� ����� � ���������� �������. (0,0) - ����� ������� ����, (1,1) - ������ ������
		/// </summary>
		[ProtoMember(4)]
		public Vector2 Anchor { get; set; }
	}
}