﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Orange
{
	public static class CsprojSynchronization
	{
		private static string ToWindowsSlashes(string path)
		{
			return path.Replace('/', '\\');
		}

		public static string ToUnixSlashes(string path)
		{
			return path.Replace('\\', '/');
		}

		public static void SynchronizeProject(string projectFileName)
		{
			bool changed = false;
			var doc = new XmlDocument();
			doc.Load(projectFileName);
			using (new DirectoryChanger(Path.GetDirectoryName(projectFileName))) {
				ExcludeMissingItems(doc, ref changed);
				IncludeNewItems(doc, ref changed);
			}
			if (changed) {
				// disble BOM
				using (var writer = new XmlTextWriter(projectFileName, new UTF8Encoding(false)))
				{
					writer.Formatting = Formatting.Indented;
					doc.Save(writer);
				}
			}
			Console.WriteLine(string.Format("Synchronized project: {0}", projectFileName));
		}

		private static void IncludeNewItems(XmlDocument doc, ref bool changed)
		{
			var itemGroups = doc["Project"].EnumerateElements("ItemGroup").ToArray();
			// It is assumed that the second <ItemGroup> tag contains compile items
			var compileItems = itemGroups[1];
			foreach (var file in new FileEnumerator(".").Enumerate(".cs")) {
				var path = ToWindowsSlashes(file.Path);
				if (Path.GetFileName(path).StartsWith("TemporaryGeneratedFile")) {
					continue;
				}
				if (!HasCompileItem(doc, path)) {
					if (IsItemShouldBeAdded(path)) {
						var item = doc.CreateElement("Compile", doc["Project"].NamespaceURI);
						var include = item.Attributes.Append(doc.CreateAttribute("Include"));
						include.Value = path;
						compileItems.AppendChild(item);
						changed = true;
						Console.WriteLine("Added a new file: " + file.Path);
					}
				}
			}
		}

		private static bool HasCompileItem(XmlDocument doc, string path)
		{
			var itemGroups = doc["Project"].EnumerateElements("ItemGroup");
			foreach (var group in itemGroups) {
				foreach (var item in group.EnumerateElements("Compile")) {
					var itemPath = item.Attributes["Include"].Value;
					if (itemPath.ToLower() == path.ToLower()) {
						return true;
					}
				}
			}
			return false;
		}

		private static void ExcludeMissingItems(XmlDocument doc, ref bool changed)
		{
			var itemGroups = doc["Project"].EnumerateElements("ItemGroup");
			foreach (var group in itemGroups) {
				ExcludeMissingItemsFromGroup(group, ref changed);
			}
		}

		private static void ExcludeMissingItemsFromGroup(XmlNode group, ref bool changed)
		{
			foreach (var item in group.EnumerateElements("Compile")) {
				var path = item.Attributes["Include"].Value;
				if (!File.Exists(ToUnixSlashes(path)) || IsPathIgnored(path)) {
					group.RemoveChild(item);
					changed = true;
					Console.WriteLine("Removed a missing file: " + path);
				}
			}
		}

		private static IEnumerable<XmlNode> EnumerateElements(this XmlNode parent, string tag)
		{
			var items = parent.Cast<XmlNode>().ToArray();
			foreach (var item in items) {
				if (item.Name == tag) {
					yield return item;
				}
			}
		}

		private static bool IsItemShouldBeAdded(string filePath)
		{
			if (IsPathIgnored(filePath)) {
				return false;
			}
			// Ignore .cs files related to sub-projects
			var dir = ToUnixSlashes(filePath);
			while (true) {
				dir = Path.GetDirectoryName(dir);
				if (string.IsNullOrEmpty(dir)) {
					break;
				}
				if (Directory.EnumerateFiles(dir, "*.csproj").Count() > 0) {
					return false;
				}
			}
			return true;
		}

		private static bool IsPathIgnored(string filePath)
		{
			return
				filePath.StartsWith("packages") ||
				filePath.Contains("\\obj\\") ||
				filePath.StartsWith("obj\\") ||
				filePath.EndsWith("Resource.designer.cs") ||
				filePath.Contains("\\bin\\") ||
				filePath.StartsWith("bin\\");
		}
	}
}
