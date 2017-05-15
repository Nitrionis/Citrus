﻿using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;

namespace Orange
{
	public static class RunTangerineAction
	{
		[Export(nameof(OrangePlugin.MenuItems))]
		[ExportMetadata("Label", "Run Tangerine")]
		[ExportMetadata("Priority", 2)]
		public static void RunTangerine()
		{
			var path = Uri.UnescapeDataString((new Uri(Assembly.GetExecutingAssembly().CodeBase)).AbsolutePath);
			while (!string.Equals(Path.GetFileName(path), "Citrus", StringComparison.CurrentCultureIgnoreCase)) {
				path = Path.GetDirectoryName(path);
			}
			path = Path.Combine(path, "Tangerine");

#if WIN
			var buildSystem = new Source.MSBuild(path, "Tangerine", TargetPlatform.Win);
			buildSystem.Configuration = "Release";
#elif MAC
			var buildSystem = new Source.MDTool(path, "Tangerine", TargetPlatform.Mac);
			buildSystem.Configuration = "Debug"; // Release requires code signing, use debug for a while.
#endif
			buildSystem.PrepareForBuild();
			if (buildSystem.Execute() == 0) {
#if MAC
				var app = Path.Combine(path, "bin/Debug/Tangerine.app/Contents/MacOS/Tangerine");
#elif WIN
				var app = Path.Combine(path, "bin/Release/Tangerine.exe");
#endif
				var p = new System.Diagnostics.Process();
				p.StartInfo.FileName = app;
#if MAC
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.EnvironmentVariables.Clear();
				p.StartInfo.EnvironmentVariables.Add("PATH", "/usr/bin");
#endif
				p.Start();
			}
		}
	}
}
