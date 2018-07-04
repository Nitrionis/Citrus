using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using Lime;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Orange
{
	public static class NewProject
	{
		private static string newCitrusDirectory;
		private static string targetDirectory;

		private static void Exec(string name, string args)
		{
			var process = new System.Diagnostics.Process {
				StartInfo = {
					FileName = name,
					Arguments = args,
					UseShellExecute = false,
					WorkingDirectory = targetDirectory,
				}
			};
			process.Start();
			process.WaitForExit();
			if (process.ExitCode != 0) {
				Console.WriteLine($"Process {name} {args} exited with code {process.ExitCode}");
				throw new InvalidOperationException();
			}
		}

		private static void Git(string gitArgs)
		{
			Exec("git", gitArgs);
		}

		[Export(nameof(OrangePlugin.MenuItems))]
		[ExportMetadata("Label", "New Project")]
		[ExportMetadata("Priority", 3)]
		public static void NewProjectAction()
		{
			Application.InvokeOnMainThread(() => {
				var citrusPath = Toolbox.CalcCitrusDirectory();
				var dlg = new FileDialog {
					AllowedFileTypes = new string[] { "" },
					Mode = FileDialogMode.SelectFolder
				};
				if (dlg.RunModal()) {
					targetDirectory = dlg.FileName;
					newCitrusDirectory = Path.Combine(dlg.FileName, "Citrus");
					var projectName = Path.GetFileName(dlg.FileName);
					var newProjectApplicationName = String.Join(" ", Regex.Split(projectName, @"(?<!^)(?=[A-Z])"));
					Console.WriteLine($"New project name is \"{projectName}\"");
					HashSet<string> textfileExtensions = new HashSet<string> { ".cs", ".xml", ".csproj", ".sln", ".citproj", ".projitems", ".shproj", ".txt", ".plist", ".strings", ".gitignore", };
					using (var dc = new DirectoryChanger($"{citrusPath}/Samples/EmptyProject/")) {
						var fe = new FileEnumerator(".");
						foreach (var f in fe.Enumerate()) {
							var targetPath = Path.Combine(dlg.FileName, f.Path.Replace("EmptyProject", projectName));
							Console.WriteLine($"Copying: {f.Path} => {targetPath}");
							Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
							System.IO.File.Copy(f.Path, targetPath);
							if (textfileExtensions.Contains(Path.GetExtension(targetPath).ToLower(CultureInfo.InvariantCulture))) {
								string text = File.ReadAllText(targetPath);
								text = text.Replace("EmptyProject", projectName);
								text = text.Replace("Empty Project", newProjectApplicationName);
								text = text.Replace("emptyproject", projectName.ToLower());
								var citrusUri = new Uri(newCitrusDirectory);
								var fileUri = new Uri(targetPath);
								var relativeUri = fileUri.MakeRelativeUri(citrusUri);
								// TODO: apply only to .sln and .csproj file
								text = text.Replace("..\\..\\..", relativeUri.ToString());
								if (targetPath.EndsWith(".citproj", StringComparison.OrdinalIgnoreCase)) {
									text = text.Replace("CitrusLocation: \"..\",", $"CitrusLocation: \"{relativeUri}\",");
								}
								File.WriteAllText(targetPath, text);
							}
						}
					}
					Git("init");
					Git("add -A");
					Git("submodule add https://gitlab.game-forest.com:8888/Common/Citrus.git Citrus");
					Git("commit -m\"Initial commit.\"");
				}
			});
		}
	}
}
