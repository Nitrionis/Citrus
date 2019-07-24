using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Lime;
using Newtonsoft.Json.Linq;

namespace Orange
{
	public interface IAtlasPackerMetadata
	{
		string Id { get; }
	}

	public interface IMenuItemMetadata
	{
		[DefaultValue("Unspecified label")]
		string Label { get; }

		[DefaultValue(int.MaxValue)]
		int Priority { get; }
	}

	public class OrangePlugin
	{
		[Import(nameof(Initialize), AllowRecomposition = true, AllowDefault = true)]
		public Action Initialize;

		[Import(nameof(BuildUI), AllowRecomposition = true, AllowDefault = true)]
		public Action<IPluginUIBuilder> BuildUI;

		[Import(nameof(Finalize), AllowRecomposition = true, AllowDefault = true)]
		public Action Finalize;

		[Import(nameof(GetRequiredAssemblies), AllowRecomposition = true, AllowDefault = true)]
		public Func<string[]> GetRequiredAssemblies;

		[ImportMany(nameof(AtlasPackers), AllowRecomposition = true)]
		public IEnumerable<Lazy<Func<string, List<TextureTools.AtlasItem>, int, int>, IAtlasPackerMetadata>> AtlasPackers { get; set; }

		[ImportMany(nameof(AfterAssetUpdated), AllowRecomposition = true)]
		public IEnumerable<Action<Lime.AssetBundle, CookingRules, string>> AfterAssetUpdated { get; set; }

		[ImportMany(nameof(AfterAssetsCooked), AllowRecomposition = true)]
		public IEnumerable<Action<string>> AfterAssetsCooked { get; set; }

		[Import(nameof(AfterBundlesCooked), AllowRecomposition = true, AllowDefault = true)]
		public Action<IReadOnlyCollection<string>> AfterBundlesCooked;

		[ImportMany(nameof(CommandLineArguments), AllowRecomposition = true)]
		public IEnumerable<Func<string>> CommandLineArguments { get; set; }

		[ImportMany(nameof(MenuItems), AllowRecomposition = true)]
		public IEnumerable<Lazy<Action, IMenuItemMetadata>> MenuItems { get; set; }

		/// <summary>
		/// Used with and as MenuItems but should return null on success or a textual info about error on error
		/// </summary>
		[ImportMany(nameof(MenuItemsWithErrorDetails), AllowRecomposition = true)]
		public IEnumerable<Lazy<Func<string>, IMenuItemMetadata>> MenuItemsWithErrorDetails { get; set; }

		[Import(nameof(TangerineProjectOpened), AllowRecomposition = true, AllowDefault = true)]
		public Action TangerineProjectOpened;

		[Import(nameof(TangerineProjectClosing), AllowRecomposition = true, AllowDefault = true)]
		public Action TangerineProjectClosing;
	}

	public static class PluginLoader
	{
		public static string CurrentPluginDirectory;
		public static OrangePlugin CurrentPlugin = new OrangePlugin();
		private static CompositionContainer compositionContainer;
		private static readonly AggregateCatalog catalog;
		private static readonly List<ComposablePartCatalog> registeredCatalogs = new List<ComposablePartCatalog>();
		private static readonly Regex ignoredAssemblies = new Regex(
			"^(Lime|System.*|mscorlib.*|Microsoft.*)",
			RegexOptions.Compiled
		);
		private const string PluginsField = "PluginAssemblies";
		private const string OrangeAndTangerineField = "OrangeAndTangerine";
		private const string OrangeField = "Orange";
		private const string TangerineField = "Tangerine";

		static PluginLoader()
		{
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
			catalog = new AggregateCatalog();
			RegisterAssembly(typeof(PluginLoader).Assembly);
			ResetPlugins();
		}

