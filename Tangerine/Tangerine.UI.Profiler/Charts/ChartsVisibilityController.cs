using System.Collections.ObjectModel;
using Lime;

namespace Tangerine.UI.Charts
{
	/// <summary>
	/// Widget for enabling and disabling the visibility of charts.
	/// </summary>
	internal class ChartsVisibilityController : Widget
	{
		private readonly ThemedColorCheckBox[] checkboxes;

		/// <summary>
		/// Collection of checkboxes for each chart.
		/// </summary>
		public readonly ReadOnlyCollection<ThemedColorCheckBox> Checkboxes;

		public ChartsVisibilityController(IChartsGroup[] groups)
		{
			checkboxes = new ThemedColorCheckBox[groups.Length];
			Checkboxes = new ReadOnlyCollection<ThemedColorCheckBox>(checkboxes);
			Layout = new VBoxLayout();
			int checkboxIndex = 0;
			foreach (var group in groups) {
				int groupFirstCheckBoxIndex = checkboxIndex;
				foreach (var chart in group.Charts) {
					var color = group.Colors[chart.ColorIndex];
					var checkBox = new ThemedColorCheckBox(color, Color4.Black);
					var chartVariableClone = chart;
					var groupVariableClone = group;
					checkBox.Clicked += () => {
						groupVariableClone.SetVisibleFor(chartVariableClone, !chart.Visible);
					};
					AddNode(checkBox);
					checkboxes[checkboxIndex++] = checkBox;
				}
				group.ChartVisibleChanged += (chart) => {
					int index = groupFirstCheckBoxIndex + chart.SlotIndex;
					checkboxes[index].Checked = chart.Visible;
				};
			}
		}
	}
}
