using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TransportTracker.App.Core.Diagnostics;

namespace TransportTracker.App.Core.UI
{
    /// <summary>
    /// Animation extensions for UI elements
    /// </summary>
    public static class AnimationExtensions
    {
        /// <summary>
        /// Animate the opacity of an element with a fade-in effect
        /// </summary>
        public static async Task FadeInAsync(this VisualElement element, uint duration = 250, Easing easing = null)
        {
            try
            {
                // Skip if element is null or already fully visible
                if (element == null || element.Opacity >= 1)
                    return;
                
                // Ensure element is visible before animating
                element.Opacity = 0;
                element.IsVisible = true;
                
                // Create and run the animation
                await element.FadeTo(1, duration, easing ?? Easing.CubicOut);
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("Animation_FadeIn", ex);
                
                // Fallback in case of failure - just make it visible
                if (element != null)
                {
                    element.Opacity = 1;
                    element.IsVisible = true;
                }
            }
        }
        
        /// <summary>
        /// Animate the opacity of an element with a fade-out effect
        /// </summary>
        public static async Task FadeOutAsync(this VisualElement element, uint duration = 250, Easing easing = null)
        {
            try
            {
                // Skip if element is null or already invisible
                if (element == null || element.Opacity <= 0)
                    return;
                
                // Create and run the animation
                await element.FadeTo(0, duration, easing ?? Easing.CubicIn);
                
                // Hide the element after fade out
                element.IsVisible = false;
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("Animation_FadeOut", ex);
                
                // Fallback in case of failure - just hide it
                if (element != null)
                {
                    element.Opacity = 0;
                    element.IsVisible = false;
                }
            }
        }
        
        /// <summary>
        /// Animate the scale of an element
        /// </summary>
        public static async Task ScaleTo(this VisualElement element, double scale, uint duration = 250, Easing easing = null)
        {
            try
            {
                // Skip if element is null
                if (element == null)
                    return;
                
                // Create and run the animation
                await element.ScaleTo(scale, duration, easing ?? Easing.SpringOut);
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("Animation_ScaleTo", ex);
                
                // Fallback in case of failure - just set scale directly
                if (element != null)
                {
                    element.Scale = scale;
                }
            }
        }
        
        /// <summary>
        /// Animate an element with a bounce effect (scale up and down)
        /// </summary>
        public static async Task BounceAsync(this VisualElement element, double scale = 1.2, uint duration = 500)
        {
            try
            {
                // Skip if element is null
                if (element == null)
                    return;
                
                // Create and run the sequence of animations
                await element.ScaleTo(scale, duration / 2, Easing.SpringOut);
                await element.ScaleTo(1, duration / 2, Easing.SpringIn);
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("Animation_Bounce", ex);
                
                // Fallback in case of failure - just reset scale
                if (element != null)
                {
                    element.Scale = 1;
                }
            }
        }
        
        /// <summary>
        /// Animate the translation of an element from one position to another
        /// </summary>
        public static async Task SlideInFromBottomAsync(this VisualElement element, uint duration = 500, Easing easing = null)
        {
            try
            {
                // Skip if element is null
                if (element == null)
                    return;
                
                // Setup initial state
                element.IsVisible = true;
                element.Opacity = 0;
                element.TranslationY = 50;
                
                // Fade in while sliding up
                await Task.WhenAll(
                    element.FadeTo(1, duration, easing ?? Easing.CubicOut),
                    element.TranslateTo(0, 0, duration, easing ?? Easing.CubicOut)
                );
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("Animation_SlideInFromBottom", ex);
                
                // Fallback in case of failure - just set final state
                if (element != null)
                {
                    element.IsVisible = true;
                    element.Opacity = 1;
                    element.TranslationY = 0;
                }
            }
        }
        
        /// <summary>
        /// Apply a pulsing animation that repeats indefinitely (for attention)
        /// </summary>
        public static void StartPulseAnimation(this VisualElement element, double minScale = 0.95, double maxScale = 1.05, uint duration = 1000)
        {
            try
            {
                // Skip if element is null
                if (element == null)
                    return;
                
                // Create the animation action that will be recursive
                Action<double> animate = null;
                animate = async (scale) =>
                {
                    try
                    {
                        if (element == null) return;
                        
                        await element.ScaleTo(scale, duration, Easing.SinInOut);
                        
                        // Toggle between min and max scale
                        double nextScale = Math.Abs(scale - minScale) < 0.01 ? maxScale : minScale;
                        animate(nextScale);
                    }
                    catch (Exception ex)
                    {
                        PerformanceMonitor.Instance.RecordFailure("Animation_Pulse", ex);
                    }
                };
                
                // Start the animation
                animate(maxScale);
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("Animation_StartPulse", ex);
                
                // Fallback - just reset scale
                if (element != null)
                {
                    element.Scale = 1;
                }
            }
        }
        
        /// <summary>
        /// Stop a pulsing animation by resetting the element's scale
        /// </summary>
        public static void StopPulseAnimation(this VisualElement element)
        {
            try
            {
                if (element != null)
                {
                    element.AbortAnimation("ScaleTo");
                    element.Scale = 1;
                }
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("Animation_StopPulse", ex);
            }
        }
        
        /// <summary>
        /// Animate a progress bar fill with a smooth effect
        /// </summary>
        public static async Task AnimateProgressAsync(this ProgressBar progressBar, double targetProgress, uint duration = 500)
        {
            try
            {
                if (progressBar == null)
                    return;
                    
                // Capture the starting progress
                double startProgress = progressBar.Progress;
                
                // Setup an animation with 60 frames per second
                uint frames = (uint)(duration / 16.667); // 16.667ms per frame at 60fps
                uint frameRate = 16;
                
                for (uint i = 0; i <= frames; i++)
                {
                    // Calculate the current progress using ease-in-out
                    double t = (double)i / frames;
                    double easedT = t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
                    
                    // Apply the progress
                    progressBar.Progress = startProgress + (targetProgress - startProgress) * easedT;
                    
                    // Wait for next frame
                    await Task.Delay((int)frameRate);
                }
                
                // Ensure we end at exactly the target value
                progressBar.Progress = targetProgress;
            }
            catch (Exception ex)
            {
                PerformanceMonitor.Instance.RecordFailure("Animation_Progress", ex);
                
                // Fallback - just set to target
                if (progressBar != null)
                {
                    progressBar.Progress = targetProgress;
                }
            }
        }
    }
}
