﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Orange
{
	static class Process
	{
		public enum Options
		{
			RedirectOutput = 1,
			RedirectErrors = 2
		}

		public static int Start(string app, string args, Options options = Options.RedirectOutput | Options.RedirectErrors, StringBuilder output = null)
		{
			if (output == null) {
				output = new StringBuilder();
			}
			var p = new System.Diagnostics.Process();
			p.StartInfo.FileName = app;
			p.StartInfo.Arguments = args;
			p.StartInfo.UseShellExecute = false;
#if WIN
			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.WorkingDirectory = Path.GetDirectoryName(app);
			int cp = System.Text.Encoding.Default.CodePage;
			if (cp == 1251)
				cp = 866;
			p.StartInfo.StandardOutputEncoding = System.Text.Encoding.GetEncoding(cp);
			p.StartInfo.StandardErrorEncoding = System.Text.Encoding.GetEncoding(cp);
#else
					p.StartInfo.StandardOutputEncoding = System.Text.Encoding.Default;
					p.StartInfo.StandardErrorEncoding = System.Text.Encoding.Default;
					p.StartInfo.EnvironmentVariables.Clear();
					p.StartInfo.EnvironmentVariables.Add("PATH", "/usr/bin");
#endif
			p.StartInfo.RedirectStandardOutput = true;
			p.StartInfo.RedirectStandardError = true;
			var logger = new System.Text.StringBuilder();
			if ((options & Options.RedirectOutput) != 0) {
				p.OutputDataReceived += (sender, e) => {
					lock (logger) {
						if (e.Data != null) {
							logger.AppendLine(e.Data);
							output.AppendLine(e.Data);
						}
					}
				};
			}
			if ((options & Options.RedirectErrors) != 0) {
				p.ErrorDataReceived += (sender, e) => {
					lock (logger) {
						if (e.Data != null) {
							logger.AppendLine(e.Data);
							output.AppendLine(e.Data);
						}
					}
				};
			}
			p.Start();
			p.BeginOutputReadLine();
			p.BeginErrorReadLine();
			while (!p.HasExited) {
				p.WaitForExit(50);
				lock (logger) {
					if (logger.Length > 0) {
						Console.Write(logger.ToString());
						logger.Clear();
					}
				}
				The.UI.ProcessPendingEvents();
			}
			Console.Write(logger.ToString());
			return p.ExitCode;
		}
	}
}
