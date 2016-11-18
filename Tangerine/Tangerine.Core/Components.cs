﻿using System;
using Lime;

namespace Tangerine.Core.Components
{
	public class CurveRow : IComponent
	{
		public Node Node { get; private set; }
		public IAnimator Animator { get; private set; }
		public CurveEditorState State { get; private set; }

		public CurveRow(Node node, IAnimator animator, CurveEditorState state)
		{
			Node = node;
			Animator = animator;
			State = state;
		}
	}

	public class CommonNodeRow : IComponent
	{
		public Node Node { get; private set; }
		public NodeVisibility Visibility { get { return Node.EditorState().Visibility; } set { Node.EditorState().Visibility = value; } }
		public bool Locked { get { return Node.EditorState().Locked; } set { Node.EditorState().Locked = value; } }
		public bool Expanded { get { return Node.EditorState().Expanded; } set { Node.EditorState().Expanded = value; } }

		public CommonNodeRow(Node node)
		{
			Node = node;
		}
	}

	public class NodeRow : CommonNodeRow
	{
		public NodeRow(Node node) : base(node) { }
	}

	public class FolderRow : CommonNodeRow
	{
		public Node FolderEnd { get; set; }
		public FolderRow(Node node) : base(node) { }
	}

	public class PropertyRow : IComponent
	{
		public Node Node { get; private set; }
		public IAnimator Animator { get; private set; }

		public PropertyRow(Node node, IAnimator animator)
		{
			Node = node;
			Animator = animator;
		}
	}
}