		private static void ResetPlugins()
		{
			catalog.Catalogs.Clear();
			foreach (var additionalCatalog in registeredCatalogs) {
				catalog.Catalogs.Add(additionalCatalog);
			}
			compositionContainer = new CompositionContainer(catalog);
			try {
				compositionContainer.ComposeParts(CurrentPlugin);
			} catch (CompositionException compositionException) {
				Console.WriteLine(compositionException.ToString());
			}
		}

		public static void RegisterAssembly(Assembly assembly)
		{
			registeredCatalogs.Add(new AssemblyCatalog(assembly));
			ResetPlugins();
		}

		public static void ScanForPlugins(string citrusProjectFile)
		{
			var pluginRoot = Path.ChangeExtension(citrusProjectFile, ".OrangePlugin");
#if DEBUG
			var pluginConfiguration = BuildConfiguration.Debug;
#else
			var pluginConfiguration = BuildConfiguration.Release;
#endif
			CurrentPluginDirectory = Path.Combine(pluginRoot, "bin", pluginConfiguration);
			CurrentPlugin?.Finalize?.Invoke();
			The.UI.DestroyPluginUI();
			CurrentPlugin = new OrangePlugin();
			ResetPlugins();
			try {
				The.Workspace.ProjectJson.JObject.TryGetValue(PluginsField, out var token);
				if (token == null) {
					throw new KeyNotFoundException($"Warning: Field '{PluginsField}' not found in {citrusProjectFile}");
				}
				(token as JObject).TryGetValue(OrangeAndTangerineField, out token);
				if (token == null) {
					throw new KeyNotFoundException($"Warning: Field '{OrangeAndTangerineField}' not found in '{PluginsField}' in {citrusProjectFile}");
				}
				var orangeAndTangerine = The.Workspace.ProjectJson.GetArray<string>($"{PluginsField}/{OrangeAndTangerineField}");
				foreach (var path in orangeAndTangerine) {
					TryLoadAssembly(path, pluginConfiguration);
				}
#if TANGERINE
				var tangerine = The.Workspace.ProjectJson.GetArray<string>($"{PluginsField}/{TangerineField}");
				if (tangerine != null) {
					foreach (var path in tangerine) {
						TryLoadAssembly(path, pluginConfiguration);
					}
					if (tangerine.Length > 0) {
						Console.WriteLine("Tangerine specific assemblies loaded successfully");
					}
				} else {
					Console.WriteLine($"WARNING: Field '{TangerineField}' not found in '{PluginsField}' in {citrusProjectFile}");
				}
#else
				var orange = The.Workspace.ProjectJson.GetArray<string>($"{PluginsField}/{OrangeField}");
				if (orange != null) {
					foreach (var path in orange) {
						TryLoadAssembly(path, pluginConfiguration);
					}
					if (orange.Length > 0) {
						Console.WriteLine("Orange specific assemblies loaded successfully");
					}
				} else {
					Console.WriteLine($"WARNING: Field '{OrangeField}' not found in '{PluginsField}' in {citrusProjectFile}");
				}
#endif
				ValidateComposition();
			} catch (BadImageFormatException e) {
				Console.WriteLine(e.Message);
			} catch (System.Exception e) {
				The.UI.ShowError(e.Message);
				Console.WriteLine(e.Message);
			}
			CurrentPlugin?.Initialize?.Invoke();
			var uiBuilder = The.UI.GetPluginUIBuilder();
			try {
				if (uiBuilder != null) {
					CurrentPlugin?.BuildUI?.Invoke(uiBuilder);
					The.UI.CreatePluginUI(uiBuilder);
				}
			} catch (System.Exception e) {
				Orange.UserInterface.Instance.ShowError($"Failed to build Orange Plugin UI with an error: {e.Message}\n{e.StackTrace}");
			}
			The.MenuController.CreateAssemblyMenuItems();
		}

