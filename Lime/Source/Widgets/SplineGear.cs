using ProtoBuf;
using Yuzu;

namespace Lime
{
	/// <summary>
	/// ������ ��� ������� � ��� �������. ������������ ��� ������� ���������� �������� ������� �� �������
	/// </summary>
	[ProtoContract]
	public class SplineGear : Node
	{
		/// <summary>
		/// Id �������. ������ ������ �� ���� �����
		/// </summary>
		[ProtoMember(1)]
		[YuzuMember]
		public string WidgetId { get; set; }

		/// <summary>
		/// Id �������. ������ ������ �� ���� �����
		/// </summary>
		[ProtoMember(2)]
		[YuzuMember]
		public string SplineId { get; set; }

		/// <summary>
		/// ��������� ������� �� �������. 0 - ������ �������, 1 - �����
		/// </summary>
		[ProtoMember(3)]
		[YuzuMember]
		public float SplineOffset { get; set; }

		protected override void SelfLateUpdate(float delta)
		{
			if (Parent == null) {
				return;
			}
			var spline = Parent.Nodes.TryFind(SplineId) as Spline;
			var widget = Parent.Nodes.TryFind(WidgetId) as Widget;
			if (spline != null && widget != null) {
				float length = spline.CalcLengthRough();
				Vector2 point = spline.CalcPoint(SplineOffset * length);
				widget.Position = spline.CalcLocalToParentTransform().TransformVector(point);
				widget.Update(0);
			}
		}
	}
}