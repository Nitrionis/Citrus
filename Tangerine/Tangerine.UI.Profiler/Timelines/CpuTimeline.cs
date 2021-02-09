#if PROFILER

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lime;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using Labels = TimelineLabels<CpuTimelineContent.ItemLabel>;
	using OwnersPool = RingPool<ReferenceTable.RowIndex>;
	using SpacingParameters = PeriodPositions.SpacingParameters;
	
	internal class CpuTimeline : Widget
	{
		private const string EmptyOwnersText = "Empty_Owners";
		private const string ObjectNoNameText = "No_Object_Name";
		private const string EmptyOwnersListText = "Empty_Owners_List";
		
		private readonly Timeline<CpuUsage, CpuTimelineContent.ItemLabel> timeline;
		private readonly List<OwnerDescription> cpuUsageOwnersList;
		private OwnersInfoCopyProcessor ownersInfoCopyProcessor;
		private readonly List<OwnerDescription> ownerHierarchyList;
		private HierarchyInfoCopyProcessor hierarchyInfoCopyProcessor;
		private readonly ThemedDropDownList owners;

		private readonly Vector4 defaultButtonColor = Color4.Black.Lighten(0.2f).ToVector4();
		private readonly Vector4 activeModeColor = Color4.Green.Darken(0.3f).ToVector4();

		public CpuTimeline()
		{
			cpuUsageOwnersList = new List<OwnerDescription>();
			ownerHierarchyList = new List<OwnerDescription>();
			var size = new Vector2(28, 22);
			var padding = new Thickness(3, 0);
			(ThemedButton, IconMaterial) CreateButton(string icon) {
				var material = new IconMaterial();
				return (new ThemedButton {
					MinMaxSize = size,
					LayoutCell = new LayoutCell(Alignment.Center),
					Nodes = {
						new Image(IconPool.GetTexture(icon)) {
							MinMaxSize = size,
							Size = size,
							Padding = padding,
							Material = material,
						}
					}
				}, material);
			}
			var timelineContent = new CpuTimelineContent(new SpacingParameters {
				MicrosecondsPerPixel = 1f,
				TimePeriodHeight = TimelineWidget.DefaultItemHeight,
				TimePeriodVerticalMargin = TimelineWidget.DefaultItemMargin
			});
			timeline = new Timeline<CpuUsage, CpuTimelineContent.ItemLabel>(timelineContent, new Labels()) {
				Anchors = Anchors.LeftRightTopBottom,
				Padding = new Thickness(0),
			};
			Layout = new VBoxLayout();
			Anchors = Anchors.LeftRight;
			Padding = new Thickness(0, 0, 4, 0);
			var (modeButton, modeMaterial) = CreateButton("Profiler.VerticalSplit");
			modeMaterial.Color = defaultButtonColor;
			modeButton.Clicked += () => {
				CpuTimelineContent.TimelineMode mode;
				if (modeMaterial.Color == activeModeColor) {
					modeMaterial.Color = defaultButtonColor;
					mode = CpuTimelineContent.TimelineMode.Default;
				} else {
					modeMaterial.Color = activeModeColor;
					mode = CpuTimelineContent.TimelineMode.BatchBreakReasons;
				}
				timeline.AddContentRebuildingRequest((waitingTask) =>
					timelineContent.RebuildAsync(waitingTask, mode));
			};
			var (zoomInButton, zoomInMaterial) = CreateButton("Profiler.ZoomIn");
			zoomInMaterial.Color = defaultButtonColor;
			var (zoomOutButton, zoomOutMaterial) = CreateButton("Profiler.ZoomOut");
			zoomOutMaterial.Color = defaultButtonColor;
			var (resetZoomButton, resetZoomMaterial) = CreateButton("Profiler.ResetSize");
			resetZoomMaterial.Color = defaultButtonColor;
			resetZoomButton.Clicked += () => timeline.ResetScale(timeline.ContentDuration);
			var (rectButton, rectMaterial) = CreateButton("Profiler.Rect");
			rectMaterial.Color = defaultButtonColor;
			rectButton.Clicked += () => {
				if (rectMaterial.Color == activeModeColor) {
					rectMaterial.Color = defaultButtonColor;
					// todo
				} else {
					rectMaterial.Color = activeModeColor;
					// todo
				}
			};
			owners = new ThemedDropDownList();
			owners.Items.Add(new CommonDropDownList.Item("Text###########"));
			owners.Items.Add(new CommonDropDownList.Item("Text###########"));
			AddNode(new Widget {
				Presenter = new WidgetFlatFillPresenter(Theme.Colors.ControlBorder),
				Layout = new HBoxLayout(),
				Padding = new Thickness(0),
				Anchors = Anchors.LeftRight,
				MaxWidth = float.PositiveInfinity,
				MinMaxHeight = 22,
				Height = 22,
				Nodes = {
					new ThemedSimpleText("CPU Timeline") {
						VAlignment = VAlignment.Center,
						Padding = new Thickness(4, 4, 2, 0)
					},
					modeButton,
					new ThemedSimpleText("Zoom") {
						VAlignment = VAlignment.Center,
						Padding = new Thickness(4, 4, 2, 0)
					},
					zoomInButton,
					zoomOutButton,
					resetZoomButton,
					new ThemedSimpleText("Selected Item") {
						VAlignment = VAlignment.Center,
						Padding = new Thickness(4, 4, 2, 0)
					},
					rectButton,
					new ThemedSimpleText("Time") {
						VAlignment = VAlignment.Center,
						Padding = new Thickness(4, 4, 2, 0)
					},
					new ThemedEditBox {
						Enabled = false,
						Text = "0.00013ms",
						MinMaxWidth = 64
					},
					new ThemedSimpleText("Reason") {
						VAlignment = VAlignment.Center,
						Padding = new Thickness(4, 4, 2, 0)
					},
					new ThemedEditBox {
						Enabled = false,
						Text = "ThemedEditBox"
					},
					new ThemedSimpleText("Owners") {
						VAlignment = VAlignment.Center,
						Padding = new Thickness(4, 4, 2, 0)
					},
					owners
				}
			});
			AddNode(timeline);
		}

		public void SetFrame(long frameIdentifier) => timeline.SetFrame(frameIdentifier);

		public bool TrySetFilter(string expression)
		{
			if (string.IsNullOrEmpty(expression)) {
				timeline.Filter = timeline.DefaultFilter;
			} else {
				Regex regexp = null;
				try {
					regexp = new Regex(expression);
				} catch (ArgumentException) {
					return false;
				}
				var mode = GetMode(expression);
				timeline.Filter = CreateFilter(mode, regexp);
			}
			return true;
		}
		
		private TimelineContent.Filter<CpuUsage> CreateFilter(FilterMode mode, Regex regexp)
		{
			return (CpuUsage usage, OwnersPool pool, FrameClipboard clipboard) => {
				switch (mode) {
					case FilterMode.ObjectName:
						return CheckOwner(usage.Owners, pool);
					case FilterMode.ReasonName:
						var name = ReasonsNames.TryGetName(usage.Reason);
						return name != null && regexp.IsMatch(name);
					case FilterMode.TypeName:
						return regexp.IsMatch(clipboard.TypesDictionary[usage.TypeIdentifier.Value]);
				}
				throw new InvalidOperationException();
				bool CheckOwner(Owners owners, OwnersPool ownersPool) {
					bool CheckObject(ReferenceTable.RowIndex rowIndex) {
						if (rowIndex.IsValid) {
							var description = clipboard.ReferenceTable[rowIndex.Value];
							return string.IsNullOrEmpty(description.ObjectName) ?
								regexp.IsMatch(ObjectNoNameText) :
								regexp.IsMatch(description.ObjectName);
						} else {
							return regexp.IsMatch(EmptyOwnersText);
						}
					}
					if (owners.IsEmpty) {
						return regexp.IsMatch(EmptyOwnersText);
					} else {
						if (owners.IsListDescriptor) {
							var list = owners.AsListDescriptor;
							if (list.IsNull) {
								return regexp.IsMatch(EmptyOwnersListText);
							} else {
								bool hasMatch = false;
								foreach (var rowIndex in ownersPool.Enumerate(list)) {
									hasMatch |= CheckObject(rowIndex);
								}
								return hasMatch;
							}
						} else {
							return CheckObject(owners.AsIndex);
						}
					}
				}
			};
		}

		private FilterMode GetMode(string regex)
		{
			string prefix = "(?# ";
			if (regex.StartsWith(prefix)) {
				var s = regex.Substring(prefix.Length, regex.IndexOf(')') - prefix.Length);
				return s == "type" ? FilterMode.TypeName : s == "reason" ? FilterMode.ReasonName : FilterMode.ObjectName;
			}
			return FilterMode.ObjectName;
		}

		private class OwnersInfoCopyProcessor : AsyncResponseProcessor<FrameDataResponse>
		{
			private readonly List<OwnerDescription> descriptions;
			private readonly int cpuUsageIndex;
			private volatile bool isCompleted;

			public bool IsCompleted => isCompleted;

			/// <param name="descriptions">Query results storage.</param>
			public OwnersInfoCopyProcessor(int cpuUsageIndex, List<OwnerDescription> descriptions)
			{
				this.descriptions = descriptions;
				this.cpuUsageIndex = cpuUsageIndex;
			}

			protected sealed override void ProcessResponseAsync(FrameDataResponse response)
			{
				descriptions.Clear();
				if (response.IsSucceed) {
					var table = response.Clipboard.ReferenceTable;
					var updateCpuUsages = response.Clipboard.UpdateCpuUsages;
					var renderCpuUsages = response.Clipboard.RenderCpuUsages;
					var (pool, owners) = updateCpuUsages.Count < cpuUsageIndex ?
						(response.Clipboard.UpdateOwnersPool, updateCpuUsages[cpuUsageIndex].Owners) :
						(response.Clipboard.RenderOwnersPool, renderCpuUsages[cpuUsageIndex].Owners);
					if (owners.IsEmpty) {
						descriptions.Add(new OwnerDescription {
							OwnerName = owners.IsListDescriptor ? "Empty Owners List" : "No Owner",
							TypeName = "No name",
							RowIndex = ReferenceTable.RowIndex.Invalid
						});
					} else if (!owners.IsListDescriptor) {
						var obj = table[owners.AsIndex.Value];
						descriptions.Add(new OwnerDescription {
							OwnerName = obj.ObjectName,
							TypeName = obj.TypeName,
							RowIndex = owners.AsIndex
						});
					} else {
						foreach (var rowIndex in pool.Enumerate(owners.AsListDescriptor)) {
							var obj = table[rowIndex.Value];
							descriptions.Add(new OwnerDescription {
								OwnerName = obj.ObjectName,
								TypeName = obj.TypeName,
								RowIndex = rowIndex
							});
						}
					}
				}
				isCompleted = true;
			}
		}

		private class HierarchyInfoCopyProcessor : AsyncResponseProcessor<FrameDataResponse>
		{
			private readonly List<OwnerDescription> descriptions;
			private readonly ReferenceTable.RowIndex leaf;
			private volatile bool isCompleted;

			public bool IsCompleted => isCompleted;

			/// <param name="descriptions">Query results storage.</param>
			public HierarchyInfoCopyProcessor(ReferenceTable.RowIndex leaf, List<OwnerDescription> descriptions)
			{
				this.descriptions = descriptions;
				this.leaf = leaf;
			}

			protected sealed override void ProcessResponseAsync(FrameDataResponse response)
			{
				descriptions.Clear();
				if (response.IsSucceed) {
					var table = response.Clipboard.ReferenceTable;
					var leaf = this.leaf;
					while (leaf.IsValid) {
						var obj = table[leaf.Value];
						descriptions.Add(new OwnerDescription {
							OwnerName = obj.ObjectName,
							TypeName = obj.TypeName,
							RowIndex = this.leaf
						});
						leaf = obj.ParentRowIndex;
					}
				}
				isCompleted = true;
			}
		}

		private struct OwnerDescription
		{
			public ReferenceTable.RowIndex RowIndex;
			public string OwnerName;
			public string TypeName;
		}

		private static class ReasonsNames
		{
			private static readonly string[] names;

			static ReasonsNames()
			{
				names = new string[CpuUsage.ReasonsBitMask + 1];
				foreach (var v in Enum.GetValues(typeof(CpuUsage.Reasons))) {
					uint value = (uint)v & CpuUsage.ReasonsBitMask;
					if (value != 0) {
						names[value] = ((CpuUsage.Reasons)value).ToString();
					}
				}
			}

			public static string TryGetName(CpuUsage.Reasons reasons)
			{
				uint value = (uint)reasons & CpuUsage.ReasonsBitMask;
				return value == 0 ? null : names[value];
			}
		}
		
		private enum FilterMode
		{
			ObjectName,
			TypeName,
			ReasonName,
		}
	}
}

#endif // PROFILER
