using System;
using System.Collections.Generic;
using Lime;
#if PROFILER
using Lime.Profiler;
#endif // PROFILER

namespace Lime
{
	public abstract class CommandHandler
	{
		public virtual void RefreshCommand(ICommand command) { }
		public abstract void Execute();
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

		public void Connect(ICommand command, CommandHandler handler)
		{
			items.Add(new Item { Command = command, Handler = handler });
		}

		public void Disconnect(ICommand command)
		{
			foreach (var item in items.FindAll(i => i.Command == command)) {
				items.Remove(item);
			}
		}

		public void Connect(ICommand command, Action action, Func<bool> enableChecker = null)
		{
			items.Add(new Item { Command = command, Handler = new DelegateHandler { Action = action, EnableChecker = enableChecker } });
		}

		public void ProcessCommands()
		{
#if PROFILER
			var usage = ProfilerDatabase.CpuUsageStarted();
#endif // PROFILER
			// We are not using foreach because collection might be modified on command execution
			for (int i = 0; i < items.Count; i++) {
				var item = items[i];
				if (!item.Command.IsConsumed()) {
					item.Handler.RefreshCommand(item.Command);
					if (item.Command.WasIssued()) {
						item.Command.Consume();
						item.Handler.Execute();
					} else {
						item.Command.Consume();
					}
				}
			}
#if PROFILER
			ProfilerDatabase.CpuUsageFinished(usage, Owners.Empty, CpuUsage.Reasons.ProcessCommands, TypeIdentifier.Empty);
#endif // PROFILER
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
