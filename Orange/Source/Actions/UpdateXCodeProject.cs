using System;
using System.IO;
using Orange;
using System.Text;

namespace Kill3.OrangePlugin
{
	public static class UpdateXCodeProject
	{
		[MenuItem("Update XCode Project")]
		public static void UpdateXCodeProjectAction()
		{
			if (The.Workspace.ProjectJson.GetBool("XCodeProject/DoSvnUpdate")) {
				Subversion.Update(GetXCodeProjectFolder());
			}
			AssetCooker.Cook(TargetPlatform.iOS);
			var builder = new Orange.SolutionBuilder(TargetPlatform.iOS);
			var output = new StringBuilder();
			builder.Clean();
			if (builder.Build(output))
			{
				The.UI.ScrollLogToEnd();
				string allText = output.ToString();
				foreach (var line in allText.Split('\n')) {
					if (line.Contains("/bin/mtouch")) {
						var mtouch = line;
						GenerateUnsignedBinary(mtouch);
						var appPath = GetGeneratedAppPath(mtouch);
						var dstPath = GetXCodeProjectDataFolder();
						CopyContent(appPath, dstPath);
						CopyDSYM(appPath, Path.GetDirectoryName(dstPath));
					}
				}
			} else if (UserInterface.Instance is ConsoleUI) {
				Environment.Exit(1);
			}
			if (The.Workspace.ProjectJson.GetBool("XCodeProject/DoSvnCommit")) {
				Subversion.Commit(GetXCodeProjectFolder(), "");
			}
		}

		static void CopyDSYM(string appPath, string dstDirectory)
		{
			var dsymPath = appPath + ".dSYM";
			var dsymDstPath = Path.Combine(dstDirectory, Path.GetFileName(dsymPath));
			CopyDirectoryRecursive(dsymPath, dsymDstPath);
		}
		
		private static void CopyContent(string appPath, string dstPath)
		{
			string resourceMasks = The.Workspace.ProjectJson["XCodeProject/Resources"] as string;
			foreach (var mask in resourceMasks.Split(' ')) {
				CopyFiles(appPath, dstPath, mask);
			}
		}

		private static void CopyDirectoryRecursive(string source, string destination)
		{
			Console.WriteLine("Copying directory {0} to {1}", source, destination);
			foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) {
				Directory.CreateDirectory(dirPath.Replace(source, destination));
			}
			foreach (string newPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories)) {
				File.Copy(newPath, newPath.Replace(source, destination), true);
			}
		}

		static void CopyFiles(string source, string destination, string searchPattern)
		{
			var files = new DirectoryInfo(source).GetFiles(searchPattern);
			foreach (var file in files) {
				var destFile = Path.Combine(destination, file.Name);
				Console.WriteLine("Writing " + destFile);
				file.CopyTo(destFile, overwrite: true);
			}
			var dirs = new DirectoryInfo(source).GetDirectories(searchPattern);
			foreach (var dir in dirs) {
				var destDir = Path.Combine(destination, dir.Name);
				Console.WriteLine("Writing " + destDir);
				Directory.CreateDirectory(destDir);
				CopyDirectoryRecursive(dir.FullName, destDir);
			}
		}

		private static string GetXCodeProjectFolder()
		{
			var p = GetXCodeProjectDataFolder();
			p = Path.GetDirectoryName(p);
			return p;
		}

		private static string GetXCodeProjectDataFolder()
		{
			var p = Path.GetDirectoryName(The.Workspace.ProjectFile);
			p = Path.Combine(p, The.Workspace.ProjectJson["XCodeProject/DataFolder"] as string);
			return p;
		}
		
		private static string GetGeneratedAppPath(string mtouch)
		{
			string[] args = mtouch.Trim().Split(' ');
			string x;
			var dev = Array.IndexOf(args, "-dev");
			if (dev >= 0) {
				x = args[dev + 1];
				x = x.Substring(1, x.Length - 2);
			} else {
				dev = Array.IndexOf(args, "--dev");
				x = args[dev + 1];
			}
			return x;
		}

		static void GenerateUnsignedBinary(string mtouch)
		{
			Console.WriteLine("======================================");
			Console.WriteLine("Generating unsigned application bundle");
			Console.WriteLine("======================================");
			mtouch = mtouch.TrimStart();
			string app, args;
			if (mtouch.StartsWith ("Tool ")) {
				mtouch = mtouch.Substring (5);
				var s = mtouch.Split(new string[] { "execution started with arguments:" }, StringSplitOptions.None);
				app = s[0].Trim();
				args = s[1];
				var dir = Path.GetDirectoryName(The.Workspace.GetSolutionFilePath());
				using (new DirectoryChanger(dir)) {
					Process.Start(app, args);
				}
			} else {
				var x = mtouch.IndexOf(' ');
				app = mtouch.Substring(0, x);
				args = mtouch.Substring(x + 1);
				Process.Start(app, args);
			}
		}
	}
}