		private static void TryLoadAssembly(string path, string pluginConfiguration)
		{
			if (!path.Contains("$(CONFIGURATION)")) {
				Console.WriteLine(
					"Warning: Using '$(CONFIGURATION)' instead of 'Debug' or 'Release' in dll path" +
					$" is strictly recommended ($(CONFIGURATION) line not found in {path}");
			}
			var assemblyPath = path.Replace("$(CONFIGURATION)", pluginConfiguration);
#if TANGERINE
			assemblyPath = assemblyPath.Replace("$(HOST_APPLICATION)", "Tangerine");
#else
			assemblyPath = assemblyPath.Replace("$(HOST_APPLICATION)", "Orange");
#endif
			var absPath = Path.Combine(The.Workspace.ProjectDirectory, assemblyPath);
			if (!File.Exists(absPath)) {
				throw new FileNotFoundException("File not found on attempt to import PluginAssemblies: " + absPath);
			}

			var domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
			if (!TryFindDomainAssembliesByPath(domainAssemblies, absPath, out var assembly)) {
				var assemblyName = AssemblyName.GetAssemblyName(absPath);
				TryFindDomainAssembliesByName(domainAssemblies, assemblyName.Name, out assembly);
			}
			try {
				if (assembly == null) {
					assembly = LoadAssembly(absPath);
				}
				catalog.Catalogs.Add(new AssemblyCatalog(assembly));
			}
			catch (ReflectionTypeLoadException e) {
				var msg = "Failed to import OrangePluginAssemblies: " + absPath;
				foreach (var loaderException in e.LoaderExceptions) {
					msg += $"\n{loaderException}";
				}
				throw new System.Exception(msg);
			}
			catch (System.Exception e) {
				var msg = $"Unhandled exception while importing OrangePluginAssemblies: {absPath}\n{e}";
				throw new System.Exception(msg);
			}
			resolvedAssemblies[assembly.GetName().Name] = assembly;
		}

		public static void AfterAssetUpdated(Lime.AssetBundle bundle, CookingRules cookingRules, string path)
		{
			foreach (var i in CurrentPlugin.AfterAssetUpdated) {
				i(bundle, cookingRules, path);
			}
		}

		public static void AfterAssetsCooked(string bundleName)
		{
			foreach (var i in CurrentPlugin.AfterAssetsCooked) {
				i(bundleName);
			}
		}

		public static void AfterBundlesCooked(IReadOnlyCollection<string> bundles)
		{
			CurrentPlugin.AfterBundlesCooked?.Invoke(bundles);
		}

		public static string GetCommandLineArguments()
		{
			string result = "";
			if (CurrentPlugin != null) {
				result = GetPluginCommandLineArgumets();
			}
			return result;
		}

		private static string GetPluginCommandLineArgumets()
		{
			return CurrentPlugin.CommandLineArguments.Aggregate("", (current, i) => current + i());
		}

		private static void ValidateComposition()
		{
			var exportedCount = catalog.Parts.SelectMany(p => p.ExportDefinitions).Count();
			var importedCount = 0;

			Func<MemberInfo, bool> isImportMember = (m) =>
				Attribute.IsDefined(m, typeof(ImportAttribute)) ||
				Attribute.IsDefined(m, typeof(ImportManyAttribute));

			foreach (
				var member in typeof(OrangePlugin).GetMembers()
					.Where(m => m is PropertyInfo || m is FieldInfo)
					.Where(m => isImportMember(m))
				) {
				if (member is PropertyInfo) {
					var property = member as PropertyInfo;
					if (property.PropertyType.GetInterfaces().Contains(typeof(IEnumerable))) {
						importedCount += ((ICollection)property.GetValue(CurrentPlugin)).Count;
					} else if (property.GetValue(CurrentPlugin) != null) {
						importedCount++;
					}
				} else if (member is FieldInfo) {
					var field = member as FieldInfo;
					if (field.FieldType.GetInterfaces().Contains(typeof(IEnumerable))) {
						importedCount += ((ICollection)field.GetValue(CurrentPlugin)).Count;
					} else if (field.GetValue(CurrentPlugin) != null ){
						importedCount++;
					}
				}
			}

			if (exportedCount != importedCount) {
				Console.WriteLine(
					$"WARNING: Plugin composition mismatch found.\nThe given assemblies defines [{exportedCount}] " +
					$"exports, but only [{importedCount}] has been imported.\nPlease check export contracts.\n");
			}
		}

