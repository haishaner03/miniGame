using System.Collections;
using UnityEngine;

namespace Utilities
{
    /// <summary>
    /// Provides utility methods for handling animations in Unity.
    /// Includes helpers for animation state checks, clip information, and height calculations for movement effects.
    /// </summary>
    public static class AnimationHelper
    {
        /// <summary>
        /// Waits until the specified animation starts playing.
        /// </summary>
        /// <param name="animator">The Animator component to monitor.</param>
        /// <param name="animationName">The name of the animation to wait for.</param>
        /// <returns>An enumerator for use in coroutines.</returns>
        public static IEnumerator WaitForAnimationStart(Animator animator, string animationName)
        {
            // Continuously check if the animation state matches the specified name
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName(animationName))
            {
                yield return null; // Wait for the next frame
            }
        }

        /// <summary>
        /// Retrieves the duration of a specified animation clip.
        /// </summary>
        /// <param name="animator">The Animator component containing the animation clips.</param>
        /// <param name="clipName">The name of the animation clip to find.</param>
        /// <returns>The length of the animation clip in seconds. Defaults to 1 second if not found.</returns>
        public static float GetAnimationClipLength(Animator animator, string clipName)
        {
            // Iterate through all clips in the Animator's controller
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == clipName)
                {
                    return clip.length; // Return the length of the matching clip
                }
            }
            return 1f; // Default duration if clip is not found
        }

        /// <summary>
        /// Calculates the vertical height based on elapsed time, total time, and peak height.
        /// Useful for simulating jumps or dodges with a parabolic motion.
        /// </summary>
        /// <param name="elapsedTime">The time elapsed since the motion began.</param>
        /// <param name="totalTime">The total duration of the motion.</param>
        /// <param name="peakHeight">The maximum vertical height to reach during the motion.</param>
        /// <returns>The calculated height at the current time.</returns>
        public static float CalculateHeight(float elapsedTime, float totalTime, float peakHeight)
        {
            // First half of the motion: ascend towards the peak height
            if (elapsedTime <= totalTime / 2)
            {
                return Mathf.Lerp(0, peakHeight, elapsedTime / (totalTime / 2));
            }

            // Second half of the motion: descend back to ground level
            return Mathf.Lerp(peakHeight, 0, (elapsedTime - totalTime / 2) / (totalTime / 2));
        }
    }
}
