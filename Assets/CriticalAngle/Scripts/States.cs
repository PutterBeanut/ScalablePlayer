using System;
using System.Collections;
using UnityEngine;

namespace CriticalAngle.ExpandablePlayer
{
    public partial class Player : MonoBehaviour
    {
        private bool isTransitioningCrouch;
        private bool isTransitioningUnCrouch;
        private bool isFullyCrouched;

        private float crouchTime;
        
        private IEnumerator Crouch()
        {
            this.isTransitioningCrouch = true;
            
            var beginCameraHeight = this.MovementSettings.StandingCameraHeight;
            var endCameraHeight = this.MovementSettings.CrouchedCameraHeight;

            var maxTime = this.MovementSettings.TimeToCrouch;

            var time = 0.0f;

            while (time < maxTime)
            {
                this.References.Camera.transform.localPosition = new Vector3(0.0f,
                    Mathf.Lerp(beginCameraHeight, endCameraHeight, time / maxTime));

                time += Time.deltaTime;
                this.crouchTime = time / maxTime;
                yield return null;
            }

            this.References.Camera.transform.localPosition = new Vector3(0.0f, endCameraHeight);

            var beginColliderHeight = this.MovementSettings.StandingColliderHeight;
            var endColliderHeight = this.MovementSettings.CrouchedColliderHeight;

            var center = (endColliderHeight - beginColliderHeight) / 2.0f;

            this.References.CharacterController.center = new Vector3(0.0f, center);
            this.References.CharacterController.height = endColliderHeight;
            
            this.isTransitioningCrouch = false;
            this.isFullyCrouched = true;
        }
        
        private IEnumerator UnCrouch()
        {
            this.isTransitioningUnCrouch = true;
            
            var beginCameraHeight = this.MovementSettings.CrouchedCameraHeight;
            var endCameraHeight = this.MovementSettings.StandingCameraHeight;

            var maxTime = this.MovementSettings.TimeToUncrouch;

            var time = 0.0f;

            while (time < maxTime)
            {
                this.References.Camera.transform.localPosition = new Vector3(0.0f,
                    Mathf.Lerp(beginCameraHeight, endCameraHeight, time / maxTime));

                time += Time.deltaTime;
                this.crouchTime = time / maxTime;
                yield return null;
            }

            this.References.Camera.transform.localPosition = new Vector3(0.0f, endCameraHeight);

            var colliderHeight = this.MovementSettings.StandingColliderHeight;

            this.References.CharacterController.center = Vector3.zero;
            this.References.CharacterController.height = colliderHeight;

            this.isTransitioningUnCrouch = false;
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

            public override void Update()
            {
                if (this.Player.crouchInput && !this.Player.isTransitioningCrouch && !this.Player.isFullyCrouched)
                    this.Player.StartCoroutine(this.Player.Crouch());
                else if (!this.Player.crouchInput && this.Player.isTransitioningCrouch)
                {
                    this.Player.StopCoroutine(this.Player.Crouch());
                    this.Player.isTransitioningCrouch = false;
                    
                    this.Player.StartCoroutine(this.Player.UnCrouch());
                }
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
                
                if (this.Player.crouchInput && !this.Player.isTransitioningCrouch && !this.Player.isFullyCrouched)
                    this.Player.StartCoroutine(this.Player.Crouch());
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
                
                if (this.Player.crouchInput && !this.Player.isTransitioningCrouch && !this.Player.isFullyCrouched)
                    this.Player.StartCoroutine(this.Player.Crouch());
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
    }
}