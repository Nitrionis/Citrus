using System;
using ProtoBuf;
using System.Collections.Generic;

namespace Lime
{
	using StateFunc = Func<IEnumerator<int>>;
	using System.Diagnostics;

	[ProtoContract]
	public class Button : Widget
	{
		[ProtoMember(1)]
		public override string Text { get; set; }

		[ProtoMember(2)]
		public bool Enabled { get; set; }

		/// <summary>
		/// Indicates whether a button has draggable behaviour. 
		/// It means that if a user has quickly passed his finger through the button it would not be pressed.
		/// </summary>
		[ProtoMember(3)]
		public bool Draggable { get; set; }

		public override Action Clicked { get; set; }
		
		private SimpleText textPresenter;
		private bool wasClicked;
		private StateMachine stateMachine = new StateMachine();
		private StateFunc State
		{
			get { return stateMachine.State; }
			set { stateMachine.SetState(value); }
		}

		/// <summary>
		/// The minimum distance which finger should pass through the button,
		/// in order to avoid button click. 
		/// For Draggable buttons only.
		/// </summary>
		private const float DragDistanceThreshold = 15;

		/// <summary>
		/// The period of time while drag detection is working, 
		/// since a finger touched the button.
		/// For Draggable buttons only.
		/// </summary>
		private const float DragDetectionTime = 0.15f;

		/// <summary>
		/// Once button has been clicked, its active area includes circle with a given radius.
		/// </summary>
		public static float ButtonEffectiveRadius = 400;

		public Button()
		{
			Enabled = true;
			// On the current frame the button contents may not be loaded, 
			// so delay its initialization until the next frame.
			State = InitialState;
		}

		private IEnumerator<int> InitialState()
		{
			yield return 0;
			State = NormalState;
		}

		public override bool WasClicked()
		{
			return wasClicked;
		}

		public override Node DeepCloneFast()
		{
			var clone = (Button)base.DeepCloneFast();
			clone.stateMachine = new StateMachine();
			clone.State = clone.NormalState;
			return clone;
		}

		private IEnumerator<int> NormalState()
		{
			if (TheActiveWidget == this) {
				TheActiveWidget = null;
			}
			TryRunAnimation("Normal");
			while (true) {
#if iOS
				if (Input.WasMousePressed() && HitTest(Input.MousePosition) && TheActiveWidget == null) {
					World.Instance.ActiveWidget = this;
					if (Draggable) {
						State = DetectDraggingState;
					} else {
						State = PressedState;
					}
				}
#else
				if (HitTest(Input.MousePosition) && TheActiveWidget == null) {
					State = FocusedState;
				}
#endif
				yield return 0;
			}
		}

		private static IEnumerable<int> TimeDelay(float secs)
		{
			var time = DateTime.Now;
			while ((DateTime.Now - time).TotalSeconds < secs) {
				yield return 0;
			}
		}

#if !iOS
		private IEnumerator<int> FocusedState()
		{
			World.Instance.ActiveWidget = this;
			TryRunAnimation("Focus");
			while (true) {
				if (!HitTest(Input.MousePosition)) {
					State = NormalState;
				} else if (Input.WasMousePressed()) {
					if (Draggable) {
						State = DetectDraggingState;
					} else {
						State = PressedState;
					}
				}
				yield return 0;
			}
		}
#endif

		private IEnumerator<int> DetectDraggingState()
		{
			var mouse = Input.MousePosition;
			foreach (var t in TimeDelay(DragDetectionTime)) {
				yield return 0;
				if ((mouse - Input.MousePosition).Length > DragDistanceThreshold) {
					State = NormalState;
				} else if (!Input.IsMousePressed() && HitTest(Input.MousePosition)) {
					State = QuickClickOnDraggableButtonState;
					yield break;
				}
			}
			State = PressedState;
		}

		private IEnumerator<int> QuickClickOnDraggableButtonState()
		{
			if (TryRunAnimation("Press")) {
				while (IsRunning) {
					yield return 0;
				}
			}
			HandleClick();
			State = ReleaseState;
		}

		private IEnumerator<int> PressedState()
		{
			var mouse = Input.VisibleMousePosition;
			TryRunAnimation("Press");
			bool wasPressed = true;
			while (true) {
				bool isPressed = HitTest(Input.VisibleMousePosition) ||
					(Input.VisibleMousePosition - this.GlobalCenter).Length < ButtonEffectiveRadius;
				if (!Input.IsMousePressed()) {
					if (isPressed) {
						HandleClick();
					}
					State = ReleaseState;
				} else if (wasPressed && !isPressed) {
					TryRunAnimation("Release");
				} else if (!wasPressed && isPressed) {
					TryRunAnimation("Press");
				}
				yield return 0;
				wasPressed = isPressed;
			}
		}

		private void HandleClick()
		{
			if (Clicked != null) {
#if !iOS
				// XXX
				// �����������, ���� �����������
/*				if (Debug.BreakOnButtonClick) {
					Debugger.Break();
				}*/
#endif
				Clicked();
			}
			wasClicked = true;
		}

		private IEnumerator<int> ReleaseState()
		{
			// buz: ��� ��������� ������ ������������ �� ������� �� ���������, ���� ��������� ����������� �������� ������� ����������
			if (TheActiveWidget == this) {
				TheActiveWidget = null;
			}
			if (CurrentAnimation != "Release") {
				if (TryRunAnimation("Release")) {
					while (IsRunning) {
						yield return 0;
					}
				}
			}
#if iOS
			State = NormalState;
#else
			if (HitTest(Input.MousePosition)) {
				State = FocusedState;
			} else {
				State = NormalState;
			}
#endif
		}

		private IEnumerator<int> DisabledState()
		{
			if (TheActiveWidget == this) {
				TheActiveWidget = null;
			}
			TryRunAnimation("Disable");
			while (IsRunning) {
				yield return 0;
			}
			while (!Enabled) {
				yield return 0;
			}
			TryRunAnimation("Enable");
			while (IsRunning) {
				yield return 0;
			}
			State = NormalState;
		}

		private void UpdateLabel()
		{
			if (textPresenter == null) {
				TryFind<SimpleText>("TextPresenter", out textPresenter);
			}
			if (textPresenter != null) {
				textPresenter.Text = Text;
			}
		}

		public override void Update(int delta)
		{
			wasClicked = false;
			if (GloballyVisible) {
				stateMachine.Advance();
				UpdateLabel();
				SyncActiveWidget();
			}
			// buz: ������ ������� ������������ ������ �� ����� �� ��, �� �������� ������� ��������� �� ����� �����.
			if (!Enabled && State != DisabledState && State != ReleaseState) {
				State = DisabledState;
			}
			base.Update(delta);
		}

		void SyncActiveWidget()
		{
			if (!Enabled) {
				return;
			}
			if (World.Instance.ActiveWidget != this && State != NormalState) {
				if (CurrentAnimation != "Release") {
					State = NormalState;
				}
			}
			if (TheActiveWidget == this) {
				World.Instance.IsActiveWidgetUpdated = true;
			}
		}

		private static Widget TheActiveWidget
		{
			get { return World.Instance.ActiveWidget; }
			set { World.Instance.ActiveWidget = value; }
		}

#region StateMachine
		class StateMachine
		{
			private IEnumerator<int> stateHandler;
			public StateFunc State { get; private set; }

			public void SetState(StateFunc state)
			{
				State = state;
				stateHandler = state();
				stateHandler.MoveNext();
			}

			public void Advance()
			{
				stateHandler.MoveNext();
			}
		}
#endregion
	}
}
