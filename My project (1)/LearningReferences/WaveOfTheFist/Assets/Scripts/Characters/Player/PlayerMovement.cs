using UnityEngine;
using UnityEngine.InputSystem;
using Characters.Base;

namespace Characters.Player
{
    /// <summary>
    /// Handles player-specific movement and input interactions.
    /// Extends the base character movement logic from <see cref="CharacterMovementBase"/>.
    /// </summary>
    public class PlayerMovement : CharacterMovementBase
    {
        private PlayerInput playerInput;

        /// <summary>
        /// Initializes the player input system and subscribes to input events.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            playerInput = new PlayerInput();

            // Subscribe to input events for movement, running, dodging, and jumping
            playerInput.Player.Move.performed += OnMove;
            playerInput.Player.Move.canceled += OnMove;
            playerInput.Player.Run.performed += OnRun;
            playerInput.Player.Run.canceled += OnRun;
            playerInput.Player.Dodge.performed += OnDodge;
            playerInput.Player.Jump.performed += OnJump;
        }

        /// <summary>
        /// Enables the input system when the object is enabled.
        /// </summary>
        private void OnEnable()
        {
            playerInput.Enable();
        }

        /// <summary>
        /// Disables the input system when the object is disabled.
        /// </summary>
        private void OnDisable()
        {
            playerInput.Disable();
        }

        /// <summary>
        /// Handles movement input and updates the character's velocity and direction.
        /// </summary>
        /// <param name="context">The input context providing movement data.</param>
        public void OnMove(InputAction.CallbackContext context)
        {
            // Read the movement input from the player
            moveInput = context.ReadValue<Vector2>();

            if (moveInput == Vector2.zero)
            {
                // Stop movement if there is no input
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                // Flip the character's direction based on horizontal input
                FlipCharacterDirection(moveInput.x);
            }
        }

        /// <summary>
        /// Toggles the running state based on input.
        /// </summary>
        /// <param name="context">The input context providing run state data.</param>
        public void OnRun(InputAction.CallbackContext context)
        {
            // Determine if the run button is pressed
            isRunning = context.ReadValueAsButton();
        }

        /// <summary>
        /// Initiates a dodge action if conditions are met.
        /// </summary>
        /// <param name="context">The input context indicating a dodge action.</param>
        public void OnDodge(InputAction.CallbackContext context)
        {
            if (context.performed && !isDodging && isGrounded)
            {
                // Start the dodge coroutine
                StartCoroutine(Dodge());
            }
        }

        /// <summary>
        /// Initiates a jump action if conditions are met.
        /// </summary>
        /// <param name="context">The input context indicating a jump action.</param>
        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed && isGrounded && !isDodging)
            {
                // Start the jump coroutine
                StartCoroutine(Jump());
            }
        }
    }
}
