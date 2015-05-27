using System;
using System.Collections.Generic;
using System.Text;
using Lime;
using ProtoBuf;

namespace Lime
{
	/// <summary>
	/// �������� ������
	/// </summary>
	[ProtoContract]
	[ProtoInclude(101, typeof(SplinePoint))]
	[ProtoInclude(102, typeof(DistortionMeshPoint))]
	public class PointObject : Node
	{
		private Vector2 position;

		/// <summary>
		/// ������� �������. ����� ���� ������������ ��� � ��������, ��� � � ��������������� �������� (��� ������, ������� �� ������-����������)
		/// </summary>
		[ProtoMember(1)]
		public Vector2 Position { get { return position; } set { position = value; } }

		/// <summary>
		/// ���������� X (���������� Position.X)
		/// </summary>
		public float X { get { return position.X; } set { position.X = value; } }

		/// <summary>
		/// ���������� Y (���������� Position.Y)
		/// </summary>
		public float Y { get { return position.Y; } set { position.Y = value; } }

		/// <summary>
		/// ���� ������ (������������ ��� �������� ��������)
		/// </summary>
		[ProtoMember(2)]
		public SkinningWeights SkinningWeights { get; set; }
	}
}
