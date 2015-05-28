using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime.PopupMenu
{
	/// <summary>
	/// ������� ����������� ����
	/// </summary>
	public class MenuItem
	{
		/// <summary>
		/// ������ ��������� ����������� ���� � ��������
		/// </summary>
		public static float Height = 40;

		public Frame Frame = new Frame() { Tag = "$MenuItem.cs" };
		
		/// <summary>
		/// ����, �������� ����������� ���� �������
		/// </summary>
		public Menu Menu;

		public bool Visible = true;
	}
}
