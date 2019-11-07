namespace Lime.KGDCitronLifeCycle
{
	public static partial class CitronLifeCycle
	{
		private class AnimationStoppedProcessor : NodeProcessor
		{
			private AnimationSystem animationSystem;

			protected internal override void Start()
			{
				base.Start();
				animationSystem = Manager.ServiceProvider.GetService<AnimationSystem>();
			}

			protected internal override void Stop()
			{
				base.Stop();
				animationSystem = null;
			}

			protected internal override void Update(float delta)
			{
				base.Update(delta);
				animationSystem?.ConsumePendingActions();
			}
		}
	}
}
