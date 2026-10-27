using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PAoC_Mod.Utils
{
    public static class EventLoader
    {
        public static IEnumerator LoadSprite(string imagePath, Action<Sprite> onLoaded)
        {
            if (!File.Exists(imagePath))
            {
                Plugin.Logger.LogWarning($"Image not found: {imagePath}");
                onLoaded(null);
                yield break;
            }

            string url = "file://" + imagePath.Replace("\\", "/");
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
            using (request)
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Logger.LogWarning($"Failed to load image '{imagePath}': {request.error}");
                    onLoaded(null);
                    yield break;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                onLoaded(sprite);
            }
        }

        public static IEnumerator LoadAudioClip(string audioPath, Action<AudioClip> onLoaded)
        {
            if (!File.Exists(audioPath))
            {
                Plugin.Logger.LogWarning($"Sound not found: {audioPath}");
                onLoaded(null);
                yield break;
            }

            string url = "file://" + audioPath.Replace("\\", "/");
            AudioType audioType = GetAudioType(audioPath);
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
            using (request)
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Logger.LogWarning($"Failed to load sound '{audioPath}': {request.error}");
                    onLoaded(null);
                    yield break;
                }

                onLoaded(DownloadHandlerAudioClip.GetContent(request));
            }
        }

        private static AudioType GetAudioType(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".wav": return AudioType.WAV;
                case ".mp3": return AudioType.MPEG;
                case ".ogg": return AudioType.OGGVORBIS;
                default:
                    Plugin.Logger.LogWarning($"Unknown audio format '{ext}', defaulting to WAV.");
                    return AudioType.WAV;
            }
        }
    }
}