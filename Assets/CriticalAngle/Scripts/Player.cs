using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CriticalAngle.ExpandablePlayer
{
    public partial class Player : MonoBehaviour
    {
        #region Inspector Fields
        
        [SerializeField] protected PlayerReferences References;
        [SerializeField] protected PlayerGeneralSettings GeneralSettings;
        [SerializeField] protected PlayerMovementSettings MovementSettings;
        [SerializeField] protected PlayerPhysicsSettings PhysicsSettings;

        #endregion

        #region Public Fields

        public bool IsGrounded { get; private set; }

        [HideInInspector] public Vector3 Velocity;
        [HideInInspector] public bool DisableGravity = false;

        [HideInInspector] public List<StateParameter> Parameters = new();
        [HideInInspector] public List<State> States = new();

        [HideInInspector] public InputActionAsset InputActions;
        [HideInInspector] public int InputMapping;
        [HideInInspector] public int MoveBinding;
        [HideInInspector] public int RunBinding;
        [HideInInspector] public int LookBinding;
        [HideInInspector] public int JumpBinding;
        [HideInInspector] public int CrouchBinding;

        #endregion

        #region Private Fields
        
        protected Vector2 moveInput;
        protected Vector2 lookInput;
        protected bool jumpInput;
        protected bool crouchInput;
        protected bool runInput;
        
        protected readonly List<PlayerState> playerStates = new();
        protected readonly Dictionary<string, int> cachedStateParameters = new();
        protected int activeState;
        
        protected float xRotation;
        
        protected decimal snapAmount;
        protected Vector3 groundNormal;
        protected bool canHitCeiling;
        
        protected Vector3 trueVelocity;
        protected Vector3 previousPosition;

        #endregion

        #region Custom Inspector Functions

#if UNITY_EDITOR

        /// <summary>
        /// Clicking the corresponding button in the Inspector calls this function.
        /// Adds the CharacterController and Camera components if they cannot be found.
        /// </summary>
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

        /// <summary>
        /// Makes sure that the CharacterController and some other variables aren't incorrect.
        /// The CharacterController should not be tampered with by the user, since this script has all of
        /// the necessary variables to control it.
        /// </summary>
        public void ApplyVariables()
        {
            this.References.Camera.transform.localPosition = new Vector3(0.0f,
                this.MovementSettings.StandingCameraHeight, 0.0f);

            if (this.References.CharacterController != null)
            {
                this.References.CharacterController.height = this.MovementSettings.StandingColliderHeight;
                this.References.CharacterController.radius = this.GeneralSettings.Radius;
                this.References.CharacterController.slopeLimit = 89.99f;
                this.References.CharacterController.minMoveDistance = this.GeneralSettings.StopSpeed;
                this.References.CharacterController.stepOffset = this.GeneralSettings.StepOffset;
                this.References.CharacterController.skinWidth = 0.001f;

                if (this.GeneralSettings.StepOffset > this.References.CharacterController.height)
                    this.GeneralSettings.StepOffset = this.References.CharacterController.height;
            }

            if (this.GeneralSettings.MinLookAngle > this.GeneralSettings.MaxLookAngle)
                this.GeneralSettings.MaxLookAngle = this.GeneralSettings.MinLookAngle;
        }

        /// <summary>
        /// You can probably guess what this one does.
        /// </summary>
        public void HideComponents()
        {
            if (this.References.CharacterController != null)
                this.References.CharacterController.hideFlags = HideFlags.HideInInspector;
        }

#endif

        #endregion

        #region Default Functions

        /*
         * All of the main default functions are created and made virtual (even if they're not used),
         * just to make things more consistent.
         */
        
        protected virtual void OnEnable()
        {
        }

        protected virtual void Awake()
        {
            this.ValidateComponents();
            this.InitializeInputs();
            this.InitializeStates();
            this.InitializeStateParameters();
            DisableCursor();
        }

        protected virtual void Start()
        {
        }

        protected virtual void Update()
        {
            // Our true velocity will allow us to tell how much we actually move,
            // since `this.Velocity` is our *desired* velocity, not how much we actually displace each frame.
            var position = this.transform.position;
            this.trueVelocity = position - this.previousPosition;
            this.previousPosition = position;
            
            this.CeilingCheck();
            this.GroundCheck();
            if (this.IsGrounded)
            {
                this.CalculateFriction();
                this.canHitCeiling = true;
            }

            this.SnapToGround();
            this.ApplyGravity();
            this.UpdateStateParameters();

            this.playerStates[this.activeState].Update();

            /*Vector3[] continuousCollisionSweepVectors =
            {
                Vector3.left,
                Vector3.right,
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back,
            };
            
            const int passes = 8;
            const float strength = 0.01f;

            for (int i = 0; i < passes; i++)
            {
                foreach (var vector in continuousCollisionSweepVectors)
                {
                    Vector3 oldPos = this.transform.position;
                    this.References.CharacterController.Move(vector * strength);
                    // ReSharper disable once Unity.InefficientPropertyAccess
                    Vector3 delta = this.transform.position - oldPos;
                    this.References.CharacterController.Move(-delta);
                }
            }*/
            
            // Do the final character movement
            var moveAmount = ((float)this.snapAmount).V3Y() + this.Velocity * Time.deltaTime;
            this.References.CharacterController.Move(moveAmount);
        }

        protected virtual void LateUpdate()
        {
            this.playerStates[this.activeState].LateUpdate();

            this.HandleRotation();
        }

        protected virtual void FixedUpdate()
        {
            this.playerStates[this.activeState].FixedUpdate();
        }

        protected virtual void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var flags = this.References.CharacterController.collisionFlags;
            var above = (flags & CollisionFlags.Above) != 0;
            var sides = (flags & CollisionFlags.Sides) != 0;
            
            // In other words, if we are not colliding from above but we are colliding from the sides,
            // add an equal and opposite force to the player to align better with real life.
            if ((sides && !above) || (this.References.CharacterController.isGrounded && !this.IsGrounded))
                this.Velocity -= hit.normal * Vector3.Dot(this.Velocity, hit.normal);
        }

        protected virtual void OnDestroy()
        {
            this.playerStates[this.activeState].OnDestroy();

            this.InputActions.Disable();
        }

        #endregion

        #region Initialization
        
        private void ValidateComponents()
        {
            if (this.References.Camera == null)
                this.Warning("The Camera component is missing! Press the \"Setup References\" button to fix it.");

            if (this.References.CharacterController == null)
                this.Warning(
                    "The CharacterController component is missing! Press the \"Setup References\" button to fix it.");

            if (this.InputActions == null)
                this.Warning(
                    "The Actions parameter is missing! Set up an InputActionAsset and drag it in or use the provided one to fix it.");
        }

        private void InitializeInputs()
        {
            var actions = this.InputActions;
            actions.Enable();

            var mapping = this.InputActions.actionMaps[this.InputMapping];

            mapping.actions[this.MoveBinding].performed += this.OnMovementInput;
            mapping.actions[this.MoveBinding].canceled += this.OnMovementInput;

            mapping.actions[LookBinding].performed += this.OnLookInput;
            mapping.actions[LookBinding].canceled += this.OnLookInput;

            mapping.actions[JumpBinding].performed += this.OnJumpInput;
            mapping.actions[JumpBinding].canceled += this.OnJumpInput;

            mapping.actions[CrouchBinding].performed += this.OnCrouchInput;
            mapping.actions[CrouchBinding].canceled += this.OnCrouchInput;

            mapping.actions[RunBinding].performed += this.OnRunInput;
            mapping.actions[RunBinding].canceled += this.OnRunInput;
        }

        /// <summary>
        /// Gets all of the classes in our assembler that derive from `PlayerState` and creates an instance
        /// of each. This allows us to simply create states and not have to worry about any extra setup.
        /// </summary>
        private void InitializeStates()
        {
            var tempPlayerStates = GetAllOfType<PlayerState>(this);
            var tempPlayerStateNames = tempPlayerStates.Select(state => state.GetStateName()).ToArray();

            // Match each player state in `tempPlayerStates` to the names in `this.States.`
            // Not doing this will cause bugs regarding the order of states not matching the
            // states in the inspector.
            var playerStateNames = this.States.Select(state => state.Name).ToArray();
            foreach (var n in playerStateNames)
            {
                var index = Array.FindIndex(tempPlayerStateNames, s => s == n);

                if (index >= 0)
                    this.playerStates.Add(tempPlayerStates[index]);
                else
                    this.Error("Cannot find a PlayerState class that returns the State name, `" + n +
                               ".` Make sure that you have a class definition that returns the correct name in its `GetStateName()` function.");
            }
        }

        private void InitializeStateParameters()
        {
            var i = 0;
            foreach (var parameter in this.Parameters)
            {
                this.cachedStateParameters.Add(parameter.Name, i);
                i++;
            }
        }

        /// <summary>
        /// An extremely helpful function to access all of the classes in the assembler that derive from
        /// a single class. This will be used to get all of the `PlayerState` classes in our assembly.
        /// </summary>
        /// <param name="args">Any arguments that the class might take. This is ambiguous so it could throw an error.</param>
        /// <typeparam name="T">The type of class we want to retrieve.</typeparam>
        /// <returns></returns>
        private static T[] GetAllOfType<T>(params object[] args)
        {
            var results = new List<T>();

            foreach (Type type in Assembly.GetAssembly(typeof(T)).GetTypes().Where(x =>
                         x.IsClass && !x.IsAbstract &&
                         x.IsSubclassOf(typeof(T))))
                results.Add((T)Activator.CreateInstance(type, args));

            return results.ToArray();
        }

        /// <summary>
        /// The most complicated function in this entire script.
        /// </summary>
        protected static void EnableCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// The second most complicated function in this entire script.
        /// </summary>
        protected static void DisableCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        #endregion

        #region State Management

        /// <summary>
        /// Simply update our parameters list to check if we should switch states.
        /// </summary>
        /// <param name="parameterName">Name of parameter.</param>
        /// <param name="value">New value of parameter.</param>
        protected void UpdateStateParameter(string parameterName, bool value)
        {
            var parameter = this.Parameters[this.cachedStateParameters[parameterName]];
            parameter.Value = value;
        }

        /// <summary>
        /// Returns the value of the given parameter name for general use.
        /// </summary>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <returns>Value of the parameter.</returns>
        protected bool GetStateParameter(string parameterName)
        {
            return this.Parameters[this.cachedStateParameters[parameterName]].Value;
        }

        /// <summary>
        /// This function is the meat and potatoes of our state machine.
        /// Based on the conditions that we specify in the inspector,
        /// we check at each frame if our parameters match the conditions.
        /// If so, switch the state to the specified one in the inspector.
        /// </summary>
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

        /// <summary>
        /// A central hub to update some of the default parameters.
        /// </summary>
        private void UpdateStateParameters()
        {
            this.UpdateStateParameter("Is Grounded", this.IsGrounded);
            this.UpdateStateParameter("Is Moving", this.moveInput.sqrMagnitude > 0.0f);
            this.UpdateStateParameter("Run Input", this.runInput);
            this.UpdateStateParameter("Crouch Input", this.crouchInput);
            this.UpdateStateParameter("Jump Input", this.jumpInput);
            this.UpdateStateParameter("Can Jump", this.IsGrounded && this.Velocity.y <= 0.0f);
            this.CheckStateConditions();
        }
        
        /// <summary>
        /// Actually transition from one state to another.
        /// This is NOT meant to be called by the user and the
        /// only function that *should* call it is `this.CheckStateConditions.`
        /// </summary>
        private void SetState(int stateIndex)
        {
            var previousState = this.activeState;
            this.playerStates[previousState].OnStateExit();

            this.activeState = stateIndex;
            this.playerStates[previousState].OnPostStateExit();
            this.playerStates[this.activeState].OnStateEnter();
        }

        /// <summary>
        /// The base class for any state that we should transition to.
        /// </summary>
        protected abstract class PlayerState
        {
            /// <summary>
            /// A reference to our player.
            /// Since the states are contained inside the `Player` class,
            /// we can call any private/protected function we'd like.
            /// </summary>
            protected readonly Player Player;
            
            protected PlayerState(Player player) =>
                this.Player = player;

            /// <summary>
            /// This allows for us to couple the state created in the inspector
            /// with the `PlayerState` class that we create in code.
            /// </summary>
            /// <returns></returns>
            public abstract string GetStateName();

            /// <summary>
            /// Called when we switch to this state.
            /// </summary>
            public virtual void OnStateEnter()
            {
            }

            public virtual void Update()
            {
            }

            public virtual void FixedUpdate()
            {
            }

            public virtual void LateUpdate()
            {
            }

            public virtual void OnDestroy()
            {
            }

            /// <summary>
            /// Called when we switch to another state when this state is active.
            /// </summary>
            public virtual void OnStateExit()
            {
            }

            /// <summary>
            /// This function is called on the PREVIOUS state AFTER `OnStateEnter` is called on the NEW state.
            /// </summary>
            public virtual void OnPostStateExit()
            {
            }
        }

        #endregion

        #region Character

        /// <summary>
        /// Maps an input, which is a Vector2 because of the New Input System,
        /// to a Vector3 that's on a flat plane.
        /// </summary>
        protected virtual Vector3 InputVectorToDirection(Vector2 input)
        {
            return new Vector3(input.x, 0.0f, input.y);
        }

        /// <summary>
        /// Takes an input and modifies it to face the direction that the player is facing.
        /// For example, if we hold W and turn to the right, our character will
        /// travel straight but in that rightward direction.
        /// </summary>
        protected virtual Vector3 GlobalToLocalSpace(Vector3 input)
        {
            return this.transform.TransformDirection(input);
        }

        protected virtual void ApplyGravity()
        {
            if (!this.DisableGravity)
                this.Velocity.y += Physics.gravity.y * Time.deltaTime;
        }

        /// <summary>
        /// This is code stolen straight from the Source SDK 2013.
        /// You can find the code on Github here:
        /// https://github.com/ValveSoftware/source-sdk-2013/blob/0d8dceea4310fde5706b3ce1c70609d72a38efdf/sp/src/game/shared/gamemovement.cpp#L2256-L2339
        /// </summary>
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

        /// <summary>
        /// Takes our rotational input and rotates our camera's X axis and player's Y axis.
        /// </summary>
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

        protected virtual void GroundCheck()
        {
            var center = this.transform.position + this.References.CharacterController.center;
            
            // Based on our collider height and center, calculate how far our
            // `SphereCast` can travel to check to see if we're grounded.
            var maxDistance = this.References.CharacterController.height / 2.0f - this.GeneralSettings.Radius;

            // Calculate if we're grounded using said `SphereCast`.
            if (!Physics.SphereCast(center, this.GeneralSettings.Radius, Vector3.down, out var hit,
                    maxDistance + 0.05f))
            {
                this.IsGrounded = false;
                return;
            }

            // This will get our ground normal.
            // The reason we don't get it via `hit.normal` in the `SphereCast`
            // is that the Sphere's normal is also taken into account, which is undesirable.
            // So instead we cast a raycast straight down based off the *point* at which we hit
            // from the `SphereCast` to allow for the normal to be on a constant flat plane (Vector3.down).
            if (Physics.Raycast(hit.point + 0.01f.V3Y(), Vector3.down, out var groundHit))
                this.groundNormal = groundHit.normal;

            // This takes our ground normal and checks if we are standing on a slope that exceeds our slope limit.
            if (Vector3.Angle(this.groundNormal, Vector3.up) > this.GeneralSettings.SlopeLimit)
            {
                this.IsGrounded = false;
                return;
            }

            var otherLayer = hit.collider.gameObject.layer;
            // Do scary bitmask operations to detect if we're actually grounded or not using our `GroundMask`.
            this.IsGrounded = (1 << otherLayer & this.GeneralSettings.GroundMask) > 0;
        }

        /// <summary>
        /// This function essentially does the same exact thing as `this.GroundCheck` but checks
        /// if something is above our head. This fixes a bug by allowing us to hit a ceiling once
        /// then immediately lose velocity and fall back down.
        /// </summary>
        protected virtual void CeilingCheck()
        {
            if (!this.canHitCeiling) return;

            var center = this.transform.position + this.References.CharacterController.center;
            var maxDistance = this.References.CharacterController.height / 2.0f - this.GeneralSettings.Radius;

            if (Physics.SphereCast(center, this.GeneralSettings.Radius, Vector3.up, out var hit, maxDistance + 0.05f))
            {
                // Apply equal and opposite force to stop us from "staying in the ceiling."
                // This is a common workaround for the bug using CharacterControllers.
                this.Velocity -= hit.normal * Vector3.Dot(this.Velocity, hit.normal);
                this.canHitCeiling = false;
            }
        }

        /// <summary>
        /// This is a code block stolen directly from the Source SDK 2013.
        /// You can find the code block on Github here:
        /// https://github.com/ValveSoftware/source-sdk-2013/blob/0d8dceea4310fde5706b3ce1c70609d72a38efdf/sp/src/game/shared/gamemovement.cpp#L1707-L1748
        /// </summary>
        protected virtual void AirAccelerate(float speed, float accel)
        {
            var direction = this.GlobalToLocalSpace(this.InputVectorToDirection(this.moveInput.normalized));
            var magnitude = this.Velocity.magnitude;

            var wishSpeed = magnitude;

            if (wishSpeed > speed)
                wishSpeed = speed;

            var currentSpeed = Vector3.Dot(this.Velocity, direction);

            var addSpeed = wishSpeed - currentSpeed;
            if (addSpeed <= 0)
                return;

            var accelerationSpeed = accel * magnitude * Time.deltaTime;

            if (accelerationSpeed > addSpeed)
                accelerationSpeed = addSpeed;

            var addVelocity = accelerationSpeed * direction;
            if (this.IsGrounded)
                addVelocity = Vector3.ProjectOnPlane(addVelocity, this.groundNormal);

            this.Velocity += addVelocity;
        }

        protected virtual void NormalAirAccelerate(float speed, float accel)
        {
            var direction = this.GlobalToLocalSpace(this.InputVectorToDirection(this.moveInput.normalized));
            var targetVelocity = Vector3.ClampMagnitude(direction * accel, speed);
            this.Velocity += targetVelocity;
        }

        protected virtual void GroundAccelerate(float speed, float accel)
        {
            var direction = this.GlobalToLocalSpace(this.InputVectorToDirection(this.moveInput.normalized));

            var targetVelocity = direction * speed;
            var velocityChange = targetVelocity - this.Velocity;
            var acceleration = velocityChange / Time.deltaTime;

            acceleration = Vector3.ClampMagnitude(acceleration, accel);
            acceleration.y = 0.0f;

            this.Velocity += acceleration;
        }

        /// <summary>
        /// Allows to stay on the ground when running down slopes.
        /// </summary>
        protected virtual void SnapToGround()
        {
            // Makes sure not to snap to the ground if we are either
            // grounded or moving upwards.
            if (!this.IsGrounded || this.trueVelocity.y > 0.0f)
            {
                this.snapAmount = 0.0m;
                return;
            }

            var center = this.transform.position + this.References.CharacterController.center;

            // This block of code does a `SphereCast` downwards.
            // From there, we calculate where our player *should* be, and then we set our `snapAmount`
            // to make sure that we do snap to that point.
            if (Physics.SphereCast(center, this.GeneralSettings.FeetRadius, Vector3.down, out var hit,
                    this.References.CharacterController.height / 2.0f + this.GeneralSettings.StepOffset,
                    this.GeneralSettings.GroundMask))
            {
                var skin = (0.5f + this.References.CharacterController.skinWidth).V3Y();
                var calculatedCenter = hit.point + hit.normal * this.GeneralSettings.Radius + skin;

                this.snapAmount = (decimal)calculatedCenter.y - (decimal)this.transform.position.y;
                this.snapAmount = Math.Min(this.snapAmount, 0.0m);
            }
            else
                this.snapAmount = 0.0m;
        }

        #endregion

        #region Input Callbacks

        private void OnMovementInput(InputAction.CallbackContext ctx)
        {
            this.moveInput = ctx.ReadValue<Vector2>();
        }

        private void OnLookInput(InputAction.CallbackContext ctx)
        {
            this.lookInput = ctx.ReadValue<Vector2>();
        }

        private void OnJumpInput(InputAction.CallbackContext ctx)
        {
            this.jumpInput = (int)ctx.ReadValue<float>() == 1;
        }

        private void OnCrouchInput(InputAction.CallbackContext ctx)
        {
            this.crouchInput = (int)ctx.ReadValue<float>() == 1;
        }

        private void OnRunInput(InputAction.CallbackContext ctx)
        {
            this.runInput = (int)ctx.ReadValue<float>() == 1;
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
            [Header("Player")] [Tooltip("The radius of our capsule collider.")] [Min(0.0f)]
            public float Radius;

            [Tooltip(
                "The radius how far we're allowed to stick to the ground. Should be less than or equal to our collider's radius.")]
            [Min(0.0f)]
            public float FeetRadius;

            [Tooltip("The max slope angle (in degrees) that we can travel.")] [Range(1, 90)]
            public int SlopeLimit;

            [Tooltip("The maximum height of a surface that we can step up onto.")] [Min(0.0f)]
            public float StepOffset;

            [Tooltip("The amount of linear friction to be applied to the player while braking.")]
            public float Friction;

            [Tooltip("The speed at which the player should stop moving.")]
            public float StopSpeed;

            [Tooltip(
                "Select the layer(s) that the ground is on using this layer mask. This will allow the player to filter out collisions when checking for if we're grounded.")]
            public LayerMask GroundMask;

            [Space] [Header("Camera")] [Tooltip("How fast should the player be able to look around?")]
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
                this.FeetRadius = 0.35f;
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
            /// <summary>
            /// The type of movement we should perform while in air.
            /// </summary>
            public enum AirStrafeType
            {
                /// <summary>
                /// We cannot control our velocity in air.
                /// </summary>
                None,

                /// <summary>
                /// Allows the player to freely travel while in air.
                /// </summary>
                Normal,

                /// <summary>
                /// Uses the Source Engine movement to travel in air.
                /// </summary>
                Acceleration
            }

            [Header("Movement Parameters")] [Tooltip("Settings for when the player is in the Walk state.")]
            public MovementParameters Walking;

            [Tooltip("Settings for when the player is in the Run state.")]
            public MovementParameters Running;

            [Tooltip("Settings for when the player is in the Crouch state.")]
            public MovementParameters Crouching;

            [Space]
            [Header("Crouching")]
            [Tooltip(
                "How much time it take for the camera to move from its standing position to its crouched position and vice versa.")]
            public float TimeToCrouch;

            [Space] [Tooltip("Where should the camera be when we are standing?")]
            public float StandingCameraHeight;

            [Tooltip("Where should the camera be when we are crouched?")]
            public float CrouchedCameraHeight;

            [Tooltip("How tall should our collider be while standing?")]
            public float StandingColliderHeight;

            [Tooltip("How tall should our collider be while crouched?")]
            public float CrouchedColliderHeight;

            [Space] [Tooltip("Should we be able to jump in the air when we are fully crouched?")]
            public bool CanJumpWhileCrouched;

            [Tooltip("Should we be able to jump while transitioning form standing to crouched?")]
            public bool CanJumpWhileTransitioningCrouch;

            [Tooltip("Should we have to hold the crouch key to stay crouched or press it to toggle it?")]
            public bool ToggleCrouch;

            [Space] [Header("Jumping")] [Tooltip("Are we allowed to jump?")]
            public bool CanJump;

            [Tooltip("The upward force added when the jump key is pressed.")]
            public float JumpForce;

            [Space]
            [Header("Falling")]
            [Tooltip(
                "Are we allowed to strafe in air? This will override the `AirControl` property with air strafing code.")]
            public AirStrafeType AirStrafe;

            [Tooltip(
                "Should we be able to crouch in air? This will cause the the feet to be raised as opposed to the head being lowered.")]
            public bool CanCrouchWhileFalling;

            [Space] [Tooltip("What is the maximum speed we should be able to travel while air strafing?")]
            public float MaxAirAcceleration;

            [Tooltip("The multiplier for how fast we should travel while air strafing.")]
            public float AirAcceleration;

            public PlayerMovementSettings()
            {
                this.Walking = new MovementParameters(true, 4.0f, 10.0f);
                this.Running = new MovementParameters(false, 6.0f, 10.0f);
                this.Crouching = new MovementParameters(true, 1.5f, 10.0f);
                this.TimeToCrouch = 0.25f;
                this.StandingCameraHeight = 0.75f;
                this.CrouchedCameraHeight = 0.25f;
                this.StandingColliderHeight = 2.0f;
                this.CrouchedColliderHeight = 1.5f;
                this.CanJumpWhileCrouched = false;
                this.CanJumpWhileTransitioningCrouch = true;
                this.CanJump = true;
                this.JumpForce = 4.0f;
                this.AirStrafe = AirStrafeType.Acceleration;
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

            public PlayerPhysicsSettings()
            {
                this.CanPushPhysicsObjects = true;
            }
        }

        /// <summary>
        /// Fields that contain how a specific movement state should behave.
        /// </summary>
        [Serializable]
        public class MovementParameters
        {
            [Tooltip("Should we be able to perform this movement action?")]
            public bool Enabled;

            [Space] [Tooltip("The max speed that we desire the player to travel.")]
            public float MaxSpeed;

            [Tooltip("How fast we should accelerate to the desired speed.")] [Range(0.0f, 1.0f)]
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