using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CriticalAngle
{
    public partial class ScalablePlayer : MonoBehaviour
    {
        #region Inspector Fields
        
        [SerializeField] protected PlayerReferences References;
        [SerializeField] protected PlayerGeneralSettings GeneralSettings;
        [SerializeField] protected PlayerMovementSettings MovementSettings;
        [SerializeField] protected PlayerPhysicsSettings PhysicsSettings;
        [SerializeField] protected PlayerInputSettings InputSettings;

        #endregion

        #region Public Fields

        [HideInInspector] public Vector3 Velocity;
        [HideInInspector] public bool IsGrounded;
        
        [HideInInspector] public List<StateParameter> Parameters = new();
        [HideInInspector] public List<State> States = new();

        #endregion
        
        #region Private Fields

        protected Vector2 directionalInput;
        protected Vector2 lookInput;
        protected bool jumpInput;
        protected bool crouchInput;
        protected bool runInput;
        
        protected bool isCrouched;
        protected bool isTransitioningCrouched;
        
        protected float xRotation;

        protected int activeState;

        #endregion
        
        #region Custom Inspector Functions
#if UNITY_EDITOR
        
        public void SetupReferences()
        {
            if (this.References.Camera == null)
            {
                if (this.transform.childCount > 0)
                    this.References.Camera = this.transform.GetChild(0).GetComponent<Camera>();

                if (this.References.Camera == null)
                {
                    this.References.Camera = new GameObject("Camera").AddComponent<Camera>();
                    this.References.Camera.transform.parent = this.transform;
                }
            }

            if (this.References.CharacterController == null)
            {
                this.References.CharacterController = this.GetComponent<CharacterController>();

                if (this.References.CharacterController == null)
                    this.References.CharacterController = this.gameObject.AddComponent<CharacterController>();
            }
        }

        public void ApplyVariables()
        {
            this.References.Camera.transform.localPosition = new Vector3(0.0f,
                this.MovementSettings.StandingCameraHeight, 0.0f);

            if (this.References.CharacterController != null)
            {
                this.References.CharacterController.height = this.MovementSettings.StandingColliderHeight;
                this.References.CharacterController.radius = this.GeneralSettings.Radius;
                this.References.CharacterController.slopeLimit = this.GeneralSettings.SlopeLimit;
                this.References.CharacterController.stepOffset = this.GeneralSettings.StepOffset;

                if (this.GeneralSettings.StepOffset > this.References.CharacterController.height)
                    this.GeneralSettings.StepOffset = this.References.CharacterController.height;
            }

            if (this.GeneralSettings.MinLookAngle > this.GeneralSettings.MaxLookAngle)
                this.GeneralSettings.MaxLookAngle = this.GeneralSettings.MinLookAngle;
        }

        public void HideComponents()
        {
            if (this.References.CharacterController != null)
                this.References.CharacterController.hideFlags = HideFlags.HideInInspector;
        }


#endif
        #endregion

        #region Default Functions

        protected void OnEnable()
        {
        }

        protected virtual void Awake()
        {
            this.ValidateComponents();
            DisableCursor();
            this.InitializeInputs();
        }

        protected void Start()
        {
        }

        protected virtual void Update()
        {
            this.CheckGrounded();
            
            if (this.IsGrounded)
            {
                this.CalculateFriction();
                this.LimitVelocity();
            }
            
            this.UpdateStateParameter("Is Moving", this.directionalInput.sqrMagnitude > 0.0f);

            this.References.CharacterController.Move(
                new Vector3(this.Velocity.x, 0.0f, this.Velocity.z) * Time.deltaTime);
            this.UseGravity();
        }

        protected virtual void LateUpdate()
        {
            this.HandleRotation();
        }

        protected virtual void FixedUpdate()
        {
        }

        protected virtual void OnDestroy()
        {
            this.InputSettings.Actions.Disable();
        }

        #endregion

        #region Initialization
        
        private void ValidateComponents()
        {
            if (this.References.Camera == null)
                this.Warning("The Camera component is missing! Press the \"Setup References\" button to fix it.");
            
            if (this.References.CharacterController == null)
                this.Warning("The CharacterController component is missing! Press the \"Setup References\" button to fix it.");
            
            if (this.InputSettings.Actions == null)
                this.Warning("The Actions parameter is missing! Set up an InputActionAsset and drag it in or use the provided one to fix it.");
            else
            {
                var mapping = this.InputSettings.Actions.FindActionMap(this.InputSettings.Mapping);
                if (mapping == null)
                {
                    this.Warning("Cannot find the input mapping provided: " + this.InputSettings.Mapping);
                    return;
                }

                if (mapping.FindAction(this.InputSettings.DirectionalAction) == null)
                {
                    this.Warning("Cannot find the Directional Action provided: " + this.InputSettings.DirectionalAction);
                    return;
                }
                
                if (mapping.FindAction(this.InputSettings.LookAction) == null)
                {
                    this.Warning("Cannot find the Look Action provided: " + this.InputSettings.LookAction);
                    return;
                }
                
                if (mapping.FindAction(this.InputSettings.JumpAction) == null)
                {
                    this.Warning("Cannot find the Jump Action provided: " + this.InputSettings.JumpAction);
                    return;
                }
                
                if (mapping.FindAction(this.InputSettings.CrouchAction) == null)
                {
                    this.Warning("Cannot find the Crouch Action provided: " + this.InputSettings.CrouchAction);
                    return;
                }
                
                if (mapping.FindAction(this.InputSettings.RunAction) == null)
                {
                    this.Warning("Cannot find the Run Action provided: " + this.InputSettings.RunAction);
                    return;
                }
            }
        }

        private void InitializeInputs()
        {
            var actions = this.InputSettings.Actions;
            actions.Enable();

            var mapping = actions.FindActionMap(this.InputSettings.Mapping);
            
            mapping[this.InputSettings.DirectionalAction].performed += this.OnDirectionalInput;
            mapping[this.InputSettings.DirectionalAction].canceled += this.OnDirectionalInput;
            
            mapping[this.InputSettings.LookAction].performed += this.OnLookInput;
            mapping[this.InputSettings.LookAction].canceled += this.OnLookInput;
            
            mapping[this.InputSettings.JumpAction].performed += this.OnJumpInput;
            mapping[this.InputSettings.JumpAction].canceled += this.OnJumpInput;
            
            mapping[this.InputSettings.CrouchAction].performed += this.OnCrouchInput;
            mapping[this.InputSettings.CrouchAction].canceled += this.OnCrouchInput;
            
            mapping[this.InputSettings.RunAction].performed += this.OnRunInput;
            mapping[this.InputSettings.RunAction].canceled += this.OnRunInput;
        }
        
        private static void EnableCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private static void DisableCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        #endregion

        #region State Management

        protected void UpdateStateParameter(string name, bool value)
        {
            this.Parameters[this.FindStateParameter(name)].Value = value;
            this.CheckStateConditions();
        }

        private int FindStateParameter(string name)
        {
            var param = -1;
            for (var i = 0; i < this.Parameters.Count; i++)
            {
                if (this.Parameters[i].Name == name)
                    param = i;
            }

            if (param == -1)
                this.Error("Cannot find a parameter with the name `" + name + "`");
            return param;
        }

        private void CheckStateConditions()
        {
            foreach (var transition in this.States[this.activeState].Transitions)
            {
                var conditionsMet = true;
                foreach (var condition in transition.Conditions)
                {
                    var value = condition.Value == StateCondition.Values.True;
                    if (this.Parameters[condition.Name].Value == value) continue;
                    
                    conditionsMet = false;
                    break;
                }

                if (!conditionsMet) continue;
                this.SetState(transition.ToState);
                break;
            }
        }

        private void SetState(int stateIndex)
        {
            print("New state: " + this.States[stateIndex].Name);
            this.activeState = stateIndex;
        }
        
        #endregion
        
        #region Character

        protected virtual Vector3 InputVectorToDirection(Vector2 input)
        {
            return new Vector3(input.x, 0.0f, input.y);
        }

        protected virtual Vector3 WorldToLocalSpace(Vector3 input)
        {
            return this.transform.TransformDirection(input);
        }
        
        protected virtual void UseGravity()
        {
            if (this.IsGrounded && this.Velocity.y < 0.0f)
                this.Velocity.y = -2.0f;
            
            this.Velocity += new Vector3(0.0f, Physics.gravity.y * Time.deltaTime);
            this.References.CharacterController.Move(Vector3.up * (this.Velocity.y * Time.deltaTime));
        }

        protected virtual void LimitVelocity()
        {
            if (this.Velocity.magnitude < this.GeneralSettings.StopSpeed)
                this.Velocity = Vector3.zero;
        }

        protected virtual void CalculateFriction()
        {
            var speed = this.Velocity.magnitude;
            if (speed == 0.0f) return;
            
            var friction = this.GeneralSettings.Friction;
            var control = Mathf.Max(speed, this.GeneralSettings.StopSpeed);
            var drop = control * friction * Time.deltaTime;
            var newSpeed = Mathf.Max(speed - drop, 0.0f) / speed;

            this.Velocity *= newSpeed;
        }

        protected virtual void HandleRotation()
        {
            var look = this.lookInput * Time.deltaTime;
            
            this.transform.Rotate(0.0f, look.x * this.GeneralSettings.Sensitivity
                                               * this.GeneralSettings.SensitivityY, 0.0f);

            this.xRotation -= look.y * this.GeneralSettings.Sensitivity
                                   * this.GeneralSettings.SensitivityX;
            
            this.xRotation = Mathf.Clamp(this.xRotation,
                this.GeneralSettings.MinLookAngle, this.GeneralSettings.MaxLookAngle);
            
            this.References.Camera.transform.localEulerAngles = new Vector3(this.xRotation, 0.0f);
        }

        protected virtual void CheckGrounded()
        {
            var pos = this.transform.position + this.References.CharacterController.center;
            var pos2 = this.References.CharacterController.height / 4.0f;
            var radius = Mathf.Lerp(0.0f, this.GeneralSettings.Radius,
                this.GeneralSettings.SlopeLimit / 90.0f);

            // ReSharper disable once Unity.PreferNonAllocApi
            var colliders = Physics.OverlapCapsule(pos, pos - new Vector3(0.0f, pos2 + 0.1f, 0.0f),
                this.GeneralSettings.Radius, this.GeneralSettings.GroundMask);

            this.IsGrounded = colliders.Length > 0;
        }
        
        protected virtual void Accelerate(Vector3 direction, float magnitude, float speed, float accel)
        {
            var wishSpeed = magnitude;

            // Cap speed
            if (wishSpeed > speed)
                wishSpeed = speed;

            // Determine veer amount
            var currentSpeed = Vector3.Dot(this.Velocity, direction);

            // See how much to add
            var addSpeed = wishSpeed - currentSpeed;

            // If not adding any, done.
            if (addSpeed <= 0)
                return;

            // Determine acceleration speed after acceleration
            var accelerationSpeed = accel * magnitude * Time.deltaTime;

            // Cap it
            if (accelerationSpeed > addSpeed)
                accelerationSpeed = addSpeed;

            this.Velocity += accelerationSpeed * direction;
        }

        #endregion

        #region Input Callbacks

        private void OnDirectionalInput(InputAction.CallbackContext ctx)
        {
            this.directionalInput = ctx.ReadValue<Vector2>();
        }
        
        private void OnLookInput(InputAction.CallbackContext ctx)
        {
            this.lookInput = ctx.ReadValue<Vector2>();
        }
        
        private void OnJumpInput(InputAction.CallbackContext ctx)
        {
            this.jumpInput = (int) ctx.ReadValue<float>() == 1;
        }
        
        private void OnCrouchInput(InputAction.CallbackContext ctx)
        {
            this.crouchInput = (int) ctx.ReadValue<float>() == 1;
        }
        
        private void OnRunInput(InputAction.CallbackContext ctx)
        {
            this.runInput = (int) ctx.ReadValue<float>() == 1;
        }

        #endregion

        #region Debugging

        protected virtual void Error(string text)
        {
            Debug.LogError(this.GetType() + ": " + text);
        }
        
        protected virtual void Warning(string text)
        {
            Debug.LogWarning(this.GetType() + ": " + text);
        }

        #endregion

        #region Settings

        /// <summary>
        /// References to components that will be needed throughout the code.
        /// </summary>
        [Serializable]
        public class PlayerReferences
        {
            public Camera Camera;
            public CharacterController CharacterController;
        }
        
        /// <summary>
        /// Miscellaneous settings to be used throughout the code.
        /// </summary>
        [Serializable]
        public class PlayerGeneralSettings
        {
            [Header("Player")]
            
            [Tooltip("The radius of our capsule collider.")] [Min(0.0f)]
            public float Radius;
            [Tooltip("The max slope angle (in degrees) that we can travel.")] [Range(1, 90)]
            public int SlopeLimit;
            [Tooltip("The maximum height of a surface that we can step up onto.")] [Min(0.0f)]
            public float StepOffset;
            [Tooltip("The amount of linear friction to be applied to the player while braking.")]
            public float Friction;
            [Tooltip("The minimum speed at which the player should stop moving.")]
            public float StopSpeed;
            [Tooltip("Select only the layer that the ground is on using this layer mask. This will allow the player to filter out collisions when checking for if we're grounded.")]
            public LayerMask GroundMask;
            
            [Space] [Header("Camera")]
            
            [Tooltip("How fast should the player be able to look around?")]
            public float Sensitivity;
            [Tooltip("How fast should the player be able to look around on the X axis?")]
            public float SensitivityX;
            [Tooltip("How fast should the player be able to look around on the Y axis?")]
            public float SensitivityY;
            [Tooltip("The lowest that the camera can look down.")]
            public float MinLookAngle;
            [Tooltip("The highest that the camera can look down.")]
            public float MaxLookAngle;

            public PlayerGeneralSettings()
            {
                this.Radius = 0.5f;
                this.SlopeLimit = 45;
                this.StepOffset = 0.3f;
                this.Friction = 10.0f;
                this.StopSpeed = 0.1f;
                this.Sensitivity = 10.0f;
                this.SensitivityX = 1.0f;
                this.SensitivityY = 1.0f;
                this.MinLookAngle = -90.0f;
                this.MaxLookAngle = 90.0f;
            }
        }

        /// <summary>
        /// Settings to control how the player travels.
        /// </summary>
        [Serializable]
        public class PlayerMovementSettings
        {
            [Header("Movement Parameters")]
            
            [Tooltip("Settings for when the player is in the Walk state.")]
            public MovementParameters Walking;
            [Tooltip("Settings for when the player is in the Run state.")]
            public MovementParameters Running;
            [Tooltip("Settings for when the player is in the Crouch state.")]
            public MovementParameters Crouching;
            
            [Space] [Header("Crouching")]

            [Tooltip("How much time it take for the camera to move from its standing position to its crouched position")]
            public float TimeToCrouch;
            [Tooltip("How much time it take for the camera to move from its crouched position to its standing position")]
            public float TimeToUncrouch;
            
            [Space]
            
            [Tooltip("Where should the camera be when we are standing?")]
            public float StandingCameraHeight;
            [Tooltip("Where should the camera be when we are crouched?")]
            public float CrouchedCameraHeight;
            [Tooltip("How tall should our collider be while standing?")]
            public float StandingColliderHeight;
            [Tooltip("How tall should our collider be while crouched?")]
            public float CrouchedColliderHeight;
            
            [Space]
            
            [Tooltip("Should we be able to jump in the air when we are fully crouched?")]
            public bool CanJumpWhileCrouched;
            [Tooltip("Should we be able to jump while transitioning form standing to crouched?")]
            public bool CanJumpWhileTransitioningCrouch;
            [Tooltip("Should we have to hold the crouch key to stay crouched or press it to toggle it?")]
            public bool ToggleCrouch;

            [Space] [Header("Jumping")]
            
            [Tooltip("Are we allowed to jump?")]
            public bool CanJump;
            [Tooltip("The upward force added when the jump key is pressed.")]
            public float JumpForce;

            [Space] [Header("Falling")]

            [Tooltip("Are we allowed to strafe in air? This will override the `AirControl` property with air strafing code.")]
            public bool CanAirStrafe;
            [Tooltip("Should we be able to crouch in air? This will cause the the feet to be raised as opposed to the head being lowered.")]
            public bool CanCrouchWhileFalling;
            
            [Space]
            
            [Tooltip("What is the maximum speed we should be able to travel while air strafing? Ignored if `CanAirStrafe` is disabled.")]
            public float MaxAirAcceleration;
            [Tooltip("The multiplier for how fast we should travel while air strafing.")]
            public float AirAcceleration;

            public PlayerMovementSettings()
            {
                this.Walking = new MovementParameters(true, 4.0f, 10.0f);
                this.Running = new MovementParameters(false, 6.0f, 10.0f);
                this.Crouching = new MovementParameters(true, 1.5f, 10.0f);
                this.TimeToCrouch = 0.25f;
                this.TimeToUncrouch = 0.25f;
                this.StandingCameraHeight = 0.75f;
                this.CrouchedCameraHeight = 0.25f;
                this.StandingColliderHeight = 2.0f;
                this.CrouchedColliderHeight = 1.5f;
                this.CanJumpWhileCrouched = false;
                this.CanJumpWhileTransitioningCrouch = true;
                this.CanJump = true;
                this.JumpForce = 4.0f;
                this.CanAirStrafe = true;
                this.CanCrouchWhileFalling = true;
                this.MaxAirAcceleration = 1.0f;
                this.AirAcceleration = 10.0f;
            }
        }

        /// <summary>
        /// Settings to control how the player collides and interacts with other physics objects.
        /// </summary>
        [Serializable]
        public class PlayerPhysicsSettings
        {
            [Tooltip("Should we be able to push and interact with other physics objects?")]
            public bool CanPushPhysicsObjects;

            [Space]
            
            [Tooltip("By default, the player will only detect collisions in the direction it is going towards. Enabling this boolean will allow for the player to check for collisions in all directions.")]
            public bool UseAccurateCollisions;

            public PlayerPhysicsSettings()
            {
                this.CanPushPhysicsObjects = true;
                this.UseAccurateCollisions = true;
            }
        }
        
        /// <summary>
        /// Settings to change how the user can move the player through input.
        /// </summary>
        [Serializable]
        public class PlayerInputSettings
        {
            [Tooltip("The InputActionAsset that contains the mappings for our player movement.")]
            public InputActionAsset Actions;
            [Tooltip("Since there are multiple mappings per InputActionAsset, we need to provide the name of the one we want to use in this context.")]
            public string Mapping;
            
            [Space]
            
            [Tooltip("The name of the action that contains the input for which direction our player would travel, eg. WASD, arrow keys, left gamepad stick.")]
            public string DirectionalAction;
            [Tooltip("The name of the action that contains the input for how we will look up, down, left, and right with our camera, eg. mouse movement, right gamepad stick.")]
            public string LookAction;
            [Tooltip("The name of the action that contains the input for jumping, eg. spacebar, gamepad A button.")]
            public string JumpAction;
            [Tooltip("The name of the action that contains the input for crouching, eg. Left Control, gamepad B button.")]
            public string CrouchAction;
            [Tooltip("The name of the action that contains the input for running, eg. Left Shift, gamepad left joystick click.")]
            public string RunAction;
        }

        /// <summary>
        /// Fields that contain how a specific movement state should behave.
        /// </summary>
        [Serializable]
        public class MovementParameters
        {
            [Tooltip("Should we be able to perform this movement action?")]
            public bool Enabled;
            
            [Space]
            
            [Tooltip("The speed that we desire the player to travel.")]
            public float MaxSpeed;
            [Tooltip("How fast we should accelerate to the desired speed.")]
            public float Acceleration;

            public MovementParameters(bool enabled, float maxSpeed, float acceleration)
            {
                this.Enabled = enabled;
                this.MaxSpeed = maxSpeed;
                this.Acceleration = acceleration;
            }
        }
        
        /// <summary>
        /// Parameter used to check for when a state should transition to another state;
        /// defined in the inspector.
        /// </summary>
        [Serializable]
        public class StateParameter
        {
            /// <summary>
            /// The name of the parameter that we can set.
            /// </summary>
            public string Name;
            /// <summary>
            /// The value that the parameter has.
            /// </summary>
            public bool Value;
        }
        
        /// <summary>
        /// Fields for an individual state that contain its name and the states it can transition out of;
        /// defined in the inspector.
        /// </summary>
        [Serializable]
        public class State
        {
            /// <summary>
            /// The name of the state.
            /// </summary>
            public string Name;
            /// <summary>
            /// The list of transitions.
            /// </summary>
            public List<StateTransition> Transitions = new();
        }

        /// <summary>
        /// Fields that define how a state can transition to another state using conditions;
        /// defined in the inspector.
        /// </summary>
        [Serializable]
        public class StateTransition
        {
            /// <summary>
            /// The state we can transition to.
            /// Provides an index of the state's location.
            /// </summary>
            public int ToState;
            /// <summary>
            /// The conditions that have to be met to transition to the specified state.
            /// </summary>
            public List<StateCondition> Conditions = new();
        }

        /// <summary>
        /// Fields that describe what it would take for a state to transition to another state;
        /// defined in the inspector.
        /// </summary>
        [Serializable]
        public class StateCondition
        {
            public enum Values
            {
                True,
                False
            }

            /// <summary>
            /// The name of the parameter that we want to check.
            /// Provides an index of the parameter's location.
            /// </summary>
            public int Name;
            /// <summary>
            /// The value that our given parameter has to be set to.
            /// </summary>
            public Values Value;
        }

        #endregion
    }
}