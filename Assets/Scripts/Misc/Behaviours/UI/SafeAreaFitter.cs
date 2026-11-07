#nullable enable
using UnityEngine;

namespace MajdataPlay
{
    /// <summary>
    /// Fits a full-screen rectangle into <see cref="Screen.safeArea"/>, so content stays clear of the notch, rounded corners and
    /// the home indicator while backgrounds placed outside it still fill the whole screen.
    /// </summary>
    /// <remarks>
    /// Refreshes whenever the safe area, the screen size or the parent size changes, which also covers a landscape device being
    /// turned around and its notch moving to the other side. A fitter made by <see cref="CreateEnlarged"/> also scales its content
    /// up on phones by <see cref="UiScaling.GetEnlargement"/>, for canvases whose own scale stays as it is.
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
        /// The parent size the offsets were last computed for.
        /// </summary>
        Vector2 _lastParentSize;

        /// <summary>
        /// Whether the content is scaled up on phones by <see cref="UiScaling.GetEnlargement"/>.
        /// </summary>
        bool _isEnlargedOnPhones;

        /// <summary>
        /// Creates a child that stretches over the safe area of the screen.
        /// </summary>
        /// <param name="canvasRoot">The full-screen root of a canvas.</param>
        /// <returns>The safe-area child; put the content inside it.</returns>
        public static RectTransform Create(RectTransform canvasRoot)
        {
            return Create(canvasRoot, false);
        }

        /// <summary>
        /// Creates a child that covers the safe area of the screen and scales its content up on phones, so children anchored to its
        /// edges stay at the edges of the safe area while their sizes grow by <see cref="UiScaling.GetEnlargement"/>.
        /// </summary>
        /// <param name="canvasRoot">The full-screen root of a canvas.</param>
        /// <returns>The safe-area child; put the content inside it.</returns>
        public static RectTransform CreateEnlarged(RectTransform canvasRoot)
        {
            return Create(canvasRoot, true);
        }

        /// <summary>
        /// Converts the size of the safe area to the offsets that keep a centered rectangle scaled by <paramref name="scale"/>
        /// exactly over it: the rectangle is made 1 / <paramref name="scale"/> as large, then scaled.
        /// </summary>
        /// <param name="safeSize">The size of the safe area in the parent's units.</param>
        /// <param name="scale">The scale of the content; 1 or more.</param>
        /// <param name="offsetMin">The lower left offset.</param>
        /// <param name="offsetMax">The upper right offset.</param>
        public static void GetScaledOffsets(Vector2 safeSize, float scale, out Vector2 offsetMin, out Vector2 offsetMax)
        {
            if (scale <= 0f)
            {
                offsetMin = Vector2.zero;
                offsetMax = Vector2.zero;
                return;
            }
            var sizeDelta = safeSize * (1f / scale - 1f);
            offsetMin = -sizeDelta * 0.5f;
            offsetMax = sizeDelta * 0.5f;
        }

        /// <summary>
        /// Creates the safe-area child.
        /// </summary>
        /// <param name="canvasRoot">The full-screen root of a canvas.</param>
        /// <param name="isEnlargedOnPhones">Whether the content is scaled up on phones.</param>
        /// <returns>The safe-area child.</returns>
        static RectTransform Create(RectTransform canvasRoot, bool isEnlargedOnPhones)
        {
            var safeObject = new GameObject("SafeArea", typeof(RectTransform));
            safeObject.layer = canvasRoot.gameObject.layer;
            var safe = (RectTransform)safeObject.transform;
            safe.SetParent(canvasRoot, false);
            safe.pivot = new Vector2(0.5f, 0.5f);
            safe.offsetMin = Vector2.zero;
            safe.offsetMax = Vector2.zero;
            var fitter = safeObject.AddComponent<SafeAreaFitter>();
            fitter._isEnlargedOnPhones = isEnlargedOnPhones;
            fitter.Apply();
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
            if (Screen.safeArea != _lastSafeArea || Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y ||
                (_isEnlargedOnPhones && GetParentSize() != _lastParentSize))
            {
                Apply();
            }
        }

        /// <summary>
        /// Gets the size of the parent rectangle.
        /// </summary>
        /// <returns>The parent size, or zero without a parent rectangle.</returns>
        Vector2 GetParentSize()
        {
            return transform.parent is RectTransform parent ? parent.rect.size : Vector2.zero;
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
            if (!_isEnlargedOnPhones)
            {
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                return;
            }
            _lastParentSize = GetParentSize();
            var scale = UiScaling.GetEnlargement(Screen.width, Screen.height, Screen.dpi);
            var safeSize = Vector2.Scale(_lastParentSize, anchorMax - anchorMin);
            GetScaledOffsets(safeSize, scale, out var offsetMin, out var offsetMax);
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
