using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime.PopupMenu
{
	/// <summary>
	/// ������� ����������� ���� � ���� ��������� ������
	/// </summary>
	public class StringItem : MenuItem
	{
		private const int MaxButtonIdFromTextLength = 16;
		MenuButton button;

		/// <summary>
		/// ��������, ������������ ��� ����� �� ���� �������
		/// </summary>
		public Action Activated;

		/// <summary>
		/// ��������� ����
		/// </summary>
		public Menu Submenu;

		/// <summary>
		/// ������������ �����
		/// </summary>
		public string Text
		{
			get { return button.Text; }
			set { button.Text = value; }
		}

		/// <summary>
		/// �����������
		/// </summary>
		/// <param name="text">������������ �����</param>
		/// <param name="activated">��������, ������������ ��� ����� �� ���� �������</param>
		public StringItem(string text, Action activated = null)
		{
			Activated = activated;
			Setup(text);
		}

		/// <summary>
		/// �����������
		/// </summary>
		/// <param name="text">������������ �����</param>
		/// <param name="iconPath">���� � ����� �����������, ������� ����� � �������� ������ (���� ������������ ����� Data �������)</param>
		/// <param name="activated">��������, ������������ ��� ����� �� ���� �������</param>
		public StringItem(string text, string iconPath, Action activated = null)
		{
			Activated = activated;
			Setup(text);
			button.ImagePath = iconPath;
		}

		/// <summary>
		/// �����������
		/// </summary>
		/// <param name="text">������������ �����</param>
		/// <param name="submenu">��������� ����</param>
		public StringItem(string text, Menu submenu)
		{
			Submenu = submenu;
			Setup(text);
		}

		private void Setup(string text)
		{
			button = new MenuButton();
			button.ArrowVisible = Submenu != null;
			Text = text;
			Frame.AddNode(new ExpandSiblingsToParent());
			Frame.AddNode(button);
			button.Clicked += OnClick;
			button.Id = text.Replace(' ', '_');
			if (button.Id.Length > MaxButtonIdFromTextLength) {
				button.Id = button.Id.Substring(0, MaxButtonIdFromTextLength);
			}
		}

		void OnClick()
		{
			if (Submenu != null) {
				Submenu.Show();
				Submenu.Scale = Menu.Scale;
				Submenu.Frame.X += 50 * Menu.Scale.X;
				Submenu.Hidden += Menu.Hide;
			} else {
				Menu.Hide();
			}
			if (Activated != null) {
				Activated();
			}
		}
	}
}
