using UnityEngine;

namespace CriticalAngle
{
    public partial class ScalablePlayer : MonoBehaviour
    {
        protected class IdleState : PlayerState
        {
            public IdleState(ScalablePlayer player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Idle";
            }
        }

        protected class WalkState : PlayerState
        {
            public WalkState(ScalablePlayer player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Walk";
            }

            public override void Update()
            {
                this.Player.GroundAccelerate(this.Player.MovementSettings.Walking.MaxSpeed,
                    this.Player.MovementSettings.Walking.Acceleration);
            }
        }
        
        protected class RunState : PlayerState
        {
            public RunState(ScalablePlayer player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Run";
            }
            
            public override void Update()
            {
                this.Player.GroundAccelerate(this.Player.MovementSettings.Running.MaxSpeed,
                    this.Player.MovementSettings.Running.Acceleration);
            }
        }

        protected class JumpState : PlayerState
        {
            public JumpState(ScalablePlayer player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Jump";
            }

            public override void OnStateEnter()
            {
                this.Player.Velocity.y = Mathf.Sqrt(this.Player.MovementSettings.JumpForce * -2.0f * Physics.gravity.y);
            }
        }

        protected class AirState : PlayerState
        {
            public AirState(ScalablePlayer player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Air";
            }

            public override void Update()
            {
                this.Player.AirAccelerate(this.Player.MovementSettings.MaxAirAcceleration,
                    this.Player.MovementSettings.AirAcceleration);
            }
        }
    }
}