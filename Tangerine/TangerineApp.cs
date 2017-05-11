﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public class TangerineApp
	{
		public static TangerineApp Instance { get; private set; }
		public readonly IMenu PadsMenu;
		public readonly Dictionary<string, Toolbar> Toolbars = new Dictionary<string, Toolbar>();
		public readonly DockManager.State DockManagerInitialState;

		public static void Initialize()
		{
			Instance = new TangerineApp();
		}

		private TangerineApp()
		{
			Orange.UserInterface.Instance = new OrangeInterface();
			WindowOptions.DefaultRefreshRate = 60;
			WidgetInput.AcceptMouseBeyondWidgetByDefault = false;
			Application.IsTangerine = true;
			Serialization.DeserializerBuilders.Insert(0, DeserializeHotStudioAssets);
			Widget.DefaultWidgetSize = Vector2.Zero;

			UserPreferences.Initialize();
			SetColorTheme(UserPreferences.Instance.Theme);

			LoadFont();

			PadsMenu = new Menu();
			DockManager.Initialize(new Vector2(1024, 768), PadsMenu);
			DockManager.Instance.MainWindowWidget.Window.AllowDropFiles = true;
			CreateMainMenu();

			Application.Exiting += () => Project.Current.Close();
			Application.Exited += () => {
				UserPreferences.Instance.DockState = DockManager.Instance.ExportState();
				UserPreferences.Instance.Save();
			};

			var timelinePanel = new DockPanel("Timeline");
			var inspectorPanel = new DockPanel("Inspector");
			var searchPanel = new DockPanel("Search");
			var filesystemPanel = new DockPanel("Filesystem");

			var dockManager = DockManager.Instance;
			dockManager.AddPanel(timelinePanel, DockSite.Top, new Vector2(800, 300));
			dockManager.AddPanel(inspectorPanel, DockSite.Left, new Vector2(300, 700));
			dockManager.AddPanel(searchPanel, DockSite.Right, new Vector2(300, 700));
			dockManager.AddPanel(filesystemPanel, DockSite.Right, new Vector2(300, 700));
			DockManagerInitialState = dockManager.ExportState();
			var documentViewContainer = InitializeDocumentArea(dockManager);

			dockManager.ImportState(UserPreferences.Instance.DockState);
			Document.Closing += doc => {
				var alert = new AlertDialog($"Save the changes to document '{doc.Path}' before closing?", "Yes", "No", "Cancel");
				switch (alert.Show()) {
					case 0: return Document.CloseAction.SaveChanges;
					case 1: return Document.CloseAction.DiscardChanges;
					default: return Document.CloseAction.Cancel;
				}
			};

			Document.NodeDecorators.AddFor<Spline>(n => n.CompoundPostPresenter.Add(new UI.SceneView.SplinePresenter()));
			Document.NodeDecorators.AddFor<Viewport3D>(n => n.CompoundPostPresenter.Add(new UI.SceneView.Spline3DPresenter()));
			Document.NodeDecorators.AddFor<PointObject>(n => n.CompoundPostPresenter.Add(new UI.SceneView.PointObjectPresenter()));
			Document.NodeDecorators.AddFor<SplinePoint>(n => n.CompoundPostPresenter.Add(new UI.SceneView.SplinePointPresenter()));

			DocumentHistory.Processors.AddRange(new IOperationProcessor[] {
				new Core.Operations.SelectRow.Processor(),
				new Core.Operations.SetProperty.Processor(),
				new Core.Operations.RemoveKeyframe.Processor(),
				new Core.Operations.SetKeyframe.Processor(),
				new Core.Operations.InsertFolderItem.Processor(),
				new Core.Operations.UnlinkFolderItem.Processor(),
				new Core.Operations.SetMarker.Processor(),
				new Core.Operations.DeleteMarker.Processor(),
				new Core.Operations.DistortionMeshProcessor(),
				new Core.Operations.SyncFolderDescriptorsProcessor(),
				new Core.Operations.TimelineHorizontalShift.Processor(),
				new UI.Timeline.Operations.SelectGridSpan.Processor(),
				new UI.Timeline.Operations.ClearGridSelection.Processor(),
				new UI.Timeline.Operations.ShiftGridSelection.Processor(),
				new UI.Timeline.Operations.SetCurrentColumn.Processor(),
				new RowsSynchronizer(),
				new UpdateNodesAndReferencesProcessor(),
			});
			DocumentHistory.Processors.AddRange(UI.Timeline.Timeline.GetOperationProcessors());

			Toolbars.Add("Create", new Toolbar(dockManager.ToolbarArea));
			Toolbars.Add("Tools", new Toolbar(dockManager.ToolbarArea));
			foreach (var c in Application.MainMenu.FindCommand("Create").Menu) {
				Toolbars["Create"].Add(c);
			}
			CreateToolsToolbar();
			Document.AttachingViews += doc => {
				if (doc.Views.Count == 0) {
					doc.Views.AddRange(new IDocumentView [] {
						new UI.Inspector.Inspector(inspectorPanel.ContentWidget),
						new UI.Timeline.Timeline(timelinePanel),
						new UI.SceneView.SceneView(documentViewContainer),
						new UI.SearchPanel(searchPanel.ContentWidget),
						new UI.FilesystemView.FilesystemView(filesystemPanel.ContentWidget),
					});
				}
			};
			var proj = UserPreferences.Instance.RecentProjects.FirstOrDefault();
			if (proj != null) {
				new Project(proj).Open();
			}
			RegisterGlobalCommands();
		}

		void SetColorTheme(ColorThemeEnum theme)
		{
			Theme.Current = new DesktopTheme();
			DesktopTheme.Colors = theme == ColorThemeEnum.Light ? DesktopTheme.ColorTheme.CreateLightTheme() : DesktopTheme.ColorTheme.CreateDarkTheme();
			ColorTheme.Current = theme == ColorThemeEnum.Light ? ColorTheme.CreateLightTheme() : ColorTheme.CreateDarkTheme();
		}

		class UpdateNodesAndReferencesProcessor : SymmetricOperationProcessor
		{
			public override void Process(IOperation op)
			{
				Document.Current.RootNode.Update(0);
			}
		}

		void CreateToolsToolbar()
		{
			var tb = Toolbars["Tools"];
			tb.Add(Tools.AlignLeft);
			tb.Add(Tools.AlignTop);
			tb.Add(Tools.AlignRight);
			tb.Add(Tools.AlignBottom);
			tb.Add(Tools.AlignCentersHorizontally);
			tb.Add(Tools.AlignCentersVertically);
			tb.Add(Tools.CenterHorizontally);
			tb.Add(Tools.CenterVertically);
			tb.Add(Tools.ResetScale);
			tb.Add(Tools.ResetRotation);
			tb.Add(Tools.FitToContainer);
			tb.Add(Tools.FitToContent);
			tb.Add(Tools.FlipX);
			tb.Add(Tools.FlipY);
		}

		Yuzu.AbstractDeserializer DeserializeHotStudioAssets(string path, System.IO.Stream stream)
		{
			if (path.EndsWith(".scene", StringComparison.CurrentCultureIgnoreCase)) {
				return new Orange.HotSceneDeserializer(stream);
			} else if (path.EndsWith(".fnt", StringComparison.CurrentCultureIgnoreCase)) {
				return new Orange.HotFontDeserializer(stream);
			}
			return null;
		}

		static Frame InitializeDocumentArea(DockManager dockManager)
		{
			var tabBar = new TabBar { LayoutCell = new LayoutCell { StretchY = 0 } };
			var documentViewContainer = new Frame {
				ClipChildren = ClipMethod.ScissorTest,
				Layout = new StackLayout(),
				HitTestTarget = true
			};
			new DocumentTabsProcessor(tabBar);
			var docArea = dockManager.DocumentArea;
			docArea.Layout = new VBoxLayout();
			docArea.AddNode(tabBar);
			docArea.AddNode(documentViewContainer);
			docArea.FocusScope = new KeyboardFocusScope(docArea);
			return documentViewContainer;
		}

		class DocumentTabsProcessor
		{
			public DocumentTabsProcessor(TabBar tabBar)
			{
				RebuildTabs(tabBar);
				tabBar.AddChangeWatcher(() => Project.Current.Documents.Version, _ => RebuildTabs(tabBar));
				tabBar.AddChangeWatcher(() => Project.Current, _ => RebuildTabs(tabBar));
			}

			private void RebuildTabs(TabBar tabBar)
			{
				tabBar.Nodes.Clear();
				foreach (var doc in Project.Current.Documents) {
					var tab = new Tab { Closable = true };
					var currentDocumentChanged = new Property<bool>(() => Document.Current == doc).DistinctUntilChanged().Where(i => i);
					tab.Tasks.Add(currentDocumentChanged.Consume(_ => tabBar.ActivateTab(tab)));
					tab.AddChangeWatcher(() => doc.Path, _ => RefreshTabText(doc, tab));
					tab.AddChangeWatcher(() => doc.IsModified, _ => RefreshTabText(doc, tab));
					tab.Clicked += doc.MakeCurrent;
					tab.Closing += () => Project.Current.CloseDocument(doc);
					tabBar.AddNode(tab);
				}
				tabBar.AddNode(new Widget { LayoutCell = new LayoutCell { StretchX = 0 }});
			}

			void RefreshTabText(Document doc, Tab tab)
			{
				tab.Text = System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(doc.Path, null));
				if (doc.IsModified) {
					tab.Text += '*';
				}
			}
		}

		void CreateMainMenu()
		{
			Application.MainMenu = new Menu {
#if MAC
				new Command("Application", new Menu {
					GenericCommands.PreferencesDialog,
					Command.MenuSeparator,
					GenericCommands.Quit,
				}),
#endif
				new Command("File", new Menu {
					GenericCommands.New,
					Command.MenuSeparator,
					GenericCommands.Open,
					GenericCommands.OpenProject,
					Command.MenuSeparator,
					GenericCommands.Save,
					GenericCommands.SaveAs,
					GenericCommands.UpgradeDocumentFormat,
					Command.MenuSeparator,
#if !MAC
					GenericCommands.PreferencesDialog,
					Command.MenuSeparator,
#endif
					GenericCommands.CloseDocument,
#if !MAC
					GenericCommands.Quit,
#endif
				}),
				new Command("Edit", new Menu {
					Command.Undo,
					Command.Redo,
					Command.MenuSeparator,
					Command.Cut,
					Command.Copy,
					Command.Paste,
					Command.Delete,
					TimelineCommands.DeleteKeyframes,
					Command.MenuSeparator,
					Command.SelectAll,
					Command.MenuSeparator,
					GenericCommands.Group,
					GenericCommands.Ungroup,
					GenericCommands.InsertTimelineColumn,
					GenericCommands.RemoveTimelineColumn,
					Command.MenuSeparator,
					GenericCommands.GroupContentsToMorphableMeshes,
					GenericCommands.ExportScene,
					GenericCommands.UpsampleAnimationTwice,
				}),
				new Command("Create", new Menu()),
				new Command("View", new Menu {
					GenericCommands.DefaultLayout,
					new Command("Pads", PadsMenu),
				}),
				new Command("Window", new Menu {
					GenericCommands.NextDocument,
					GenericCommands.PreviousDocument
				}),
			};
			var nodeTypes = new[] {
				typeof(Frame),
				typeof(Button),
				typeof(Image),
				typeof(Audio),
				typeof(Movie),
				typeof(Bone),
				typeof(ParticleEmitter),
				typeof(ParticleModifier),
				typeof(EmitterShapePoint),
				typeof(ParticlesMagnet),
				typeof(SimpleText),
				typeof(RichText),
				typeof(TextStyle),
				typeof(NineGrid),
				typeof(DistortionMesh),
				typeof(Spline),
				typeof(SplinePoint),
				typeof(SplineGear),
				typeof(ImageCombiner),
				typeof(Viewport3D),
				typeof(Camera3D),
				typeof(Model3D),
				typeof(Node3D),
				typeof(WidgetAdapter3D),
				typeof(Spline3D),
				typeof(SplinePoint3D),
				typeof(SplineGear3D),
			};
			foreach (var t in nodeTypes) {
				var cmd = new Command(t.Name) { Icon = NodeIconPool.GetTexture(t) };
				CommandHandlerList.Global.Connect(cmd, new CreateNode(t));
				Application.MainMenu.FindCommand("Create").Menu.Add(cmd);
			}
		}

		void RegisterGlobalCommands()
		{
			UI.Inspector.Inspector.RegisterGlobalCommands();
			UI.Timeline.Timeline.RegisterGlobalCommands();
			UI.SceneView.SceneView.RegisterGlobalCommands();

			var h = CommandHandlerList.Global;
			h.Connect(GenericCommands.New, new FileNew());
			h.Connect(GenericCommands.Open, new FileOpen());
			h.Connect(GenericCommands.OpenProject, new FileOpenProject());
			h.Connect(GenericCommands.Save, new FileSave());
			h.Connect(GenericCommands.SaveAs, new FileSaveAs());
			h.Connect(GenericCommands.UpgradeDocumentFormat, new UpgradeDocumentFormat());
			h.Connect(GenericCommands.CloseDocument, new FileClose());
			h.Connect(GenericCommands.Quit, Application.Exit);
			h.Connect(GenericCommands.PreferencesDialog, () => new PreferencesDialog());
			h.Connect(GenericCommands.Group, new GroupNodes());
			h.Connect(GenericCommands.Ungroup, new UngroupNodes());
			h.Connect(GenericCommands.InsertTimelineColumn, new InsertTimelineColumn());
			h.Connect(GenericCommands.RemoveTimelineColumn, new RemoveTimelineColumn());
			h.Connect(GenericCommands.NextDocument, new SetNextDocument());
			h.Connect(GenericCommands.PreviousDocument, new SetPreviousDocument());
			h.Connect(GenericCommands.DefaultLayout, new ViewDefaultLayout());
			h.Connect(GenericCommands.GroupContentsToMorphableMeshes, new GroupContentsToMorphableMeshes());
			h.Connect(GenericCommands.ExportScene, new ExportScene());
			h.Connect(GenericCommands.UpsampleAnimationTwice, new UpsampleAnimationTwice());
			h.Connect(Tools.AlignLeft, new AlignLeft());
			h.Connect(Tools.AlignRight, new AlignRight());
			h.Connect(Tools.AlignTop, new AlignTop());
			h.Connect(Tools.AlignBottom, new AlignBottom());
			h.Connect(Tools.CenterHorizontally, new CenterHorizontally());
			h.Connect(Tools.CenterVertically, new CenterVertically());
			h.Connect(Tools.AlignCentersHorizontally, new AlignCentersHorizontally());
			h.Connect(Tools.AlignCentersVertically, new AlignCentersVertically());
			h.Connect(Tools.ResetScale, new ResetScale());
			h.Connect(Tools.ResetRotation, new ResetRotation());
			h.Connect(Tools.FitToContainer, new FitToContainer());
			h.Connect(Tools.FitToContent, new FitToContent());
			h.Connect(Tools.FlipX, new FlipX());
			h.Connect(Tools.FlipY, new FlipY());
			h.Connect(Command.Copy, Core.Operations.Copy.CopyToClipboard, () => Document.Current?.SelectedRows().Any() ?? false);
			h.Connect(Command.Cut, Core.Operations.Cut.Perform, () => Document.Current?.SelectedRows().Any() ?? false);
			h.Connect(Command.Paste, Paste, Document.HasCurrent);
			h.Connect(Command.Delete, Core.Operations.Delete.Perform, () => Document.Current?.SelectedRows().Any() ?? false);
			h.Connect(Command.SelectAll, () => {
				foreach (var row in Document.Current.Rows) {
					Core.Operations.SelectRow.Perform(row, true);
				}
			}, () => Document.Current?.Rows.Count > 0);
			h.Connect(Command.Undo, () => Document.Current.History.Undo(), () => Document.Current?.History.CanUndo() ?? false);
			h.Connect(Command.Redo, () => Document.Current.History.Redo(), () => Document.Current?.History.CanRedo() ?? false);
		}

		static void Paste()
		{
			try {
				Core.Operations.Paste.Perform();
			} catch (InvalidOperationException e) {
				AlertDialog.Show(e.Message);
			}
		}

		static void LoadFont()
		{
			var fontData = new EmbeddedResource("Tangerine.Resources.SegoeUIRegular.ttf", "Tangerine").GetResourceBytes();
			var font = new DynamicFont(fontData);
			FontPool.Instance.AddFont("Default", font);
		}
	}
}
