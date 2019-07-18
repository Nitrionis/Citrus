using System;

namespace Lime
{
	[NodeComponentDontSerialize]
	public class AwakeBehavior : NodeBehavior
	{
		public event Action<Node> Action;

		public override int Order => -1000100;

		public bool IsAwoken { get; private set; }

		protected override void OnRegister()
		{
			base.OnRegister();
			if (IsAwoken) {
				return;
			}
			Action?.Invoke(Owner);
			IsAwoken = true;
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			IsAwoken = false;
		}

		public override NodeComponent Clone()
		{
			var clone = (AwakeBehavior)base.Clone();
			clone.IsAwoken = false;
			return clone;
		}
	}

	[NodeComponentDontSerialize]
	public class UpdateBehavior : NodeBehavior
	{
		public UpdateHandler Updating;

		public override void Update(float delta) => Updating?.Invoke(delta);
		public override int Order => -1000000;

		public override NodeComponent Clone()
		{
			var clone = (UpdateBehavior)base.Clone();
			clone.Updating = null;
			return clone;
		}
	}

	[NodeComponentDontSerialize]
	public class UpdatedBehavior : NodeBehavior
	{
		public UpdateHandler Updated;

		public override void LateUpdate(float delta) => Updated?.Invoke(delta);
		public override int Order => 1000000;

		public override NodeComponent Clone()
		{
			var clone = (UpdatedBehavior)base.Clone();
			clone.Updated = null;
			return clone;
		}
	}

	[NodeComponentDontSerialize]
	public class TasksBehavior : NodeBehavior
	{
		public TaskList Tasks { get; private set; }

		public override void Update(float delta) => Tasks.Update(delta);
		public override int Order => -900000;

		protected override void OnOwnerChanged(Node oldOwner)
		{
			Tasks?.Stop();
			Tasks = Owner == null ? null : new TaskList(Owner);
		}

		public override NodeComponent Clone()
		{
			var clone = (TasksBehavior)base.Clone();
			clone.Tasks = null;
			return clone;
		}

		public override void Dispose()
		{
			Tasks?.Stop();
		}
	}

	[NodeComponentDontSerialize]
	public class LateTasksBehavior : NodeBehavior
	{
		public TaskList Tasks { get; private set; }

		public override void LateUpdate(float delta) => Tasks.Update(delta);
		public override int Order => 900000;

		protected override void OnOwnerChanged(Node oldOwner)
		{
			Tasks?.Stop();
			Tasks = Owner == null ? null : new TaskList(Owner);
		}

		public override NodeComponent Clone()
		{
			var clone = (LateTasksBehavior)base.Clone();
			clone.Tasks = null;
			return clone;
		}

		public override void Dispose()
		{
			Tasks?.Stop();
		}
	}
}
