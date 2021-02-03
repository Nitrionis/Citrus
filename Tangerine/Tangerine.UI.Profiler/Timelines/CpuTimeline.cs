#if PROFILER

using System;
using System.Text.RegularExpressions;
using Lime.Profiler;
using Lime.Profiler.Contexts;

namespace Tangerine.UI.Timelines
{
	using Content = TimelineContent<CpuUsage, CpuTimelineContent.ItemLabel>;
	using Labels = TimelineLabels<CpuTimelineContent.ItemLabel>;
	using OwnersPool = RingPool<ReferenceTable.RowIndex>;
	
	internal class CpuTimeline : Timeline<CpuUsage, CpuTimelineContent.ItemLabel>
	{
		private const string EmptyOwnersText = "Empty_Owners";
		private const string ObjectNoNameText = "No_Object_Name";
		private const string EmptyOwnersListText = "Empty_Owners_List";
		
		private static readonly TimelineContent.Filter<CpuUsage> defaultFilter;

		private TimelineContent.Filter<CpuUsage> activeFilter;
		
		static CpuTimeline() => defaultFilter = (usage, pool, clipboard) => true;

		public CpuTimeline() : base(CreateContentBuilder(), new Labels())
		{
			activeFilter = defaultFilter;
		}

		public bool TrySetFilter(string expression)
		{
			if (string.IsNullOrEmpty(expression)) {
				activeFilter = defaultFilter;
			} else {
				Regex regexp = null;
				try {
					regexp = new Regex(expression);
				} catch (ArgumentException) {
					return false;
				}
				var mode = GetMode(expression);
				activeFilter = CreateFilter(mode, regexp);
				// todo create rebuild request
				throw new NotImplementedException();
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
		
		private static Content CreateContentBuilder()
		{
			// todo default spacing parameters
			throw new InvalidOperationException();
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