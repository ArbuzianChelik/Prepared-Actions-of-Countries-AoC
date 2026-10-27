using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PAoC_Mod.Models;
using PAoC_Mod.Utils;
using PAoC_Mod.Executor;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PAoC_Mod.UI
{
    public static class EventPanel
    {
        public static bool IsEventOpen { get; private set; }

        private static Queue<Action> _eventQueue = new Queue<Action>();
        private static bool _isProcessingQueue;

        private const float REFERENCE_WIDTH = 1920f;
        private const float REFERENCE_HEIGHT = 1080f;

        private const float SE_PANEL_WIDTH = 912f;
        private const float SE_PANEL_HEIGHT = 579f;

        private const float SE_IMAGE_LEFT = 52f;
        private const float SE_IMAGE_TOP = 93f;
        private const float SE_IMAGE_WIDTH = 810f;
        private const float SE_IMAGE_HEIGHT = 386f;

        private const float SE_QUOTE_LEFT = 349f;
        private const float SE_QUOTE_TOP = 342f;
        private const float SE_QUOTE_WIDTH = 500f;
        private const float SE_QUOTE_HEIGHT = 137f;

        private const float SE_TITLE_LEFT_FRAC = 0.05f;
        private const float SE_TITLE_RIGHT_FRAC = 0.05f;
        private const float SE_TITLE_TOP = 0.02f;
        private const float SE_TITLE_BOTTOM = 0.15f;

        private const float NEWS_PANEL_WIDTH = 498f;
        private const float NEWS_PANEL_HEIGHT = 579f;

        private const float NEWS_IMAGE_LEFT = 38f;
        private const float NEWS_IMAGE_TOP = 93f;
        private const float NEWS_IMAGE_WIDTH = 422f;
        private const float NEWS_IMAGE_HEIGHT = 152f;

        private const float NEWS_BODY_LEFT = 38f;
        private const float NEWS_BODY_TOP = 265f;
        private const float NEWS_BODY_WIDTH = 422f;
        private const float NEWS_BODY_HEIGHT = 300f;

        private const float NEWS_TITLE_LEFT_FRAC = 0.05f;
        private const float NEWS_TITLE_RIGHT_FRAC = 0.05f;
        private const float NEWS_TITLE_TOP = 0.025f;
        private const float NEWS_TITLE_BOTTOM = 0.145f;

        private static readonly float SE_IMAGE_LEFT_FRAC = SE_IMAGE_LEFT / SE_PANEL_WIDTH;
        private static readonly float SE_IMAGE_RIGHT_FRAC = 1f - (SE_IMAGE_LEFT + SE_IMAGE_WIDTH) / SE_PANEL_WIDTH;
        private static readonly float SE_IMAGE_TOP_FRAC = SE_IMAGE_TOP / SE_PANEL_HEIGHT;
        private static readonly float SE_IMAGE_BOTTOM_FRAC = (SE_IMAGE_TOP + SE_IMAGE_HEIGHT) / SE_PANEL_HEIGHT;

        private static readonly float SE_QUOTE_LEFT_FRAC = SE_QUOTE_LEFT / SE_PANEL_WIDTH;
        private static readonly float SE_QUOTE_RIGHT_FRAC = 1f - (SE_QUOTE_LEFT + SE_QUOTE_WIDTH) / SE_PANEL_WIDTH;
        private static readonly float SE_QUOTE_TOP_FRAC = SE_QUOTE_TOP / SE_PANEL_HEIGHT;
        private static readonly float SE_QUOTE_BOTTOM_FRAC = (SE_QUOTE_TOP + SE_QUOTE_HEIGHT) / SE_PANEL_HEIGHT;

        private static readonly float NEWS_IMAGE_LEFT_FRAC = NEWS_IMAGE_LEFT / NEWS_PANEL_WIDTH;
        private static readonly float NEWS_IMAGE_RIGHT_FRAC = 1f - (NEWS_IMAGE_LEFT + NEWS_IMAGE_WIDTH) / NEWS_PANEL_WIDTH;
        private static readonly float NEWS_IMAGE_TOP_FRAC = NEWS_IMAGE_TOP / NEWS_PANEL_HEIGHT;
        private static readonly float NEWS_IMAGE_BOTTOM_FRAC = (NEWS_IMAGE_TOP + NEWS_IMAGE_HEIGHT) / NEWS_PANEL_HEIGHT;

        private static readonly float NEWS_BODY_LEFT_FRAC = NEWS_BODY_LEFT / NEWS_PANEL_WIDTH;
        private static readonly float NEWS_BODY_RIGHT_FRAC = 1f - (NEWS_BODY_LEFT + NEWS_BODY_WIDTH) / NEWS_PANEL_WIDTH;
        private static readonly float NEWS_BODY_TOP_FRAC = NEWS_BODY_TOP / NEWS_PANEL_HEIGHT;
        private static readonly float NEWS_BODY_BOTTOM_FRAC = (NEWS_BODY_TOP + NEWS_BODY_HEIGHT) / NEWS_PANEL_HEIGHT;

        private const float BUTTON_WIDTH = 190f;
        private const float BUTTON_HEIGHT = 55f;
        private const float BUTTON_BOTTOM_OFFSET = 25f;

        private const int TITLE_FONT_SIZE = 20;
        private const int BODY_FONT_SIZE = 20;
        private const int BUTTON_FONT_SIZE = 22;
        private const int MIN_BUTTON_FONT_SIZE = 12;
        private const int MIN_TEXT_FONT_SIZE = 14;

        private const float OPTION_BUTTON_WIDTH = 174f;
        private const float OPTION_BUTTON_HEIGHT = 37f;
        private const float OPTION_BUTTON_SPACING = 6f;
        private const float TEXT_MARGIN_LEFT = 8f;
        private const float TEXT_MARGIN_TOP = 9f;

        private const float CLOSE_BUTTON_SIZE = 30f;

        private static Sprite _seFrameSprite;
        private static Sprite _seButtonSprite;
        private static Sprite _newsFrameSprite;
        private static Sprite _newsButtonSprite;
        private static Sprite _eventFrameSprite;
        private static Sprite _eventButtonSprite;
        private static Sprite _unavailableButtonSprite;
        private static Sprite _closeButtonSprite;
        private static bool _assetsRequested;

        private static GameObject _currentAudioObject;
        private static AudioSource _currentAudioSource;

        public static IEnumerator EnsureAssets()
        {
            if (_assetsRequested) yield break;
            _assetsRequested = true;

            string imagesDir = Path.Combine(Plugin.ModDirectory, "images");

            yield return EventLoader.LoadSprite(Path.Combine(imagesDir, "se_frame.png"), s => _seFrameSprite = s);
            yield return EventLoader.LoadSprite(Path.Combine(imagesDir, "se_button.png"), s => _seButtonSprite = s);
            yield return EventLoader.LoadSprite(Path.Combine(imagesDir, "news_frame.png"), s => _newsFrameSprite = s);
            yield return EventLoader.LoadSprite(Path.Combine(imagesDir, "news_button.png"), s => _newsButtonSprite = s);
            yield return EventLoader.LoadSprite(Path.Combine(imagesDir, "event_frame.png"), s => _eventFrameSprite = s);
            yield return EventLoader.LoadSprite(Path.Combine(imagesDir, "event_button.png"), s => _eventButtonSprite = s);
            yield return EventLoader.LoadSprite(Path.Combine(imagesDir, "button_unavailable.png"), s => _unavailableButtonSprite = s);
            yield return EventLoader.LoadSprite(Path.Combine(imagesDir, "close_button.png"), s => _closeButtonSprite = s);

            if (_seFrameSprite == null)
                Plugin.Logger.LogWarning("se_frame.png not found - superevent will use a plain background.");
            if (_seButtonSprite == null)
                Plugin.Logger.LogWarning("se_button.png not found - superevent button will use a plain background.");
            if (_newsFrameSprite == null)
                Plugin.Logger.LogWarning("news_frame.png not found - news will fall back to se_frame.png (if available).");
            if (_newsButtonSprite == null)
                Plugin.Logger.LogWarning("news_button.png not found - news button will fall back to se_button.png (if available).");
            if (_eventFrameSprite == null)
                Plugin.Logger.LogWarning("event_frame.png not found - event will fall back to se_frame.png (if available).");
            if (_eventButtonSprite == null)
                Plugin.Logger.LogWarning("event_button.png not found - event button will fall back to se_button.png (if available).");
            if (_unavailableButtonSprite == null)
                Plugin.Logger.LogWarning("button_unavailable.png not found - unavailable buttons will use a plain gray background.");
            if (_closeButtonSprite == null)
                Plugin.Logger.LogWarning("close_button.png not found - close button will use a plain background.");
        }

        private static string ParseColors(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var colorMap = new Dictionary<char, string>
            {
                { 'W', "white" },
                { 'B', "blue" },
                { 'R', "red" },
                { 'G', "green" },
                { 'Y', "yellow" },
                { 'O', "#FFA500" },
                { 'P', "#FF69B4" },
                { 'V', "#8A2BE2" },
                { 'M', "#FF00FF" },
                { 'C', "#00FFFF" },
                { 'b', "black" },
                { 'g', "grey" },
                { 'S', "#C0C0C0" },
            };

            string result = text;
            int i = 0;
            bool colorOpen = false;
            while (i < result.Length)
            {
                if (result[i] == '§' && i + 1 < result.Length)
                {
                    char code = result[i + 1];
                    if (code == '!')
                    {
                        if (colorOpen)
                        {
                            result = result.Remove(i, 2);
                            result = result.Insert(i, "</color>");
                            i += 8;
                            colorOpen = false;
                        }
                        else
                        {
                            result = result.Remove(i, 2);
                        }
                        continue;
                    }
                    else if (colorMap.TryGetValue(code, out string colorName))
                    {
                        string tag = $"<color={colorName}>";
                        result = result.Remove(i, 2);
                        result = result.Insert(i, tag);
                        i += tag.Length;
                        colorOpen = true;
                        continue;
                    }
                    else
                    {
                        result = result.Remove(i, 1);
                        continue;
                    }
                }
                i++;
            }

            if (colorOpen)
                result += "</color>";

            return result;
        }

        public static void StopSuperEventSound()
        {
            if (_currentAudioObject != null)
            {
                if (_currentAudioSource != null)
                    _currentAudioSource.Stop();
                UnityEngine.Object.Destroy(_currentAudioObject);
                _currentAudioObject = null;
                _currentAudioSource = null;
            }
        }

        private static void ProcessQueue()
        {
            if (_isProcessingQueue) return;
            if (_eventQueue.Count == 0) return;
            _isProcessingQueue = true;
            var next = _eventQueue.Dequeue();
            next?.Invoke();
            _isProcessingQueue = false;
            if (_eventQueue.Count > 0 && Plugin.Instance != null)
                Plugin.Instance.StartCoroutine(DelayedProcessQueue());
        }

        private static IEnumerator DelayedProcessQueue()
        {
            yield return null;
            ProcessQueue();
        }

        public static void ShowSuperEvent(SuperEventData data, Sprite image, AudioClip sound)
        {
            if (IsEventOpen)
            {
                Plugin.Logger.LogInfo("Event window already open, queuing superevent.");
                _eventQueue.Enqueue(() => ShowSuperEvent(data, image, sound));
                ProcessQueue();
                return;
            }
            string title = ActionExecutor.ReplaceVariables(data.Title);
            string quote = ActionExecutor.ReplaceVariables(data.Quote);
            string author = ActionExecutor.ReplaceVariables(data.QuoteAuthor);
            string button = ActionExecutor.ReplaceVariables(data.ButtonText);
            Show(title, image, quote, author, button, sound, null, _seFrameSprite, _seButtonSprite, true);
        }

        public static void ShowNews(NewsData data, Sprite image, Action onClosed)
        {
            if (IsEventOpen)
            {
                Plugin.Logger.LogInfo("Event window already open, queuing news.");
                _eventQueue.Enqueue(() => ShowNews(data, image, onClosed));
                ProcessQueue();
                return;
            }
            Sprite frame = _newsFrameSprite != null ? _newsFrameSprite : _seFrameSprite;
            Sprite btn = _newsButtonSprite != null ? _newsButtonSprite : _seButtonSprite;
            string title = ActionExecutor.ReplaceVariables(data.Title);
            string desc = ActionExecutor.ReplaceVariables(data.Description);
            string button = ActionExecutor.ReplaceVariables(data.ButtonText);
            Show(title, image, desc, null, button, null, onClosed, frame, btn, false);
        }

        public static void ShowEvent(EventData data, Sprite image, Action<string> onOptionSelected)
        {
            if (IsEventOpen)
            {
                Plugin.Logger.LogInfo("Event window already open, queuing event.");
                _eventQueue.Enqueue(() => ShowEvent(data, image, onOptionSelected));
                ProcessQueue();
                return;
            }
            ShowEventInternal(data.Title, image, data.Description, data.Options, onOptionSelected, _eventFrameSprite ?? _seFrameSprite, _eventButtonSprite ?? _seButtonSprite, data.CloseButtonEnabled);
        }

        private static void SetupButtonText(TextMeshProUGUI tmp, string text, float maxFontSize, float minFontSize, float availableWidth, float availableHeight, float marginLeft, float marginTop)
        {
            tmp.text = ParseColors(text);
            tmp.fontSize = maxFontSize;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minFontSize;
            tmp.fontSizeMax = maxFontSize;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.margin = new Vector4(marginLeft, marginTop, marginLeft, marginTop);
            tmp.color = Color.black;
            tmp.ForceMeshUpdate();
        }

        private static void Show(string title, Sprite image, string bodyText, string authorText, string buttonText, AudioClip sound, Action onClosed, Sprite frameSprite, Sprite buttonSprite, bool isSuperEvent)
        {
            try
            {
                IsEventOpen = true;
                if (Session.Instance != null)
                    Session.Instance.gamePaused = true;

                if (isSuperEvent)
                {
                    if (_currentAudioObject != null)
                    {
                        if (_currentAudioSource != null)
                            _currentAudioSource.Stop();
                        UnityEngine.Object.Destroy(_currentAudioObject);
                        _currentAudioObject = null;
                        _currentAudioSource = null;
                    }
                }

                var canvasGO = new GameObject("PAoC_EventCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;

                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                var root = canvasGO;

                var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bgGO.transform.SetParent(root.transform, false);
                bgGO.GetComponent<RectTransform>().anchorMin = Vector2.zero;
                bgGO.GetComponent<RectTransform>().anchorMax = Vector2.one;
                bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
                bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                bgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

                float panelWidth, panelHeight;
                if (frameSprite != null && frameSprite.texture != null)
                {
                    panelWidth = frameSprite.texture.width;
                    panelHeight = frameSprite.texture.height;
                }
                else
                {
                    panelWidth = isSuperEvent ? SE_PANEL_WIDTH : NEWS_PANEL_WIDTH;
                    panelHeight = isSuperEvent ? SE_PANEL_HEIGHT : NEWS_PANEL_HEIGHT;
                }

                var panelGO = new GameObject("Panel", typeof(RectTransform));
                panelGO.transform.SetParent(root.transform, false);
                var panelRect = panelGO.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

                if (image != null)
                {
                    var imgGO = new GameObject("ContentImage", typeof(RectTransform), typeof(Image));
                    imgGO.transform.SetParent(panelGO.transform, false);
                    var imgRect = imgGO.GetComponent<RectTransform>();

                    if (isSuperEvent)
                    {
                        imgRect.anchorMin = new Vector2(SE_IMAGE_LEFT_FRAC, 1f - SE_IMAGE_BOTTOM_FRAC);
                        imgRect.anchorMax = new Vector2(1f - SE_IMAGE_RIGHT_FRAC, 1f - SE_IMAGE_TOP_FRAC);
                    }
                    else
                    {
                        imgRect.anchorMin = new Vector2(NEWS_IMAGE_LEFT_FRAC, 1f - NEWS_IMAGE_BOTTOM_FRAC);
                        imgRect.anchorMax = new Vector2(1f - NEWS_IMAGE_RIGHT_FRAC, 1f - NEWS_IMAGE_TOP_FRAC);
                    }
                    imgRect.offsetMin = Vector2.zero;
                    imgRect.offsetMax = Vector2.zero;

                    var img = imgGO.GetComponent<Image>();
                    img.sprite = image;
                    img.preserveAspect = false;
                    img.type = Image.Type.Simple;
                }

                var frameGO = new GameObject("Frame", typeof(RectTransform), typeof(Image));
                frameGO.transform.SetParent(panelGO.transform, false);
                var frameRect = frameGO.GetComponent<RectTransform>();
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
                var frameImage = frameGO.GetComponent<Image>();
                if (frameSprite != null)
                {
                    frameImage.sprite = frameSprite;
                    frameImage.type = Image.Type.Simple;
                }
                else
                {
                    frameImage.color = new Color(0.25f, 0.15f, 0.13f, 1f);
                }

                float titleTop, titleBottom, titleLeftFrac, titleRightFrac;
                if (isSuperEvent)
                {
                    titleTop = SE_TITLE_TOP;
                    titleBottom = SE_TITLE_BOTTOM;
                    titleLeftFrac = SE_TITLE_LEFT_FRAC;
                    titleRightFrac = SE_TITLE_RIGHT_FRAC;
                }
                else
                {
                    titleTop = NEWS_TITLE_TOP;
                    titleBottom = NEWS_TITLE_BOTTOM;
                    titleLeftFrac = NEWS_TITLE_LEFT_FRAC;
                    titleRightFrac = NEWS_TITLE_RIGHT_FRAC;
                }
                AddBand(panelGO.transform, "Title", TITLE_FONT_SIZE, titleTop, titleBottom, titleLeftFrac, titleRightFrac, title, TextAlignmentOptions.Center, false);

                if (isSuperEvent)
                {
                    string fullQuote = bodyText;
                    if (!string.IsNullOrEmpty(authorText))
                        fullQuote += "\n- " + authorText;

                    AddBand(panelGO.transform, "Quote", BODY_FONT_SIZE,
                        SE_QUOTE_TOP_FRAC, SE_QUOTE_BOTTOM_FRAC,
                        SE_QUOTE_LEFT_FRAC, SE_QUOTE_RIGHT_FRAC,
                        fullQuote, TextAlignmentOptions.TopRight, true);
                }
                else
                {
                    AddBand(panelGO.transform, "Body", BODY_FONT_SIZE,
                        NEWS_BODY_TOP_FRAC, NEWS_BODY_BOTTOM_FRAC,
                        NEWS_BODY_LEFT_FRAC, NEWS_BODY_RIGHT_FRAC,
                        bodyText, TextAlignmentOptions.Top, true);
                }

                var buttonGO = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonGO.transform.SetParent(panelGO.transform, false);
                var buttonRect = buttonGO.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0.5f, 0f);
                buttonRect.anchorMax = new Vector2(0.5f, 0f);
                buttonRect.pivot = new Vector2(0.5f, 0f);
                buttonRect.anchoredPosition = new Vector2(0f, BUTTON_BOTTOM_OFFSET);
                buttonRect.sizeDelta = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);

                var buttonImg = buttonGO.GetComponent<Image>();
                if (buttonSprite != null)
                {
                    buttonImg.sprite = buttonSprite;
                    buttonImg.type = Image.Type.Simple;
                }
                else
                {
                    buttonImg.color = new Color(0.6f, 0.45f, 0.25f, 1f);
                }

                var button = buttonGO.GetComponent<Button>();
                button.targetGraphic = buttonImg;
                ColorBlock cb = button.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.7f, 0.7f, 0.7f);
                cb.pressedColor = new Color(0.5f, 0.5f, 0.5f);
                cb.colorMultiplier = 1f;
                button.colors = cb;
                button.interactable = true;

                var buttonTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                buttonTextGO.transform.SetParent(buttonGO.transform, false);
                var buttonTextRect = buttonTextGO.GetComponent<RectTransform>();
                buttonTextRect.anchorMin = Vector2.zero;
                buttonTextRect.anchorMax = Vector2.one;
                buttonTextRect.offsetMin = Vector2.zero;
                buttonTextRect.offsetMax = Vector2.zero;
                var buttonTmp = buttonTextGO.GetComponent<TextMeshProUGUI>();

                string btnText = string.IsNullOrEmpty(buttonText) ? "OK" : buttonText;
                SetupButtonText(buttonTmp, btnText, BUTTON_FONT_SIZE, MIN_BUTTON_FONT_SIZE, BUTTON_WIDTH - TEXT_MARGIN_LEFT * 2, BUTTON_HEIGHT - TEXT_MARGIN_TOP * 2, TEXT_MARGIN_LEFT, TEXT_MARGIN_TOP);

                button.onClick.AddListener(new UnityAction(() =>
                {
                    if (Session.Instance != null)
                        Session.Instance.gamePaused = false;
                    UnityEngine.Object.Destroy(root);
                    IsEventOpen = false;
                    onClosed?.Invoke();
                    ProcessQueue();
                }));

                if (isSuperEvent && sound != null)
                {
                    var audioGO = new GameObject("PAoC_EventSound", typeof(AudioSource));
                    var source = audioGO.GetComponent<AudioSource>();
                    source.clip = sound;
                    source.Play();
                    _currentAudioObject = audioGO;
                    _currentAudioSource = source;
                    if (Plugin.Instance != null)
                        Plugin.Instance.StartCoroutine(DestroyAfterPlay(audioGO, source));
                }

                Canvas.ForceUpdateCanvases();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to show event panel: {ex}");
                IsEventOpen = false;
                onClosed?.Invoke();
                ProcessQueue();
            }
        }

        private static void ShowEventInternal(string title, Sprite image, string description, List<EventOption> options, Action<string> onOptionSelected, Sprite frameSprite, Sprite buttonSprite, bool closeButtonEnabled)
        {
            try
            {
                IsEventOpen = true;
                if (Session.Instance != null)
                    Session.Instance.gamePaused = true;

                title = ActionExecutor.ReplaceVariables(title);
                description = ActionExecutor.ReplaceVariables(description);

                var canvasGO = new GameObject("PAoC_EventCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;

                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                var root = canvasGO;

                var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bgGO.transform.SetParent(root.transform, false);
                bgGO.GetComponent<RectTransform>().anchorMin = Vector2.zero;
                bgGO.GetComponent<RectTransform>().anchorMax = Vector2.one;
                bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
                bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                bgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

                float panelWidth, panelHeight;
                if (frameSprite != null && frameSprite.texture != null)
                {
                    panelWidth = frameSprite.texture.width;
                    panelHeight = frameSprite.texture.height;
                }
                else
                {
                    panelWidth = NEWS_PANEL_WIDTH;
                    panelHeight = NEWS_PANEL_HEIGHT;
                }

                var panelGO = new GameObject("Panel", typeof(RectTransform));
                panelGO.transform.SetParent(root.transform, false);
                var panelRect = panelGO.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

                float imageLeft = 38f;
                float imageTop = 387f;
                float imageWidth = 218f;
                float imageHeight = 152f;
                float imageLeftFrac = imageLeft / panelWidth;
                float imageRightFrac = 1f - (imageLeft + imageWidth) / panelWidth;
                float imageTopFrac = imageTop / panelHeight;
                float imageBottomFrac = (imageTop + imageHeight) / panelHeight;

                if (image != null)
                {
                    var imgGO = new GameObject("ContentImage", typeof(RectTransform), typeof(Image));
                    imgGO.transform.SetParent(panelGO.transform, false);
                    var imgRect = imgGO.GetComponent<RectTransform>();
                    imgRect.anchorMin = new Vector2(imageLeftFrac, 1f - imageBottomFrac);
                    imgRect.anchorMax = new Vector2(1f - imageRightFrac, 1f - imageTopFrac);
                    imgRect.offsetMin = Vector2.zero;
                    imgRect.offsetMax = Vector2.zero;
                    var img = imgGO.GetComponent<Image>();
                    img.sprite = image;
                    img.preserveAspect = false;
                    img.type = Image.Type.Simple;
                }

                var frameGO = new GameObject("Frame", typeof(RectTransform), typeof(Image));
                frameGO.transform.SetParent(panelGO.transform, false);
                var frameRect = frameGO.GetComponent<RectTransform>();
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
                var frameImage = frameGO.GetComponent<Image>();
                if (frameSprite != null)
                {
                    frameImage.sprite = frameSprite;
                    frameImage.type = Image.Type.Simple;
                }
                else
                {
                    frameImage.color = new Color(0.25f, 0.15f, 0.13f, 1f);
                }

                AddBand(panelGO.transform, "Title", TITLE_FONT_SIZE, NEWS_TITLE_TOP, NEWS_TITLE_BOTTOM, NEWS_TITLE_LEFT_FRAC, NEWS_TITLE_RIGHT_FRAC, title, TextAlignmentOptions.Center, false);

                float descTop = 0.16f;
                float descBottom = 0.68f;
                float descLeft = 0.05f;
                float descRight = 0.05f;
                AddBand(panelGO.transform, "Description", BODY_FONT_SIZE, descTop, descBottom, descLeft, descRight, description, TextAlignmentOptions.TopLeft, true);

                int optionCount = Math.Min(options.Count, 5);
                float startX = 280f;
                float startYFromTop = imageTop - 15f;
                float bottomMargin = BUTTON_BOTTOM_OFFSET;

                float buttonWidth = OPTION_BUTTON_WIDTH;
                float availableHeight = panelHeight - startYFromTop - bottomMargin;
                if (availableHeight < 10f) availableHeight = 10f;

                float spacing = 6f;
                float minSpacing = 4f;
                float maxSpacing = 10f;
                float minButtonHeight = 20f;
                float maxButtonHeight = 55f;

                float buttonHeight = (availableHeight - (optionCount - 1) * spacing) / optionCount;

                if (buttonHeight > maxButtonHeight)
                {
                    buttonHeight = maxButtonHeight;
                    spacing = (availableHeight - optionCount * buttonHeight) / (optionCount - 1);
                    if (optionCount == 1) spacing = 0f;
                    spacing = Mathf.Clamp(spacing, minSpacing, maxSpacing);
                }
                else if (buttonHeight < minButtonHeight)
                {
                    spacing = minSpacing;
                    buttonHeight = (availableHeight - (optionCount - 1) * spacing) / optionCount;
                    if (buttonHeight < minButtonHeight)
                        buttonHeight = minButtonHeight;
                }

                float maxFontSize = Mathf.Min(22f, buttonHeight * 0.6f);
                if (maxFontSize < 12f) maxFontSize = 12f;

                int countryId = 0;
                if (Session.Instance != null && Session.Instance.selectedNation != null)
                    countryId = Session.Instance.selectedNation.id;
                Nation selectedCountry = GameAccessor.GetNationById(countryId);

                for (int i = 0; i < optionCount; i++)
                {
                    var option = options[i];
                    bool available = true;
                    int trueCount = 0;
                    int totalCount = 0;

                    if (option.AvailableConditions != null && option.AvailableConditions.Count > 0)
                    {
                        totalCount = option.AvailableConditions.Count;

                        foreach (var condition in option.AvailableConditions)
                        {
                            if (condition.IsNegatedGroup)
                            {
                                if (ActionExecutor.EvaluateCondition(null, condition))
                                    trueCount++;
                                continue;
                            }

                            if (condition.Type != "var" && !condition.CountryId.HasValue)
                            {
                                Plugin.Logger.LogWarning($"Condition '{condition.Type}' has no CountryId and is not global (var). It will be treated as false.");
                                continue;
                            }

                            Nation targetCountry = null;
                            if (condition.CountryId.HasValue)
                                targetCountry = GameAccessor.GetNationById(condition.CountryId.Value);

                            if (targetCountry == null && condition.Type != "var")
                                continue;

                            if (ActionExecutor.EvaluateCondition(targetCountry, condition))
                                trueCount++;
                        }

                        if (option.AvailableOp.HasValue)
                        {
                            switch (option.AvailableOp.Value)
                            {
                                case ConditionOperator.All:
                                    available = (totalCount > 0 && trueCount == totalCount);
                                    break;
                                case ConditionOperator.Exact:
                                    available = (trueCount == option.AvailableValue);
                                    break;
                                case ConditionOperator.GreaterOrEqual:
                                    available = (trueCount >= option.AvailableValue);
                                    break;
                                case ConditionOperator.LessOrEqual:
                                    available = (trueCount <= option.AvailableValue);
                                    break;
                                default:
                                    available = false;
                                    break;
                            }
                        }
                        else
                        {
                            available = (totalCount > 0 && trueCount == totalCount);
                        }
                    }

                    float yPosFromTop = startYFromTop + i * (buttonHeight + spacing);
                    float yFromBottom = panelHeight - yPosFromTop - buttonHeight;

                    var btnGO = new GameObject("OptionButton_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                    btnGO.transform.SetParent(panelGO.transform, false);
                    var btnRect = btnGO.GetComponent<RectTransform>();
                    btnRect.anchorMin = new Vector2(0f, 0f);
                    btnRect.anchorMax = new Vector2(0f, 0f);
                    btnRect.pivot = new Vector2(0f, 0f);
                    btnRect.anchoredPosition = new Vector2(startX, yFromBottom);
                    btnRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

                    var btnImg = btnGO.GetComponent<Image>();
                    if (available)
                    {
                        if (buttonSprite != null)
                        {
                            btnImg.sprite = buttonSprite;
                            btnImg.type = Image.Type.Simple;
                            btnImg.color = Color.white;
                        }
                        else
                        {
                            btnImg.color = new Color(0.6f, 0.45f, 0.25f, 1f);
                        }
                    }
                    else
                    {
                        if (_unavailableButtonSprite != null)
                        {
                            btnImg.sprite = _unavailableButtonSprite;
                            btnImg.type = Image.Type.Simple;
                            btnImg.color = Color.white;
                        }
                        else
                        {
                            btnImg.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                        }
                    }

                    var btn = btnGO.GetComponent<Button>();
                    btn.targetGraphic = btnImg;
                    ColorBlock cb = btn.colors;
                    cb.normalColor = Color.white;
                    cb.highlightedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                    cb.pressedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                    cb.disabledColor = Color.white;
                    cb.colorMultiplier = 1f;
                    btn.colors = cb;
                    btn.interactable = available;

                    var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                    textGO.transform.SetParent(btnGO.transform, false);
                    var textRect = textGO.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = Vector2.zero;
                    textRect.offsetMax = Vector2.zero;
                    var tmp = textGO.GetComponent<TextMeshProUGUI>();

                    string optionText = ActionExecutor.ReplaceVariables(option.Name);
                    SetupButtonText(tmp, optionText, maxFontSize, MIN_BUTTON_FONT_SIZE, buttonWidth - TEXT_MARGIN_LEFT * 2, buttonHeight - TEXT_MARGIN_TOP * 2, TEXT_MARGIN_LEFT, TEXT_MARGIN_TOP);
                    tmp.color = Color.black;

                    string optionName = option.Name;
                    btn.onClick.AddListener(new UnityAction(() =>
                    {
                        if (available)
                        {
                            if (Session.Instance != null)
                                Session.Instance.gamePaused = false;
                            UnityEngine.Object.Destroy(root);
                            IsEventOpen = false;
                            onOptionSelected?.Invoke(optionName);
                            ProcessQueue();
                        }
                    }));
                }

                if (closeButtonEnabled)
                {
                    var closeBtnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
                    closeBtnGO.transform.SetParent(panelGO.transform, false);
                    var closeRect = closeBtnGO.GetComponent<RectTransform>();
                    closeRect.anchorMin = new Vector2(1f, 1f);
                    closeRect.anchorMax = new Vector2(1f, 1f);
                    closeRect.pivot = new Vector2(1f, 1f);
                    closeRect.anchoredPosition = Vector2.zero;
                    closeRect.sizeDelta = new Vector2(CLOSE_BUTTON_SIZE, CLOSE_BUTTON_SIZE);

                    var closeImg = closeBtnGO.GetComponent<Image>();
                    if (_closeButtonSprite != null)
                    {
                        closeImg.sprite = _closeButtonSprite;
                        closeImg.type = Image.Type.Simple;
                    }
                    else
                    {
                        closeImg.color = new Color(0.6f, 0.2f, 0.2f, 0.8f);
                    }

                    var closeBtn = closeBtnGO.GetComponent<Button>();
                    closeBtn.targetGraphic = closeImg;
                    ColorBlock closeCb = closeBtn.colors;
                    closeCb.normalColor = Color.white;
                    closeCb.highlightedColor = new Color(0.8f, 0.8f, 0.8f);
                    closeCb.pressedColor = new Color(0.6f, 0.6f, 0.6f);
                    closeCb.colorMultiplier = 1f;
                    closeBtn.colors = closeCb;
                    closeBtn.interactable = true;

                    closeBtn.onClick.AddListener(new UnityAction(() =>
                    {
                        if (Session.Instance != null)
                            Session.Instance.gamePaused = false;
                        UnityEngine.Object.Destroy(root);
                        IsEventOpen = false;
                        ProcessQueue();
                    }));
                }

                Canvas.ForceUpdateCanvases();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to show event panel: {ex}");
                IsEventOpen = false;
                ProcessQueue();
            }
        }

        private static IEnumerator DestroyAfterPlay(GameObject audioGO, AudioSource source)
        {
            while (source != null && source.isPlaying)
                yield return null;
            if (audioGO != null)
            {
                if (_currentAudioObject == audioGO)
                {
                    _currentAudioObject = null;
                    _currentAudioSource = null;
                }
                UnityEngine.Object.Destroy(audioGO);
            }
        }

        private static void AddBand(Transform parent, string name, int fontSize, float topFraction, float bottomFraction,
            float leftFraction, float rightFraction, string text, TextAlignmentOptions alignment, bool autoShrink = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(leftFraction, 1f - bottomFraction);
            rect.anchorMax = new Vector2(1f - rightFraction, 1f - topFraction);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = ParseColors(text);
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;

            if (autoShrink)
            {
                Canvas.ForceUpdateCanvases();
                tmp.ForceMeshUpdate();

                float availableWidth = rect.rect.width;
                float availableHeight = rect.rect.height;
                float currentFontSize = fontSize;
                float minFontSize = MIN_TEXT_FONT_SIZE;

                while (currentFontSize > minFontSize)
                {
                    tmp.fontSize = currentFontSize;
                    tmp.ForceMeshUpdate();
                    float prefWidth = tmp.preferredWidth;
                    float prefHeight = tmp.preferredHeight;
                    if (prefWidth <= availableWidth && prefHeight <= availableHeight)
                        break;
                    currentFontSize -= 0.5f;
                }
                tmp.fontSize = currentFontSize;
            }
        }
    }
}