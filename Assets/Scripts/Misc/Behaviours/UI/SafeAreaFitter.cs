#nullable enable
using UnityEngine;

namespace MajdataPlay
{
    /// <summary>
    /// Fits a full-screen rectangle into <see cref="Screen.safeArea"/>, so content stays clear of the notch, rounded corners and
    /// the home indicator while backgrounds placed outside it still fill the whole screen.
    /// </summary>
    /// <remarks>
    /// Refreshes whenever the safe area or the screen size changes, which also covers a landscape device being turned around
    /// and its notch moving to the other side.
    /// </remarks>
    internal sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>
        /// The safe area the anchors were last computed for.
        /// </summary>
        Rect _lastSafeArea;

        /// <summary>
        /// The screen size the anchors were last computed for.
        /// </summary>
        Vector2Int _lastScreenSize;

        /// <summary>
        /// Creates a child that stretches over the safe area of the screen.
        /// </summary>
        /// <param name="canvasRoot">The full-screen root of a canvas.</param>
        /// <returns>The safe-area child; put the content inside it.</returns>
        public static RectTransform Create(RectTransform canvasRoot)
        {
            var safeObject = new GameObject("SafeArea", typeof(RectTransform));
            safeObject.layer = canvasRoot.gameObject.layer;
            var safe = (RectTransform)safeObject.transform;
            safe.SetParent(canvasRoot, false);
            safe.pivot = new Vector2(0.5f, 0.5f);
            safe.offsetMin = Vector2.zero;
            safe.offsetMax = Vector2.zero;
            safeObject.AddComponent<SafeAreaFitter>().Apply();
            return safe;
        }

        /// <summary>
        /// Converts a safe area to normalized anchors of the screen.
        /// </summary>
        /// <param name="safeArea">The safe area in screen pixels.</param>
        /// <param name="screenWidth">The screen width in pixels.</param>
        /// <param name="screenHeight">The screen height in pixels.</param>
        /// <param name="anchorMin">The lower left anchor.</param>
        /// <param name="anchorMax">The upper right anchor.</param>
        public static void GetAnchors(Rect safeArea, float screenWidth, float screenHeight, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || safeArea.width <= 0f || safeArea.height <= 0f)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }
            anchorMin = new Vector2(Mathf.Clamp01(safeArea.xMin / screenWidth), Mathf.Clamp01(safeArea.yMin / screenHeight));
            anchorMax = new Vector2(Mathf.Clamp01(safeArea.xMax / screenWidth), Mathf.Clamp01(safeArea.yMax / screenHeight));
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea || Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y)
            {
                Apply();
            }
        }

        /// <summary>
        /// Moves the anchors to the current safe area.
        /// </summary>
        void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            GetAnchors(_lastSafeArea, Screen.width, Screen.height, out var anchorMin, out var anchorMax);
            var rect = (RectTransform)transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
