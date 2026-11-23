using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// 调用系统分享：iOS 弹出 UIActivityViewController（Plugins/iOS/MajShare.mm），
    /// Android 弹出分享选择器（Plugins/Android/.../ShareKit.java）；其他平台没有分享菜单，打开文件所在的文件夹
    /// </summary>
    internal static class NativeShare
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void MajShare_Share(string paths, string text);
#endif
        public static void ShareFiles(IReadOnlyList<string> paths, string mimeType, string title)
        {
            if (paths.Count == 0)
            {
                return;
            }
#if UNITY_IOS && !UNITY_EDITOR
            MajShare_Share(string.Join("\n", paths), string.Empty);
#elif UNITY_ANDROID && !UNITY_EDITOR
            var array = new string[paths.Count];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = paths[i];
            }
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var shareKit = new AndroidJavaClass("net.majdata.majdataplay.ShareKit");
            shareKit.CallStatic("shareFiles", activity, array, mimeType, title);
#else
            OpenFolder(Path.GetDirectoryName(paths[0]) ?? string.Empty);
#endif
        }
        /// <param name="fallbackPath">没有分享菜单的平台把文字保存到这里</param>
        public static void ShareText(string text, string title, string fallbackPath)
        {
#if UNITY_IOS && !UNITY_EDITOR
            MajShare_Share(string.Empty, text);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var shareKit = new AndroidJavaClass("net.majdata.majdataplay.ShareKit");
            shareKit.CallStatic("shareText", activity, text, title);
#else
            GUIUtility.systemCopyBuffer = text;
            File.WriteAllText(fallbackPath, text);
            OpenFolder(Path.GetDirectoryName(fallbackPath) ?? string.Empty);
#endif
        }
        /// <summary>
        /// Opens a folder in the system file manager on desktops; does nothing on phones, which have no such view.
        /// </summary>
        /// <param name="directory">The local path of the folder.</param>
        internal static void OpenFolder(string directory)
        {
#if !((UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR)
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }
            Application.OpenURL("file:///" + directory.Replace('\\', '/').TrimStart('/'));
#endif
        }
    }
}
