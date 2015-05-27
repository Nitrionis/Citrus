using System;
using System.Collections;
using System.Collections.Generic;
using ProtoBuf;

namespace Lime
{
	/// <summary>
	/// ��������� �������� ��������
	/// </summary>
	[ProtoContract]
	public class MarkerCollection : List<Marker>
	{
		public MarkerCollection() { }
		public MarkerCollection(int capacity) 
			: base(capacity)
		{ }

		internal static MarkerCollection DeepClone(MarkerCollection source)
		{
			var result = new MarkerCollection(source.Count);
			foreach (var marker in source) {
				result.Add(marker.Clone());
			}
			return result;
		}
		
		/// <summary>
		/// ���� ������ � ��������� Id. ���� ������ ������� ���, ���������� null
		/// </summary>
		public Marker TryFind(string id)
		{
			foreach (var marker in this) {
				if (marker.Id == id) {
					return marker;
				}
			}
			return null;
		}

		/// <summary>
		/// ���� ������ � ��������� Id. ���� ������ ������� ���, ���������� false
		/// </summary>
		/// <param name="id">Id �������</param>
		/// <param name="marker">����������, � ������� ����� ������� ���������</param>
		public bool TryFind(string id, out Marker marker)
		{
			marker = TryFind(id);
			return marker != null;
		}

		/// <summary>
		/// ���� ������ � ��������� Id. ���� ������ ������� ���, ���������� ����������
		/// </summary>
		/// <exception cref="Lime.Exception"/>
		public Marker this[string id]
		{
			get { return Find(id); }
		}

		/// <summary>
		/// ���� ������ � ��������� Id. ���� ������ ������� ���, ���������� ����������
		/// </summary>
		/// <exception cref="Lime.Exception"/>
		public Marker Find(string id)
		{
			var marker = TryFind(id);
			if (marker == null) {
				throw new Lime.Exception("Unknown marker '{0}'", id);
			}	
			return marker;
		}

		/// <summary>
		/// ���������� ������, ����������� �� ��������� �����. ���� ������� ���, ���������� null
		/// </summary>
		public Marker GetByFrame(int frame)
		{
			foreach (var marker in this) {
				if (marker.Frame == frame) {
					return marker;
				}
			}
			return null;
		}

		/// <summary>
		/// ��������� Stop-������ (������ ����� ��������)
		/// </summary>
		/// <param name="id">�������� �������</param>
		/// <param name="frame">����� �����, �� ������� ����� ���������� ������</param>
		public void AddStopMarker(string id, int frame)
		{
			Add(new Marker() { Id = id, Action = MarkerAction.Stop, Frame = frame });
		}

		/// <summary>
		/// ��������� Play-������ (������ ������ ��������)
		/// </summary>
		/// <param name="id">�������� �������</param>
		/// <param name="frame">����� �����, �� ������� ����� ���������� ������</param>
		public void AddPlayMarker(string id, int frame)
		{
			Add(new Marker() { Id = id, Action = MarkerAction.Play, Frame = frame });
		}
	}
}
