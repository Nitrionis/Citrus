﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using Action = System.Action;

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

		[ImportMany(nameof(AtlasPackers), AllowRecomposition = true)]
		public IEnumerable<Lazy<Func<string, List<AssetCooker.AtlasItem>, int, int>, IAtlasPackerMetadata>> AtlasPackers { get; set; }

		[ImportMany(nameof(AfterAssetsCooked), AllowRecomposition = true)]
		public IEnumerable<Action<string>> AfterAssetsCooked { get; set; }

		[ImportMany(nameof(CommandLineArguments), AllowRecomposition = true)]
		public IEnumerable<Func<string>> CommandLineArguments { get; set; }

		[ImportMany(nameof(MenuItems), AllowRecomposition = true)]
		public IEnumerable<Lazy<Action, IMenuItemMetadata>> MenuItems { get; set; }
	}

	public static class PluginLoader
	{
		public static string CurrentPluginDirectory;
		public static OrangePlugin CurrentPlugin = new OrangePlugin();
		private static CompositionContainer compositionContainer;
		private static readonly AggregateCatalog catalog;
		private static readonly List<ComposablePartCatalog> registeredCatalogs = new List<ComposablePartCatalog>();

		static PluginLoader()
		{
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
			var pluginConfiguration = "Debug";
#else
			var pluginConfiguration = "Release";
#endif
			CurrentPluginDirectory = Path.Combine(pluginRoot, "bin", pluginConfiguration);
			CurrentPlugin?.Finalize?.Invoke();
			The.UI.DestroyPluginUI();
			CurrentPlugin = new OrangePlugin();
			ResetPlugins();
			try {
				catalog.Catalogs.Add(new DirectoryCatalog(CurrentPluginDirectory));
				ValidateComposition();
			} catch (BadImageFormatException e) {
				Console.WriteLine(e.Message);
			} catch (System.Exception e) {
				Console.WriteLine(e.Message);
			}
			CurrentPlugin?.Initialize?.Invoke();
			var uiBuilder = The.UI.GetPluginUIBuilder();
			if (uiBuilder != null) {
				CurrentPlugin?.BuildUI?.Invoke(uiBuilder);
				The.UI.CreatePluginUI(uiBuilder);
			}
			The.MenuController.CreateAssemblyMenuItems();
		}

		public static void AfterAssetsCooked(string bundleName)
		{
			foreach (var i in CurrentPlugin.AfterAssetsCooked) {
				i(bundleName);
			}
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
					} else {
						importedCount++;
					}
				} else if (member is FieldInfo) {
					var field = member as FieldInfo;
					if (field.FieldType.GetInterfaces().Contains(typeof(IEnumerable))) {
						importedCount += ((ICollection)field.GetValue(CurrentPlugin)).Count;
					} else {
						importedCount++;
					}
				}
			}

			if (exportedCount != importedCount) {
				throw new Exception(
					$"WARNING: Plugin composition missmatch found.\nThe given assemblies defines [{exportedCount}] " +
					$"exports, but only [{importedCount}] has been imported.\nPlease check export contracts.\n");
			}
		}
	}
}
