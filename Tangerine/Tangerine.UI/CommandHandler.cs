﻿using System;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI
{
	public abstract class CommandHandler
	{
		public virtual void RefreshCommand(ICommand command) { }
		public abstract void Execute();
	}

	public abstract class DocumentCommandHandler : CommandHandler
	{
		public override void RefreshCommand(ICommand command)
		{
			command.Enabled = Core.Document.Current != null && GetEnabled();
		}

		public virtual bool GetEnabled() => true;
	}

	public class CommandHandlerList
	{
		struct Item
		{
			public ICommand Command;
			public CommandHandler Handler;
		}

		private readonly List<Item> items = new List<Item>();

		public static readonly CommandHandlerList Global = new CommandHandlerList();

		static CommandHandlerList()
		{
			Application.ActiveWindowUpdated += _ => Global.ProcessCommands();
		}

		public void Connect(ICommand command, CommandHandler handler)
		{
			items.Add(new Item { Command = command, Handler = handler });
		}

		public void Connect(ICommand command, Action action, Func<bool> enableChecker = null)
		{
			items.Add(new Item { Command = command, Handler = new DelegateHandler { Action = action, EnableChecker = enableChecker } });
		}

		public void ProcessCommands()
		{
			foreach (var i in items) {
				if (!i.Command.IsConsumed()) {
					i.Handler.RefreshCommand(i.Command);
					if (i.Command.WasIssued()) {
						i.Handler.Execute();
					}
				}
			}
		}

		class DelegateHandler : CommandHandler
		{
			public Action Action;
			public Func<bool> EnableChecker;

			public override void Execute() => Action?.Invoke();

			public override void RefreshCommand(ICommand command)
			{
				command.Enabled = EnableChecker?.Invoke() ?? true;
			}
		}
	}
}