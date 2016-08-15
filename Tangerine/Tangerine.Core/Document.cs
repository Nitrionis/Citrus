﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Lime;

namespace Tangerine.Core
{
	public interface IDocumentView
	{
		void Detach();
		void Attach();
	}

	public sealed class Document
	{
		public enum CloseAction
		{
			Cancel,
			SaveChanges,
			DiscardChanges
		}

		public static event Action<Document> AttachingViews;
		public static event Func<Document, CloseAction> Closing;

		public const string SceneFileExtension = "scene";

		public static Document Current { get; private set; }

		public string Path { get; private set; }
		public readonly DocumentHistory History;
		public bool IsModified => History.IsDocumentModified;

		public Node RootNode { get; private set; }
		public Node Container { get; set; }
		public VersionedCollection<Node> SelectedNodes = new VersionedCollection<Node>();
		public string SceneNavigatedFrom;
		public readonly List<IDocumentView> Views = new List<IDocumentView>();

		public int AnimationFrame
		{
			get { return Container.AnimationFrame; }
			set { Container.AnimationFrame = value; }
		}

		public string AnimationId { get; set; }

		public Document()
		{
			History = new DocumentHistory();
		}

		public Document(string path) : this()
		{
			Path = path;
			using (Theme.Push(DefaultTheme.Instance)) {
				RootNode = new Frame(path);
			}
			Container = RootNode;
		}

		public void MakeCurrent()
		{
			SetCurrent(this);
		}

		public static void SetCurrent(Document doc)
		{
			if (Current != null) {
				Current.DetachViews();
			}
			Current = doc;
			if (doc == null) {
			} else {
				doc.AttachViews();
			}
		}

		void AttachViews()
		{
			AttachingViews?.Invoke(this);
			foreach (var i in Current.Views) {
				i.Attach();
			}
		}

		void DetachViews()
		{
			foreach (var i in Current.Views) {
				i.Detach();
			}
		}

		public bool Close()
		{
			if (!IsModified) {
				return true;
			}
			if (Closing != null) {
				var r = Closing(this);
				if (r == CloseAction.Cancel) {
					return false;
				}
				if (r == CloseAction.SaveChanges) {
					Save();
				}
			} else {
				Save();
			}
			return true;
		}

		public void Save()
		{
			throw new NotImplementedException();
		}
	}
}
