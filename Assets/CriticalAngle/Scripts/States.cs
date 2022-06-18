using System;
using System.Collections;
using UnityEngine;

namespace CriticalAngle.ExpandablePlayer
{
    public partial class Player : MonoBehaviour
    {
        private IEnumerator Crouch(float time = 0.0f, bool cameFromUnCrouch = false)
        {
            this.UpdateStateParameter("Should Uncrouch", false);
            
            var beginCameraHeight = this.MovementSettings.StandingCameraHeight;
            var endCameraHeight = this.MovementSettings.CrouchedCameraHeight;

            var maxTime = this.MovementSettings.TimeToCrouch;

            while (time < maxTime)
            {
                if (!this.IsGrounded)
                {
                    this.References.Camera.transform.localPosition = beginCameraHeight.V3Y();
                    this.UpdateStateParameter("Is Transitioning Crouch", false);
                    yield break;
                }
                
                if (!this.crouchInput)
                {
                    this.StartCoroutine(this.UnCrouch(maxTime - time, true));
                    yield break;
                }

                this.References.Camera.transform.localPosition =
                    Mathf.Lerp(beginCameraHeight, endCameraHeight, time / maxTime).V3Y();

                time += Time.deltaTime;
                yield return null;
            }

            this.References.Camera.transform.localPosition = endCameraHeight.V3Y();

            var beginColliderHeight = this.MovementSettings.StandingColliderHeight;
            var endColliderHeight = this.MovementSettings.CrouchedColliderHeight;

            var center = (endColliderHeight - beginColliderHeight) / 2.0f;

            this.References.CharacterController.center = center.V3Y();
            this.References.CharacterController.height = endColliderHeight;
            
            this.UpdateStateParameter("Should Crouch", true);
            this.UpdateStateParameter("Is Transitioning Crouch", false);
        }
        
        private IEnumerator UnCrouch(float time = 0.0f, bool cameFromCrouch = false)
        {
            this.UpdateStateParameter("Should Crouch", false);
            
            var beginCameraHeight = this.MovementSettings.CrouchedCameraHeight;
            var endCameraHeight = this.MovementSettings.StandingCameraHeight;

            var maxTime = this.MovementSettings.TimeToCrouch;

            while (time < maxTime)
            {
                if (this.crouchInput)
                {
                    this.StartCoroutine(this.Crouch(maxTime - time, true));
                    yield break;
                }
                
                this.References.Camera.transform.localPosition =
                    Mathf.Lerp(beginCameraHeight, endCameraHeight, time / maxTime).V3Y();

                time += Time.deltaTime;
                yield return null;
            }

            this.References.Camera.transform.localPosition = endCameraHeight.V3Y();

            var colliderHeight = this.MovementSettings.StandingColliderHeight;

            this.References.CharacterController.center = Vector3.zero;
            this.References.CharacterController.height = colliderHeight;
            
            this.UpdateStateParameter("Should Uncrouch", true);
            this.UpdateStateParameter("Is Transitioning Crouch", false);
        }
        
        protected class IdleState : PlayerState
        {
            public IdleState(Player player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Idle";
            }
        }

        protected class WalkState : PlayerState
        {
            public WalkState(Player player) : base(player)
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
            public RunState(Player player) : base(player)
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
            public JumpState(Player player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Jump";
            }

            public override void OnStateEnter()
            {
                this.Player.snapAmount = 0.0m;
                this.Player.Velocity.y = Mathf.Sqrt(this.Player.MovementSettings.JumpForce * -2.0f * Physics.gravity.y);
            }
        }

        protected class AirState : PlayerState
        {
            public AirState(Player player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Air";
            }

            public override void Update()
            {
                this.Player.snapAmount = 0.0m;
                
                switch (this.Player.MovementSettings.AirStrafe)
                {
                    case PlayerMovementSettings.AirStrafeType.Normal:
                        this.Player.NormalAirAccelerate(this.Player.MovementSettings.MaxAirAcceleration,
                            this.Player.MovementSettings.AirAcceleration);
                        break;
                    case PlayerMovementSettings.AirStrafeType.Acceleration:
                        this.Player.AirAccelerate(this.Player.MovementSettings.MaxAirAcceleration,
                            this.Player.MovementSettings.AirAcceleration);
                        break;
                }
            }
        }

        protected class CrouchState : PlayerState
        {
            public CrouchState(Player player) : base(player)
            {
            }
            

            public override string GetStateName()
            {
                return "Crouch";
            }

            public override void OnStateEnter()
            {
                this.Player.UpdateStateParameter("Should Crouch", false);
            }

            public override void OnPostStateExit()
            {
                this.Player.UpdateStateParameter("Should Uncrouch", false);
            }

            public override void Update()
            {
                this.Player.GroundAccelerate(this.Player.MovementSettings.Crouching.MaxSpeed,
                    this.Player.MovementSettings.Crouching.Acceleration);
            }
        }

        protected class AirCrouchState : PlayerState
        {
            public AirCrouchState(Player player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Air Crouch";
            }

            public override void Update()
            {
                switch (this.Player.MovementSettings.AirStrafe)
                {
                    case PlayerMovementSettings.AirStrafeType.Normal:
                        this.Player.NormalAirAccelerate(this.Player.MovementSettings.MaxAirAcceleration,
                            this.Player.MovementSettings.AirAcceleration);
                        break;
                    case PlayerMovementSettings.AirStrafeType.Acceleration:
                        this.Player.AirAccelerate(this.Player.MovementSettings.MaxAirAcceleration,
                            this.Player.MovementSettings.AirAcceleration);
                        break;
                }
            }
        }
        
        protected class ToCrouchState : PlayerState
        {
            public ToCrouchState(Player player) : base(player)
            {
            }
            
            public override string GetStateName()
            {
                return "To Crouch";
            }

            public override void OnStateEnter()
            {
                this.Player.UpdateStateParameter("Is Transitioning Crouch", true);
                this.Player.UpdateStateParameter("Finished Crouch Callback", true);
                
                this.Player.StartCoroutine(this.Player.Crouch());
            }

            public override void OnPostStateExit()
            {
                this.Player.UpdateStateParameter("Finished Crouch Callback", false);
            }
        }
        
        protected class FromCrouchState : PlayerState
        {
            public FromCrouchState(Player player) : base(player)
            {
            }
            
            public override string GetStateName()
            {
                return "From Crouch";
            }

            public override void OnStateEnter()
            {
                this.Player.UpdateStateParameter("Is Transitioning Crouch", true);
                this.Player.UpdateStateParameter("Finished Crouch Callback", true);
                
                this.Player.StartCoroutine(this.Player.UnCrouch());
            }

            public override void OnPostStateExit()
            {
                this.Player.UpdateStateParameter("Finished Crouch Callback", false);
            }
        }
    }
}