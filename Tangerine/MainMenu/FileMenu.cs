﻿using System;
using System.IO;
using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public class FileOpenProject : CommandHandler
	{
		public override void Execute()
		{
			var dlg = new FileDialog { AllowedFileTypes = new string[] { "citproj" }, Mode = FileDialogMode.Open };
			if (dlg.RunModal()) {
				if (Project.Current.Close()) {
					new Project(dlg.FileName).Open();
					var prefs = UserPreferences.Instance;
					prefs.RecentProjects.Remove(dlg.FileName);
					prefs.RecentProjects.Insert(0, dlg.FileName);
					prefs.Save();
				}
			}
		}
	}

	public class FileNew : CommandHandler
	{
		public override void RefreshCommand(ICommand command)
		{
			command.Enabled = Project.Current != Project.Null;
		}

		public override void Execute()
		{
			Project.Current.NewDocument();
		}
	}

	public class FileOpen : CommandHandler
	{
		public override void RefreshCommand(ICommand command)
		{
			command.Enabled = Project.Current != Project.Null;
		}

		public override void Execute()
		{
			var dlg = new FileDialog {
				AllowedFileTypes = Document.AllowedFileTypes,
				Mode = FileDialogMode.Open,
			};
			if (Document.Current != null) {
				dlg.InitialDirectory = Project.Current.GetSystemDirectory(Document.Current.Path);
			}
			if (dlg.RunModal()) {
				string localPath;
				if (!Project.Current.TryGetAssetPath(dlg.FileName, out localPath)) {
					AlertDialog.Show("Can't open a document outside the project directory");
				} else {
					Project.Current.OpenDocument(localPath);
				}
			}
		}
	}

	public class FileSave : DocumentCommandHandler
	{
		static FileSave()
		{
			Document.PathSelector += SelectPath;
		}

		public override void Execute()
		{
			try {
				Document.Current.Save();
			} catch (System.Exception e) {
				ShowErrorMessageBox(e);
			}				
		}
		
		public static void ShowErrorMessageBox(System.Exception e)
		{
			AlertDialog.Show($"Save document error: '{e.Message}'.\nYou may have to upgrade the document format.");
		}

		public static bool SelectPath(out string path)
		{
			var dlg = new FileDialog {
				AllowedFileTypes = new string[] { Document.Current.GetFileExtension() },
				Mode = FileDialogMode.Save,
				InitialDirectory = Project.Current.GetSystemDirectory(Document.Current.Path)
			};
			path = null;
			if (!dlg.RunModal()) {
				return false;
			}
			if (!Project.Current.TryGetAssetPath(dlg.FileName, out path)) {
				AlertDialog.Show("Can't save the document outside the project directory");
				return false;
			}
			return true;
		}
	}

	public class FileSaveAs : DocumentCommandHandler
	{
		public override void Execute()
		{
			SaveAs();
		}

		public static void SaveAs()
		{
			var dlg = new FileDialog {
				AllowedFileTypes = new string[] { Document.Current.GetFileExtension() },
				Mode = FileDialogMode.Save,
				InitialDirectory = Project.Current.GetSystemDirectory(Document.Current.Path)
			};
			if (dlg.RunModal()) {
				string assetPath;
				if (!Project.Current.TryGetAssetPath(dlg.FileName, out assetPath)) {
					AlertDialog.Show("Can't save the document outside the project directory");
				} else {
					try {
						Document.Current.SaveAs(assetPath);
					} catch (System.Exception e) {
						FileSave.ShowErrorMessageBox(e);
					}
				}
			}
		}
	}

	public class FileClose : DocumentCommandHandler
	{
		public override void Execute()
		{
			if (Document.Current != null) {
				Project.Current.CloseDocument(Document.Current);
			}
		}
	}
	
	public class UpgradeDocumentFormat : DocumentCommandHandler
	{
		public override bool GetEnabled()
		{
			return Document.Current.Format == DocumentFormat.Scene;
		}

		public override void Execute()
		{
			try {
				AssetBundle.Instance.DeleteFile(Path.ChangeExtension(Document.Current.Path, "scene"));
				Document.Current.Format = DocumentFormat.Tan;
				RestoreDefaultAnimationEngine(Document.Current.RootNode);
				new UpsampleAnimationTwice().Execute();
				Document.Current.Save();
			} catch (System.Exception e) {
				AlertDialog.Show($"Upgrade document format error: '{e.Message}'");
			}				
		}

		private void RestoreDefaultAnimationEngine(Node node)
		{
			node.DefaultAnimation.AnimationEngine = DefaultAnimationEngine.Instance;
			foreach (var child in node.Nodes) {
				RestoreDefaultAnimationEngine(child);
			}
		}
	}
}
