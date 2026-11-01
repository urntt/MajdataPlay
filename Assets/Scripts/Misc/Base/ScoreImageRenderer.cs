using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// 把要分享的成绩画成 PNG：用 Unity UI 在离屏摄像机上渲染。左上角是玩家头像与昵称，右上角是“Best X”与日期；
    /// 每行 5 张卡片（曲绘、曲名、难度与等级、达成率、评级、FC/AP、DX 分、游玩次数），每张图最多 50 条，超过时分成多张
    /// </summary>
    internal static class ScoreImageRenderer
    {
        const int COLUMNS = 5;
        const int ENTRIES_PER_IMAGE = 50;
        const int PADDING = 40;
        const int GAP = 20;
        const int HEADER_HEIGHT = 140;
        const int AVATAR_SIZE = 84;
        const int CARD_WIDTH = 400;
        const int CARD_HEIGHT = 184;
        const int COVER_SIZE = CARD_HEIGHT - 24;
        const int MIN_WIDTH = 960;
        /// <summary>
        /// 放在远离游戏画面的位置并使用隐藏层，只由离屏摄像机渲染
        /// </summary>
        static readonly Vector3 RENDER_ORIGIN = new Vector3(10000f, 10000f, 0f);

        static readonly Color BACKGROUND_COLOR = new Color(0.07f, 0.07f, 0.11f, 1f);
        static readonly Color CARD_COLOR = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color COVER_PLACEHOLDER_COLOR = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color SUB_TEXT_COLOR = new Color(1f, 1f, 1f, 0.7f);
        static readonly Color GOLD_COLOR = new Color(1f, 0.82f, 0.3f, 1f);
        static readonly Color FC_COLOR = new Color(0.55f, 0.9f, 0.55f, 1f);
        static readonly Color AVATAR_PLACEHOLDER_COLOR = new Color32(0x58, 0x6F, 0xDD, 0xFF);

        /// <returns>生成的 PNG 文件路径</returns>
        public static async UniTask<List<string>> RenderAsync(IReadOnlyList<ScoreExchange.ScoreEntry> entries, ScoreExchange.ShareHeader header, string directory, string baseName)
        {
            var font = FindFont();
            var paths = new List<string>();
            var pageCount = (entries.Count + ENTRIES_PER_IMAGE - 1) / ENTRIES_PER_IMAGE;
            var circle = UiSprites.Circle;
            for (var page = 0; page < pageCount; page++)
            {
                var start = page * ENTRIES_PER_IMAGE;
                var count = Math.Min(ENTRIES_PER_IMAGE, entries.Count - start);
                var covers = new Sprite?[count];
                for (var i = 0; i < count; i++)
                {
                    covers[i] = await LoadCoverAsync(entries[start + i].Song);
                }
                var pageText = pageCount > 1 ? $"{page + 1}/{pageCount}" : string.Empty;
                var png = RenderPage(entries, start, count, covers, header, pageText, circle, font);
                var path = Path.Combine(directory, pageCount > 1 ? $"{baseName}-{page + 1}.png" : $"{baseName}.png");
                File.WriteAllBytes(path, png);
                paths.Add(path);
                await UniTask.Yield();
            }
            return paths;
        }
        static async UniTask<Sprite?> LoadCoverAsync(ISongDetail? song)
        {
            if (song is null)
            {
                return null;
            }
            try
            {
                return await song.GetCoverAsync(true);
            }
            catch (Exception e)
            {
                MajDebug.LogWarning($"[ScoreImageRenderer] Failed to load cover of {song.Title}: {e.Message}");
                return null;
            }
        }
        /// <summary>
        /// 使用当前场景里文字的字体（含中日文），找不到时用 TMP 的默认字体
        /// </summary>
        static TMP_FontAsset? FindFont()
        {
            var text = UnityEngine.Object.FindFirstObjectByType<TMP_Text>();
            if (text != null && text.font != null)
            {
                return text.font;
            }
            return TMP_Settings.defaultFontAsset;
        }
        static byte[] RenderPage(IReadOnlyList<ScoreExchange.ScoreEntry> entries, int start, int count, Sprite?[] covers,
                                 ScoreExchange.ShareHeader header, string pageText, Sprite circle, TMP_FontAsset? font)
        {
            var columns = Math.Min(COLUMNS, count);
            var rows = (count + COLUMNS - 1) / COLUMNS;
            var gridWidth = columns * CARD_WIDTH + (columns - 1) * GAP;
            var width = Math.Max(MIN_WIDTH, gridWidth + PADDING * 2);
            var height = HEADER_HEIGHT + rows * CARD_HEIGHT + (rows - 1) * GAP + PADDING;
            var gridLeft = (width - gridWidth) / 2;
            var layer = MajEnv.HIDDEN_LAYER;

            var cameraObject = new GameObject("ScoreImageCamera");
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var canvasObject = new GameObject("ScoreImageCanvas");
            Texture2D? texture = null;
            try
            {
                cameraObject.transform.position = RENDER_ORIGIN + new Vector3(0, 0, -10f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = height / 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = BACKGROUND_COLOR;
                camera.cullingMask = 1 << layer;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.targetTexture = renderTexture;

                canvasObject.layer = layer;
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 5f;
                var root = canvasObject.transform;

                CreateHeader(root, font, header, pageText, circle, width);

                for (var i = 0; i < count; i++)
                {
                    var column = i % COLUMNS;
                    var row = i / COLUMNS;
                    var x = gridLeft + column * (CARD_WIDTH + GAP);
                    var y = HEADER_HEIGHT + row * (CARD_HEIGHT + GAP);
                    CreateCard(root, font, entries[start + i], start + i + 1, covers[i], x, y);
                }

                SetLayerRecursively(canvasObject.transform, layer);
                Canvas.ForceUpdateCanvases();
                foreach (var text in canvasObject.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                }
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false);
                RenderTexture.active = previous;
                return texture.EncodeToPNG();
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
                UnityEngine.Object.Destroy(canvasObject);
                UnityEngine.Object.Destroy(cameraObject);
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
        }
        /// <summary>
        /// 左上角：圆形头像（未登录时为带首字母的圆）与昵称；右上角：“Best X”/“All N”与日期，当前谱面只有日期
        /// </summary>
        static void CreateHeader(Transform root, TMP_FontAsset? font, ScoreExchange.ShareHeader header, string pageText, Sprite circle, int width)
        {
            var avatarTop = (HEADER_HEIGHT - AVATAR_SIZE) / 2;
            var avatarMask = CreateImage(root, circle, header.Avatar != null ? Color.white : AVATAR_PLACEHOLDER_COLOR);
            Place(avatarMask.rectTransform, PADDING, avatarTop, AVATAR_SIZE, AVATAR_SIZE);
            if (header.Avatar != null)
            {
                var mask = avatarMask.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var avatar = CreateImage(avatarMask.transform, header.Avatar, Color.white);
                Fill(avatar.rectTransform);
            }
            else
            {
                var initial = string.IsNullOrEmpty(header.PlayerName) ? "G" : System.Globalization.StringInfo.GetNextTextElement(header.PlayerName).ToUpperInvariant();
                var initialText = CreateText(avatarMask.transform, font, initial, 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                Fill(initialText.rectTransform);
            }

            var rightWidth = 420;
            var nameLeft = PADDING + AVATAR_SIZE + 20;
            var nameText = CreateText(root, font, header.PlayerName, 38f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Place(nameText.rectTransform, nameLeft, avatarTop, width - nameLeft - PADDING - rightWidth - 20, AVATAR_SIZE);

            var dateLine = string.IsNullOrEmpty(pageText) ? header.Date : $"{header.Date} · {pageText}";
            var rightLeft = width - PADDING - rightWidth;
            if (string.IsNullOrEmpty(header.Label))
            {
                var dateText = CreateText(root, font, dateLine, 30f, SUB_TEXT_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
                Place(dateText.rectTransform, rightLeft, avatarTop, rightWidth, AVATAR_SIZE);
            }
            else
            {
                var labelText = CreateText(root, font, header.Label, 40f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
                Place(labelText.rectTransform, rightLeft, avatarTop - 10, rightWidth, 60);
                var dateText = CreateText(root, font, dateLine, 24f, SUB_TEXT_COLOR, TextAlignmentOptions.Right, FontStyles.Normal);
                Place(dateText.rectTransform, rightLeft, avatarTop + 52, rightWidth, 36);
            }
        }
        static void CreateCard(Transform parent, TMP_FontAsset? font, ScoreExchange.ScoreEntry entry, int rank, Sprite? cover, int x, int y)
        {
            var card = CreateImage(parent, null, CARD_COLOR);
            Place(card.rectTransform, x, y, CARD_WIDTH, CARD_HEIGHT);
            var cardRoot = card.transform;

            // 圆角曲绘：外层圆角底作遮罩，里面放曲绘
            var coverFrame = CreateImage(cardRoot, null, COVER_PLACEHOLDER_COLOR);
            UiSprites.ApplyRounded(coverFrame, 14f);
            coverFrame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Place(coverFrame.rectTransform, 12, 12, COVER_SIZE, COVER_SIZE);
            if (cover != null)
            {
                var coverImage = CreateImage(coverFrame.transform, cover, Color.white);
                coverImage.preserveAspect = true;
                Fill(coverImage.rectTransform);
            }

            var rankBack = CreateImage(cardRoot, null, new Color(0f, 0f, 0f, 0.6f));
            UiSprites.ApplyRounded(rankBack, 8f);
            Place(rankBack.rectTransform, 12, 12, 56, 30);
            var rankText = CreateText(rankBack.transform, font, $"#{rank}", 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Fill(rankText.rectTransform);

            var textLeft = COVER_SIZE + 24;
            var textWidth = CARD_WIDTH - textLeft - 12;
            var halfWidth = textWidth / 2;

            var difficulty = CreateImage(cardRoot, null, GetDifficultyColor(entry.Level));
            Place(difficulty.rectTransform, textLeft, 12, textWidth, 30);
            var difficultyLabel = string.IsNullOrEmpty(entry.LevelText) ? entry.DifficultyName : $"{entry.DifficultyName} {entry.LevelText}";
            var difficultyText = CreateText(difficulty.transform, font, difficultyLabel, 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Fill(difficultyText.rectTransform);

            // 每段文字的框高约为字号的 1.5 倍，否则 TMP 会把放不下的整行省略掉
            var titleText = CreateText(cardRoot, font, entry.Title, 20f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            Place(titleText.rectTransform, textLeft, 46, textWidth, 30);

            var achievementText = CreateText(cardRoot, font, entry.AchievementText, 28f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Place(achievementText.rectTransform, textLeft, 76, textWidth, 42);

            var gradeColor = entry.IsPlayed && entry.Score.Acc.DX >= 100 ? GOLD_COLOR : Color.white;
            var gradeText = CreateText(cardRoot, font, entry.GradeText, 22f, gradeColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Place(gradeText.rectTransform, textLeft, 118, halfWidth, 33);

            if (!string.IsNullOrEmpty(entry.ComboText))
            {
                var comboColor = entry.ComboText.StartsWith("AP", StringComparison.Ordinal) ? GOLD_COLOR : FC_COLOR;
                var comboText = CreateText(cardRoot, font, entry.ComboText, 22f, comboColor, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                Place(comboText.rectTransform, textLeft + halfWidth, 118, textWidth - halfWidth, 33);
            }

            var dxText = CreateText(cardRoot, font, entry.DxScoreText, 18f, SUB_TEXT_COLOR, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            Place(dxText.rectTransform, textLeft, 151, halfWidth + 20, 27);
            var playCountText = CreateText(cardRoot, font, entry.PlayCountText, 18f, SUB_TEXT_COLOR, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
            Place(playCountText.rectTransform, textLeft + halfWidth + 20, 151, textWidth - halfWidth - 20, 27);
        }
        static Color GetDifficultyColor(ChartLevel level)
        {
            return level switch
            {
                ChartLevel.Easy => new Color32(0x0E, 0x9B, 0xEE, 0xFF),
                ChartLevel.Basic => new Color32(0x22, 0xBB, 0x5B, 0xFF),
                ChartLevel.Advance => new Color32(0xFB, 0x9C, 0x2D, 0xFF),
                ChartLevel.Expert => new Color32(0xF6, 0x48, 0x61, 0xFF),
                ChartLevel.Master => new Color32(0x9E, 0x45, 0xE2, 0xFF),
                ChartLevel.ReMaster => new Color32(0xBA, 0x67, 0xF8, 0xFF),
                _ => new Color32(0xE8, 0x37, 0x9C, 0xFF),
            };
        }
        static Image CreateImage(Transform parent, Sprite? sprite, Color color)
        {
            var obj = new GameObject("Image", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        static TextMeshProUGUI CreateText(Transform parent, TMP_FontAsset? font, string content, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            var obj = new GameObject("Text", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }
            // 场景字体的材质带描边和阴影，换成干净的材质
            CleanTmpMaterial.Apply(text);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
        /// <summary>
        /// 以左上角为原点、像素为单位摆放
        /// </summary>
        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
        static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }
    }
}
