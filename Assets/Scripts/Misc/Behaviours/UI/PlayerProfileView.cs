using TMPro;
using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// Shows the nickname and round avatar of the current player in the top bar of the touch interface.
    /// </summary>
    /// <remarks>
    /// Created in code by <see cref="LightUi.CreatePlayerProfile"/>. The avatar is loaded asynchronously after login,
    /// so the view checks the profile periodically and only rebuilds its content when the name or avatar changed.
    /// </remarks>
    internal sealed class PlayerProfileView : MonoBehaviour
    {
        /// <summary>
        /// The interval between two profile checks, in seconds.
        /// </summary>
        private const float REFRESH_INTERVAL_SEC = 0.5f;

        /// <summary>
        /// The text showing the player name.
        /// </summary>
        private TextMeshProUGUI? _nameText;

        /// <summary>
        /// The image showing the avatar inside the round frame.
        /// </summary>
        private Image? _avatarImage;

        /// <summary>
        /// The silhouette shown while the player has no avatar.
        /// </summary>
        private Image? _placeholderIcon;

        /// <summary>
        /// The name currently shown, or <see langword="null"/> before the first refresh.
        /// </summary>
        private string? _shownName;

        /// <summary>
        /// The avatar currently shown, or <see langword="null"/> when the placeholder is shown.
        /// </summary>
        private Sprite? _shownAvatar;

        /// <summary>
        /// The time left until the next profile check, in seconds.
        /// </summary>
        private float _refreshRemaining;

        /// <summary>
        /// Connects the view to the parts created by <see cref="LightUi.CreatePlayerProfile"/> and shows the current profile.
        /// </summary>
        /// <param name="nameText">The text showing the player name.</param>
        /// <param name="avatarImage">The image showing the avatar.</param>
        /// <param name="placeholderIcon">The silhouette shown while there is no avatar.</param>
        public void Init(TextMeshProUGUI nameText, Image avatarImage, Image placeholderIcon)
        {
            _nameText = nameText;
            _avatarImage = avatarImage;
            _placeholderIcon = placeholderIcon;
            Refresh();
        }

        private void Update()
        {
            _refreshRemaining -= Time.unscaledDeltaTime;
            if (_refreshRemaining > 0f)
            {
                return;
            }
            Refresh();
        }

        /// <summary>
        /// Shows the current profile when its name or avatar differs from the one shown.
        /// </summary>
        private void Refresh()
        {
            _refreshRemaining = REFRESH_INTERVAL_SEC;
            if (_nameText == null || _avatarImage == null || _placeholderIcon == null)
            {
                return;
            }
            var profile = PlayerProfile.GetCurrent();
            if (profile.Name != _shownName)
            {
                _shownName = profile.Name;
                _nameText.text = profile.Name;
            }
            var avatar = profile.Avatar != null ? profile.Avatar : null;
            if (avatar == _shownAvatar && _avatarImage.enabled == (avatar != null))
            {
                return;
            }
            _shownAvatar = avatar;
            if (avatar != null)
            {
                LightUi.SetCover(_avatarImage, avatar);
                _avatarImage.enabled = true;
                _placeholderIcon.enabled = false;
            }
            else
            {
                _avatarImage.enabled = false;
                _placeholderIcon.enabled = true;
            }
        }
    }
}
