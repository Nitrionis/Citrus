using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	// Used to unify generic descendants of ExpandableProperty for type checking
	public interface IExpandablePropertyEditor
	{
		bool Expanded { get; set; }
	}

	public class ExpandablePropertyEditor<T> : CommonPropertyEditor<T>, IExpandablePropertyEditor
	{
		private bool expanded;
		public bool Expanded
		{
			get => expanded;
			set
			{
				expanded = value;
				ExpandButton.Expanded = value;
				ExpandableContent.Visible = value;
			}
		}
		public Widget ExpandableContent { get; }
		protected ThemedExpandButton ExpandButton { get; }

		public ExpandablePropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			ExpandableContent = new ThemedFrame {
				Padding = new Thickness(4),
				Layout = new VBoxLayout(),
				Visible = false
			};
			ExpandButton = new ThemedExpandButton {
				MinMaxSize = Vector2.One * 20f,
				LayoutCell = new LayoutCell(Alignment.LeftCenter)
			};
			ExpandButton.Clicked += () => Expanded = !Expanded;
			editorParams.InspectorPane.AddNode(ExpandableContent);
			ExpandableContent.AddChangeWatcher(() => EditorContainer.GloballyEnabled,
				enabled => ExpandableContent.Enabled = enabled);
			LabelContainer.Nodes.Insert(0, ExpandButton);
		}
	}
}
