using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using MajdataPlay.IO;
using MajdataPlay.Threading;
using MajdataPlay.Utils;
using ManagedBass;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MajdataPlay.Scenes.List
{
#nullable enable
    public class PreviewSoundPlayer : MonoBehaviour
    {
        ISongDetail? _currentPreviewSong = null;
        bool _isPreviewPlaying = false;
        int _previewVersion = 0;

        public bool IsPreviewPending(ISongDetail info)
        {
            return ReferenceEquals(_currentPreviewSong, info);
        }

        public bool IsPreviewPlaying(ISongDetail info)
        {
            return ReferenceEquals(_currentPreviewSong, info) && _isPreviewPlaying;
        }

        /// <param name="isFromListBrowser">列表式选歌打开时只播放它请求的预览，封面轮播在后台切歌不出声</param>
        public void PlayPreviewSound(ISongDetail info, int loadDelayMS = 1000, CancellationToken token = default, bool isFromListBrowser = false)
        {
            if (SongListBrowser.IsOpen && !isFromListBrowser)
            {
                return;
            }
            _currentPreviewSong = info;
            _isPreviewPlaying = false;
            var previewVersion = ++_previewVersion;
            CabinetLed.SetButtonLight(Color.green, 3);
            CabinetLed.SetCabinetLight(1.0f);
            ListManager.AllBackgroundTasks.Add(PlayPreviewAsync(info, loadDelayMS, token, previewVersion)
                                               .Register($"[{nameof(PreviewSoundPlayer)}]Load song preview"));
        }
        async Task PlayPreviewAsync(ISongDetail info, int loadDelayMS, CancellationToken token, int previewVersion)
        {
            var selectSound = MajInstances.AudioManager.GetSFX("bgm_select.mp3");
            AudioSampleWrap? previewSample = null;
            // 选歌即时切换预览：切歌时背景音乐保持静音，直接接新歌的预览；只有加载失败时才恢复
            var isInstantPreview = MajEnv.Settings.Game.ListInstantPreview;
            try
            {
                if (!isInstantPreview)
                {
                    selectSound.SetVolume(MajEnv.Settings.Audio.Volume.BGM);
                }
                await UniTask.SwitchToThreadPool();
                await Task.Delay(loadDelayMS, cancellationToken: token);

                var simaiChart = await info.GetMaidataAsync(token: token);
                previewSample = await info.GetPreviewAudioTrackAsync(token: token);

                var previewOffsetSec = -1f;
                var previewLengthSec = -1f;
                if (previewSample is null || previewSample.IsEmpty)
                {
                    throw new InvalidAudioTrackException("Failed to decode audio track", string.Empty);
                }
                for (var i = 0; i < simaiChart.Commands.Count; i++)
                {
                    var command = simaiChart.Commands[i];
                    switch(command.Prefix)
                    {
                        case "demo_seek":
                            if (float.TryParse(command.Value, out var offsetSec))
                            {
                                if(previewOffsetSec != -1)
                                {
                                    MajDebug.LogWarning($"Multiple \"&demo_seek\" commands found. Previous value: {previewOffsetSec}, new value: {offsetSec}. Ignored.");
                                }
                                else if(offsetSec < 0)
                                {
                                    MajDebug.LogWarning($"Invalid \"&demo_seek\" value: {offsetSec}. Value must be non-negative. Ignored.");
                                }
                                else
                                {
                                    previewOffsetSec = offsetSec;
                                }
                            }
                            else
                            {
                                MajDebug.LogWarning($"Failed to parse \"&demo_seek\" value: {command.Value}");
                            }
                            break;
                        case "demo_len":
                            if (float.TryParse(command.Value, out var lenSec))
                            {
                                if (previewLengthSec != -1)
                                {
                                    MajDebug.LogWarning($"Multiple \"&demo_len\" commands found. Previous value: {previewOffsetSec}, new value: {lenSec}, ignored");
                                }
                                else if (lenSec <= 0)
                                {
                                    MajDebug.LogWarning($"Invalid \"&demo_len\" value: {lenSec}. Value must be positive. Ignored.");
                                }
                                else
                                {
                                    previewLengthSec = lenSec;
                                }
                            }
                            else
                            {
                                MajDebug.LogWarning($"Failed to parse \"&demo_len\" value: {command.Value}");
                            }
                            break;
                    }
                }
                if(previewOffsetSec == -1)
                {
                    previewOffsetSec = 0;
                }
                else
                {
                    if(previewOffsetSec >= (float)previewSample.Length.TotalSeconds)
                    {
                        previewOffsetSec = 0;
                    }    
                }
                if (previewLengthSec == -1)
                {
                    previewLengthSec = (float)previewSample.Length.TotalSeconds;
                }
                else
                {
                    previewLengthSec = Math.Min(previewLengthSec, (float)previewSample.Length.TotalSeconds - previewOffsetSec);
                    previewLengthSec = Math.Max(0, previewLengthSec);
                }
                MajDebug.LogDebug($"Playing preview song\nOffset: {previewOffsetSec}s\nLength: {previewLengthSec}s");
                previewSample.SetVolume(MajEnv.Settings.Audio.Volume.BGM);
                //set sample.CurrentSec Not implmented
                previewSample.IsLoop = true;
                if(previewSample.CanSeek)
                {
                    previewSample.CurrentSec = previewOffsetSec;
                }
                else
                {
                    previewSample.Stop();
                }
                previewSample.Speed = 1.0f;
                if (_previewVersion != previewVersion || !ReferenceEquals(_currentPreviewSong, info))
                {
                    return;
                }
                previewSample.Play();
                _isPreviewPlaying = true;
                token.ThrowIfCancellationRequested();
                await UniTask.Delay(500, cancellationToken: token, cancelImmediately: true);
                token.ThrowIfCancellationRequested();
                // 即时切换时从当前音量淡出：背景音乐已静音时不会被拉回原音量
                var fadeFromVolume = isInstantPreview ? selectSound.Volume : MajEnv.Settings.Audio.Volume.BGM;
                for (var i = 1f; i > 0; i = i - 0.2f)
                {
                    token.ThrowIfCancellationRequested();
                    selectSound.Volume = i * fadeFromVolume;
                    await UniTask.Delay(100, cancellationToken: token, cancelImmediately: true);
                }
                while (true)
                {
                    if(previewSample.CanSeek)
                    {
                        var currentSec = previewSample.CurrentSec;
                        if (previewLengthSec != 0)
                        {
                            if (currentSec - (previewOffsetSec + previewLengthSec) > -0.5f)
                            {
                                for (var i = 1f; i > 0; i = i - 0.2f)
                                {
                                    token.ThrowIfCancellationRequested();
                                    previewSample.Volume = i * MajEnv.Settings.Audio.Volume.BGM;
                                    await UniTask.Delay(100, cancellationToken: token, cancelImmediately: true);
                                }
                                previewSample.Pause();
                                await UniTask.Delay(1000, cancellationToken: token, cancelImmediately: true);
                                previewSample.Volume = MajEnv.Settings.Audio.Volume.BGM;
                                previewSample.CurrentSec = previewOffsetSec;
                                previewSample.Play();
                            }
                        }
                    }
                    previewSample.Volume = MajEnv.Settings.Audio.Volume.BGM;
                    await UniTask.Yield(token, cancelImmediately: true);
                }
            }
            finally
            {
                if (_previewVersion == previewVersion && ReferenceEquals(_currentPreviewSong, info))
                {
                    // 即时切换时预览没能播放（加载失败）且没有切到别的歌：恢复选歌背景音乐
                    if (isInstantPreview && !_isPreviewPlaying && !token.IsCancellationRequested)
                    {
                        selectSound.SetVolume(MajEnv.Settings.Audio.Volume.BGM);
                    }
                    _currentPreviewSong = null;
                    _isPreviewPlaying = false;
                }
                if (previewSample is not null && !previewSample.IsEmpty)
                {
                    previewSample.Pause();
                    previewSample.IsLoop = false;
                }
            }
        }
        private void OnDestroy()
        {
            var selectSound = MajInstances.AudioManager.GetSFX("bgm_select.mp3");
            selectSound.SetVolume(MajEnv.Settings.Audio.Volume.BGM);
        }
    }
}
