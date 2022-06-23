using System;
using System.Collections;
using UnityEditor.TextCore.Text;
using UnityEngine;

namespace CriticalAngle.ExpandablePlayer
{
    public partial class Player : MonoBehaviour
    {
        private IEnumerator Crouch(float time = 0.0f)
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
                    this.StartCoroutine(this.Uncrouch(maxTime - time));
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
        
        private IEnumerator Uncrouch(float time = 0.0f)
        {
            this.UpdateStateParameter("Should Crouch", false);
            
            var beginCameraHeight = this.MovementSettings.CrouchedCameraHeight;
            var endCameraHeight = this.MovementSettings.StandingCameraHeight;

            var maxTime = this.MovementSettings.TimeToCrouch;

            while (time < maxTime)
            {
                if (this.crouchInput)
                {
                    this.StartCoroutine(this.Crouch(maxTime - time));
                    yield break;
                }

                if (!this.CanUncrouch())
                {
                    this.References.Camera.transform.localPosition = beginCameraHeight.V3Y();
                    this.UpdateStateParameter("Is Transitioning Crouch", false);
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

        private bool CanUncrouch()
        {
            var offset = this.transform.position -
                         (this.MovementSettings.StandingColliderHeight / 2.0f - this.GeneralSettings.Radius).V3Y();

            if (Physics.SphereCast(offset,
                    this.GeneralSettings.Radius - this.References.CharacterController.skinWidth,
                    Vector3.up,
                    out var hit,
                    this.MovementSettings.StandingColliderHeight - this.GeneralSettings.Radius * 2.0f))
            {
                print(hit.collider.name);
                return false;
            }
            else return true;
        }

        private bool CanUnCrouchAir()
        {
            var difference = this.MovementSettings.StandingColliderHeight -
                             this.MovementSettings.CrouchedColliderHeight - this.GeneralSettings.Radius;

            return !Physics.SphereCast(this.transform.position - difference.V3Y(), this.GeneralSettings.Radius,
                Vector3.down, out var hit, this.MovementSettings.StandingColliderHeight / 2.0f);
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

            public override void OnStateEnter()
            {
                this.Player.References.Camera.transform.localPosition =
                    this.Player.MovementSettings.StandingCameraHeight.V3Y();
                this.Player.References.CharacterController.center = Vector3.zero;
                this.Player.References.CharacterController.height = this.Player.MovementSettings.StandingColliderHeight;
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

                this.Player.UpdateStateParameter("Can Uncrouch", this.Player.CanUncrouch());
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

            public override void OnStateEnter()
            {
                var beginColliderHeight = this.Player.MovementSettings.StandingColliderHeight;
                var endColliderHeight = this.Player.MovementSettings.CrouchedColliderHeight;

                var center = (beginColliderHeight - endColliderHeight) / 2.0f;

                this.Player.References.CharacterController.center = center.V3Y();
                this.Player.References.CharacterController.height = endColliderHeight;

                var cam = this.Player.References.Camera.transform;
                var oldPos = cam.localPosition.y;
                cam.localPosition = this.Player.MovementSettings.StandingCameraHeight.V3Y();
                
                // ReSharper disable once Unity.InefficientPropertyAccess
                this.Player.transform.Translate((oldPos - cam.localPosition.y).V3Y());
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
                
                this.Player.UpdateStateParameter("Can Uncrouch Air", this.Player.CanUnCrouchAir());
            }
        }

        protected class AirCrouchToCrouchState : PlayerState
        {
            public AirCrouchToCrouchState(Player player) : base(player)
            {
            }

            public override string GetStateName()
            {
                return "Air Crouch To Crouch";
            }

            public override void OnStateEnter()
            {
                var beginColliderHeight = this.Player.MovementSettings.StandingColliderHeight;
                var endColliderHeight = this.Player.MovementSettings.CrouchedColliderHeight;

                var center = (endColliderHeight - beginColliderHeight) / 2.0f;

                this.Player.References.CharacterController.center = center.V3Y();
                this.Player.References.CharacterController.height = endColliderHeight;

                var cam = this.Player.References.Camera.transform;
                var oldPos = cam.localPosition.y;
                cam.localPosition = this.Player.MovementSettings.CrouchedCameraHeight.V3Y();
                
                // ReSharper disable once Unity.InefficientPropertyAccess
                this.Player.transform.Translate((oldPos - cam.localPosition.y).V3Y());
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
                
                this.Player.StartCoroutine(this.Player.Uncrouch());
            }

            public override void OnPostStateExit()
            {
                this.Player.UpdateStateParameter("Finished Crouch Callback", false);
            }
        }
    }
}