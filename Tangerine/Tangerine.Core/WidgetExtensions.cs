﻿using System;
using Lime;

namespace Tangerine.Core
{
	public static class WidgetExtensions
	{
		public static void AddChangeWatcher<T>(this Widget widget, Func<T> getter, Action<T> action)
		{
			widget.Tasks.Add(new Property<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddChangeWatcher<T>(this Widget widget, IDataflowProvider<T> provider, Action<T> action)
		{
			widget.Tasks.Add(provider.DistinctUntilChanged().Consume(action));
		}

		public static void AddChangeLateWatcher<T>(this Widget widget, Func<T> getter, Action<T> action)
		{
			widget.LateTasks.Add(new Property<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddChangeLateWatcher<T>(this Widget widget, IDataflowProvider<T> provider, Action<T> action)
		{
			widget.LateTasks.Add(provider.DistinctUntilChanged().Consume(action));
		}

		public static float Top(this Widget widget) => widget.Y;
		public static float Bottom(this Widget widget) => widget.Y + widget.Height;
	}
}
