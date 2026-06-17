using System.Collections;
using UnityEngine;
using Utilities;

namespace Characters.Base
{
    /// <summary>
    /// Base class for character movement, providing fundamental movement logic such as walking, running, jumping, and dodging.
    /// Designed to be inherited and customized for specific character behaviors.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
    public abstract class CharacterMovementBase : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField, Tooltip("Speed at which the character walks.")]
        protected float walkSpeed = 0.8f;
        
        [SerializeField, Tooltip("Speed at which the character runs.")]
        protected float runSpeed = 1.5f;

        [Header("Dodge Settings")]
        [SerializeField, Tooltip("Distance the character travels during a dodge.")]
        protected float dodgeDistance = 2f;
        
        [SerializeField, Tooltip("Vertical height achieved during a dodge.")]
        protected float dodgeHeight = 0.1f;

        [Header("Jump Settings")]
        [SerializeField, Tooltip("Force applied when the character jumps.")]
        protected float jumpForce = 1f;
        
        [SerializeField, Tooltip("Total time the character spends in the air during a jump.")]
        protected float jumpTotalTime = 0.3f;

        // Movement input from the player
        protected Vector2 moveInput;

        // References to essential components
        protected Rigidbody2D rb;
        protected Animator animator;

        // State flags
        protected bool isRunning = false;
        protected bool isDodging = false;
        protected bool isGrounded = true;

        /// <summary>
        /// Initializes component references.
        /// </summary>
        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Updates character movement and animation states on a fixed time interval.
        /// </summary>
        protected virtual void FixedUpdate()
        {
            // Prevent normal movement during a dodge
            if (isDodging)
            {
                UpdateAnimator();
                return;
            }

            // Stop horizontal movement while in the air
            if (!isGrounded)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            // Handle regular movement
            MoveCharacter();
            UpdateAnimator();
        }

        /// <summary>
        /// Handles character movement based on input and current speed settings.
        /// </summary>
        protected void MoveCharacter()
        {
            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            rb.linearVelocity = moveInput * currentSpeed;
        }

        /// <summary>
        /// Updates animator parameters based on movement states.
        /// </summary>
        protected void UpdateAnimator()
        {
            animator.SetFloat("Speed", rb.linearVelocity.magnitude);
        }

        /// <summary>
        /// Flips the character's direction based on horizontal input.
        /// </summary>
        /// <param name="direction">The horizontal input direction.</param>
        protected void FlipCharacterDirection(float direction)
        {
            transform.localScale = new Vector3(Mathf.Sign(direction), 1, 1);
        }

        /// <summary>
        /// Executes a dodge action, temporarily moving the character backward and upward.
        /// </summary>
        protected IEnumerator Dodge()
        {
            if (isDodging) yield break;

            isDodging = true;
            animator.SetBool("isDodging", true);

            // Reset velocity and calculate dodge direction
            rb.linearVelocity = Vector2.zero;
            Vector2 dodgeDirection = new Vector2(-Mathf.Sign(transform.localScale.x), 0).normalized;

            yield return null; // Wait for Animator to process state

            // Wait for animation start and calculate dodge duration
            yield return AnimationHelper.WaitForAnimationStart(animator, "Unarmed_Dodge");
            float dodgeTime = AnimationHelper.GetAnimationClipLength(animator, "Unarmed_Dodge");

            float elapsedTime = 0f;
            float originalY = transform.position.y;

            rb.linearVelocity = dodgeDirection * (dodgeDistance / dodgeTime);

            while (elapsedTime < dodgeTime)
            {
                elapsedTime += Time.deltaTime;

                // Calculate height for upward and downward arcs
                float height = AnimationHelper.CalculateHeight(elapsedTime, dodgeTime, dodgeHeight);
                transform.position = new Vector3(
                    transform.position.x + rb.linearVelocity.x * Time.deltaTime,
                    originalY + height,
                    transform.position.z
                );

                yield return null;
            }

            EndDodge(originalY);
        }

        /// <summary>
        /// Resets dodge state and ensures proper positioning after a dodge.
        /// </summary>
        protected void EndDodge(float originalY)
        {
            rb.linearVelocity = Vector2.zero;

            // Correct any vertical displacement
            if (Mathf.Abs(transform.position.y - originalY) > 0.01f)
            {
                transform.position = new Vector3(transform.position.x, originalY, transform.position.z);
            }

            animator.SetBool("isDodging", false);
            isDodging = false;
        }

        /// <summary>
        /// Executes a jump, applying vertical force and optional horizontal movement.
        /// </summary>
        protected IEnumerator Jump()
        {
            isGrounded = false;
            animator.SetTrigger("Jump");

            float originalY = transform.position.y;
            float elapsedTime = 0f;

            // Determine jump direction
            Vector2 jumpDirection = moveInput == Vector2.zero ? Vector2.zero : moveInput.normalized;
            float currentSpeed = isRunning ? runSpeed : walkSpeed;

            while (elapsedTime < jumpTotalTime)
            {
                elapsedTime += Time.deltaTime;

                float height = AnimationHelper.CalculateHeight(elapsedTime, jumpTotalTime, jumpForce);

                // Update position for both vertical and horizontal movement
                transform.position = new Vector3(
                    transform.position.x + jumpDirection.x * currentSpeed * Time.deltaTime,
                    originalY + height,
                    transform.position.z
                );

                yield return null;
            }

            EndJump(originalY);
        }

        /// <summary>
        /// Finalizes jump by resetting vertical position and state.
        /// </summary>
        protected void EndJump(float originalY)
        {
            if (Mathf.Abs(transform.position.y - originalY) > 0.01f)
            {
                transform.position = new Vector3(transform.position.x, originalY, transform.position.z);
            }

            rb.linearVelocity = Vector2.zero;
            isGrounded = true;
        }
    }
}
