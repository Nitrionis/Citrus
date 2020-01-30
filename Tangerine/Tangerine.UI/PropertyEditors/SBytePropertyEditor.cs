using Lime;
using Tangerine.Core;
using Tangerine.Core.ExpressionParser;

namespace Tangerine.UI
{
	public class SBytePropertyEditor : CommonPropertyEditor<sbyte>
	{
		private NumericEditBox editor;

		public SBytePropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			editor = editorParams.NumericEditBoxFactory();
			editor.LayoutCell = new LayoutCell(Alignment.Center);
			EditorContainer.AddNode(editor);
			EditorContainer.AddNode(Spacer.HStretch());
			var current = CoalescedPropertyValue();
			editor.Submitted += text => SetComponent(text, current.GetValue());
			editor.AddChangeLateWatcher(current, v => editor.Text = v.IsDefined ? v.Value.ToString() : ManyValuesText);
			ManageManyValuesOnFocusChange(editor, current);
		}

		public void SetComponent(string text, CoalescedValue<sbyte> current)
		{
			if (Parser.TryParse(text, out double newValue)) {
				SetProperty((sbyte)newValue);
				editor.Text = ((sbyte)newValue).ToString();
			} else {
				editor.Text = current.IsDefined ? current.Value.ToString() : ManyValuesText;
			}
		}

		public override void Submit()
		{
			var current = CoalescedPropertyValue();
			SetComponent(editor.Text, current.GetValue());
		}
	}
}
