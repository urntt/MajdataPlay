using MajdataPlay.Net;
using UnityEngine;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// Describes the player shown by the touch interface and in shared score headers.
    /// </summary>
    internal readonly struct PlayerProfile
    {
        /// <summary>
        /// The name shown when no account is logged in.
        /// </summary>
        public const string GUEST_NAME = "Guest";

        /// <summary>
        /// Gets the nickname of the player, or the username when the nickname is empty, or <see cref="GUEST_NAME"/>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the avatar of the logged-in account, or <see langword="null"/> when it is not loaded or no account is logged in.
        /// </summary>
        public Sprite? Avatar { get; }

        /// <summary>
        /// Gets a value indicating whether an account is logged in.
        /// </summary>
        public bool IsLoggedIn { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerProfile"/> struct.
        /// </summary>
        /// <param name="name">The name to show.</param>
        /// <param name="avatar">The avatar to show, or <see langword="null"/> for none.</param>
        /// <param name="isLoggedIn">Whether an account is logged in.</param>
        public PlayerProfile(string name, Sprite? avatar, bool isLoggedIn)
        {
            Name = name;
            Avatar = avatar;
            IsLoggedIn = isLoggedIn;
        }

        /// <summary>
        /// Gets the profile of the first logged-in account on a server that requires authentication.
        /// </summary>
        /// <returns>The logged-in player, or a guest profile when no account is logged in.</returns>
        public static PlayerProfile GetCurrent()
        {
            var endpoints = MajEnv.ApiEndpoints;
            if (endpoints is null)
            {
                return new PlayerProfile(GUEST_NAME, null, false);
            }
            foreach (var endpoint in endpoints)
            {
                var runtimeConfig = endpoint.RuntimeConfig;
                if (runtimeConfig.AuthMethod == NetAuthMethodOption.None || !runtimeConfig.IsLoggedIn)
                {
                    continue;
                }
                var name = string.IsNullOrEmpty(runtimeConfig.Nickname) ? runtimeConfig.Username : runtimeConfig.Nickname;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = GUEST_NAME;
                }
                return new PlayerProfile(name, runtimeConfig.Avatar, true);
            }
            return new PlayerProfile(GUEST_NAME, null, false);
        }
    }
}
