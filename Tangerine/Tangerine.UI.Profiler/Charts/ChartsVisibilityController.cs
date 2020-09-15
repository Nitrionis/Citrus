using System.Collections.ObjectModel;
using System.Linq;
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
			checkboxes = new ThemedColorCheckBox[groups.Sum(g => g.Charts.Count)];
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
					checkBox.Changed += (e) => {
						groupVariableClone.SetVisibleFor(chartVariableClone, e.Value);
					};
					AddNode(checkBox);
					checkboxes[checkboxIndex++] = checkBox;
				}
			}
		}
	}
}
