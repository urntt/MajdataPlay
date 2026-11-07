#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace MajdataPlay
{
    /// <summary>
    /// Scales a code-built canvas with <see cref="UiScaling.GetScaleFactor"/>: the 1920×1080 reference layout on tablets and
    /// computers, larger on phones. Refreshes when the screen size or orientation changes.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    internal sealed class AdaptiveCanvasScaler : MonoBehaviour
    {
        /// <summary>
        /// The scaler this component drives; switched to a constant pixel size whose factor is set here.
        /// </summary>
        CanvasScaler? _scaler;

        /// <summary>
        /// The screen size the factor was last computed for.
        /// </summary>
        Vector2Int _lastScreenSize;

        /// <summary>
        /// Adds the component to a canvas that already has a <see cref="CanvasScaler"/> and applies the scale at once.
        /// </summary>
        /// <param name="scaler">The scaler of the canvas.</param>
        /// <returns>The added component.</returns>
        public static AdaptiveCanvasScaler Attach(CanvasScaler scaler)
        {
            var adaptive = scaler.gameObject.AddComponent<AdaptiveCanvasScaler>();
            adaptive._scaler = scaler;
            adaptive.Apply();
            return adaptive;
        }

        private void Update()
        {
            if (Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y)
            {
                Apply();
            }
        }

        /// <summary>
        /// Computes the scale factor for the current screen and applies it to the scaler.
        /// </summary>
        void Apply()
        {
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (_scaler == null)
            {
                return;
            }
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.scaleFactor = UiScaling.GetScaleFactor(Screen.width, Screen.height, Screen.dpi);
        }
    }
}