		private static readonly Dictionary<string, Assembly> resolvedAssemblies = new Dictionary<string, Assembly>();
		public static IEnumerable<Type> EnumerateTangerineExportedTypes()
		{
			var requiredAssemblies = CurrentPlugin?.GetRequiredAssemblies;
			if (requiredAssemblies != null) {
				foreach (var name in requiredAssemblies()) {
					AssemblyResolve(null, new ResolveEventArgs(name, null));
				}
			}

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				var assemblyName = assembly.GetName().Name;
				if (ignoredAssemblies.IsMatch(assemblyName)) {
					continue;
				}

				Type[] exportedTypes;
				try {
					exportedTypes = assembly.GetExportedTypes();
				} catch (System.Exception) {
					exportedTypes = null;
				}
				if (exportedTypes != null) {
					foreach (var t in exportedTypes) {
						if (t.GetCustomAttributes(false).Any(i =>
							i is TangerineRegisterNodeAttribute || i is TangerineRegisterComponentAttribute)
						) {
							yield return t;
						}
					}
				}
			}
		}

		private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var commaIndex = args.Name.IndexOf(',');
			var name = commaIndex < 0 ? Path.GetFileName(args.Name) : args.Name.Substring(0, commaIndex);
			if (string.IsNullOrEmpty(name)) {
				return null;
			}

			if (!resolvedAssemblies.TryGetValue(name, out var assembly)) {
				var requiredAssemblies = CurrentPlugin?.GetRequiredAssemblies?.Invoke();
				var foundPath = requiredAssemblies?.FirstOrDefault(assemblyPath =>
					assemblyPath == name || Path.GetFileName(assemblyPath).Equals(name, StringComparison.InvariantCultureIgnoreCase)
				);
				if (foundPath == null) {
					return null;
				}

				var domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
				var dllPath = Path.Combine(CurrentPluginDirectory, foundPath) + ".dll";
				if (TryFindDomainAssembliesByPath(domainAssemblies, dllPath, out assembly)) {
					resolvedAssemblies.Add(name, assembly);
					return assembly;
				}
				if (TryFindDomainAssembliesByName(domainAssemblies, name, out assembly)) {
					throw new InvalidOperationException(
						$"WARNING: Assembly {name} with path {assembly.Location} has already loaded in domain." +
						$"\nAssembly {name} with path {dllPath} leads to exception."
					);
				}
				assembly = LoadAssembly(dllPath);
				resolvedAssemblies.Add(name, assembly);
			}
			return assembly;
		}

		private static bool TryFindDomainAssembliesByPath(Assembly[] domainAssemblies, string path, out Assembly assembly)
		{
			assembly = domainAssemblies.FirstOrDefault(i => {
				try {
					return string.Equals(Path.GetFullPath(i.Location), Path.GetFullPath(path), StringComparison.CurrentCultureIgnoreCase);
				} catch {
					return false;
				}
			});
			return assembly != null;
		}

		private static bool TryFindDomainAssembliesByName(Assembly[] domainAssemblies, string name, out Assembly assembly)
		{
			assembly = domainAssemblies.FirstOrDefault(i => {
				try {
					return string.Equals(i.GetName().Name, name, StringComparison.CurrentCultureIgnoreCase);
				} catch {
					return false;
				}
			});
			return assembly != null;
		}

		private static Assembly LoadAssembly(string path)
		{
			var readAllDllBytes = File.ReadAllBytes(path);
			byte[] readAllPdbBytes = null;
#if DEBUG
			var pdbPath = Path.ChangeExtension(path, ".pdb");
			if (File.Exists(pdbPath)) {
				readAllPdbBytes = File.ReadAllBytes(pdbPath);
			}
#endif
			return Assembly.Load(readAllDllBytes, readAllPdbBytes);
		}
	}
}
