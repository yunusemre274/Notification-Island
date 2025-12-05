using System;
using NI.App.ViewModels;

namespace NI.App.Services
{
    /// <summary>
    /// Simple state machine for pet behavior.
    /// States: Idle, ClickReaction, Feeding, Jumping, Sleeping
    /// </summary>
    public class PetStateMachine
    {
        public PetState CurrentState { get; private set; } = PetState.Idle;

        public void TriggerClick()
        {
            CurrentState = PetState.ClickReaction;
            ScheduleReturnToIdle(600);
        }

        public void TriggerFeed()
        {
            CurrentState = PetState.Feeding;
            ScheduleReturnToIdle(1000);
        }

        public void TriggerJump()
        {
            CurrentState = PetState.Jumping;
            ScheduleReturnToIdle(400);
        }

        public void TriggerSleep()
        {
            CurrentState = PetState.Sleeping;
        }

        public void WakeUp()
        {
            if (CurrentState == PetState.Sleeping)
                CurrentState = PetState.Idle;
        }

        private void ScheduleReturnToIdle(int delayMs)
        {
            System.Threading.Tasks.Task.Delay(delayMs).ContinueWith(_ =>
            {
                if (CurrentState != PetState.Sleeping)
                    CurrentState = PetState.Idle;
            });
        }
    }
}
