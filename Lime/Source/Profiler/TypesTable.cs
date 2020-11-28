#if PROFILER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Yuzu;

namespace Lime.Profiler
{
	/// <summary>
	/// GUID-es from implementations of this interface are compatible with MaterialsTable.
	/// </summary>
	public interface IMaterialAlias {}

	/// <summary>
	/// GUID of this class can be used if it was not possible to get a link to the material.
	/// </summary>
	public sealed class NullMaterial : IMaterialAlias
	{
		public static readonly Guid Guid = typeof(NullMaterial).GUID;
	}

	/// <summary>
	/// Alias for frame buffers cleaning commands.
	/// </summary>
	public class ClearMaterial : IMaterialAlias
	{
		public static readonly Guid Guid = typeof(ClearMaterial).GUID;
	}

	public class TypesTable
	{
		[YuzuMember]
		protected readonly Dictionary<Guid, Type> guidToMaterials;

		public TypesTable() => guidToMaterials = new Dictionary<Guid, Type>();

		public void AddTypes(IEnumerable<Type> types)
		{
			foreach (var t in types) {
				if (!guidToMaterials.ContainsKey(t.GUID)) {
					guidToMaterials.Add(t.GUID, t);
				}
			}
		}

		public Type GetType(Guid guid)
		{
			if (guid == Guid.Empty) {
				return null;
			} else if (guidToMaterials.TryGetValue(guid, out var type)) {
				return type;
			} else {
				return TryFetchType(guid);
			}
		}

		protected virtual Type TryFetchType(Guid guid) => null;
	}

	internal class NativeTypesTable : TypesTable
	{
		private static readonly Regex ignoredAssemblies;
		private static readonly HashSet<Type> requestedTypes;

		/// <summary>
		/// You need to add custom types to this list.
		/// </summary>
		/// <remarks>
		/// This method can be called from any thread.
		/// </remarks>
		public static event Action<HashSet<Type>> FetchTypes;

		static NativeTypesTable()
		{
			ignoredAssemblies = new Regex(
				"^(System.*|mscorlib.*|Microsoft.*)",
				RegexOptions.Compiled
			);
			requestedTypes = new HashSet<Type>();
		}

		private static IEnumerable<Type> GetSupportedTypes() =>
			AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !ignoredAssemblies.IsMatch(a.FullName))
			.SelectMany(s => s.GetTypes())
			.Where(t => requestedTypes.Any(bt => bt.IsAssignableFrom(t)));

		//private static IEnumerable<Type> GetSupportedTypes() =>
		//	AppDomain.CurrentDomain.GetAssemblies()
		//	.Where(a => !ignoredAssemblies.IsMatch(a.FullName))
		//	.SelectMany(s => s.GetTypes())
		//	.Where(t => typeof(IMaterial).IsAssignableFrom(t) ||
		//				typeof(IMaterialAlias).IsAssignableFrom(t) ||
		//				typeof(RenderObject).IsAssignableFrom(t) ||
		//				typeof(Animation).IsAssignableFrom(t) ||
		//				typeof(BehaviorComponent).IsAssignableFrom(t) ||
		//				typeof(NodeProcessor).IsAssignableFrom(t));

		public NativeTypesTable()
		{
			foreach (var t in GetSupportedTypes()) {
				guidToMaterials.Add(t.GUID, t);
			}
		}

		protected override Type TryFetchType(Guid guid)
		{
			// Types can be loaded dynamically.
			FetchTypes?.Invoke(requestedTypes);
			AddTypes(GetSupportedTypes());
			if (guidToMaterials.TryGetValue(guid, out var type)) {
				return type;
			} else {
				throw new System.Exception($"Profiler: {nameof(NativeTypesTable)} unknown guid!");
			}
		}
	}
}

#endif // PROFILER
