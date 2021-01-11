#if PROFILER

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lime.Profiler.Contexts
{
	using TypesDictionary = Dictionary<int, string>;

	internal interface ITypeIdentifiersCache
	{
		void EnsureKeyValuePairFor(TypeIdentifier identifier);
		TypesDictionary FindAndGetTypeNames(IProfilerDatabase database);
		string GetTypeName(TypeIdentifier identifier, IProfilerDatabase database);
	}

	internal class TypeIdentifiersCache : ITypeIdentifiersCache
	{
		private static readonly TypeIdentifiersCache instance = new TypeIdentifiersCache();

		private readonly object accessLockObject;
		private readonly Regex ignoredAssemblies;
		private readonly TypesDictionary typesDictionary;

		private bool typesReloadRequired;

		private TypeIdentifiersCache()
		{
			accessLockObject = new object();
			ignoredAssemblies = new Regex(
				"^(System.*|mscorlib.*|Microsoft.*)",
				RegexOptions.Compiled
			);
			typesDictionary = new TypesDictionary();
		}

		public static void SafeAccess(Action<ITypeIdentifiersCache> action)
		{
			lock (instance.accessLockObject) {
				action.Invoke(instance);
			}
		}

		public void EnsureKeyValuePairFor(TypeIdentifier identifier) =>
			typesReloadRequired |= !identifier.IsEmpty && !typesDictionary.ContainsKey(identifier.Value);

		public TypesDictionary FindAndGetTypeNames(IProfilerDatabase database)
		{
			if (typesReloadRequired) {
				ReloadTypes(database);
			}
			return typesDictionary;
		}

		public string GetTypeName(TypeIdentifier identifier, IProfilerDatabase database)
		{
			string typeName = "Empty Identifier";
			if (!identifier.IsEmpty && !typesDictionary.TryGetValue(identifier.Value, out typeName)) {
				ReloadTypes(database);
				if (!typesDictionary.TryGetValue(identifier.Value, out typeName)) {
					throw new InvalidOperationException("Profiler: unknown identifier!");
				}
			}
			return typeName;
		}

		private void ReloadTypes(IProfilerDatabase database)
		{
			foreach (var t in GetTypes()) {
				if (database.NativeTypesTable.TryGetValue(t, out var identifier)) {
					if (!typesDictionary.ContainsKey(identifier.Value)) {
						typesDictionary.Add(identifier.Value, t.FullName);
					}
				}
			}
		}

		private IEnumerable<Type> GetTypes() =>
			AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !ignoredAssemblies.IsMatch(a.FullName))
			.SelectMany(s => s.GetTypes());
	}
}

#endif // PROFILER
