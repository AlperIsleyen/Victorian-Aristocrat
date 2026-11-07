using System.Linq;
using UnityEngine;

namespace VictorianAristocrat
{
    public static class GameSettings
    {
        public static float Sensitivity { get { return PlayerPrefs.GetFloat("va_mouse", 1f); } }
        public static float Volume { get { return PlayerPrefs.GetFloat("va_volume", 1f); } }

        public static void Apply()
        {
            AudioListener.volume = Volume;
            int quality = PlayerPrefs.GetInt("va_quality", QualitySettings.GetQualityLevel());
            QualitySettings.SetQualityLevel(Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1));
            int width = PlayerPrefs.GetInt("va_width", Screen.width);
            int height = PlayerPrefs.GetInt("va_height", Screen.height);
            bool full = PlayerPrefs.GetInt("va_full", Screen.fullScreen ? 1 : 0) == 1;
            if (Screen.resolutions.Any(r => r.width == width && r.height == height))
                Screen.SetResolution(width, height, full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            else Screen.fullScreenMode = full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        public static void SetSensitivity(float value) { PlayerPrefs.SetFloat("va_mouse", Mathf.Clamp(value, .5f, 2f)); PlayerPrefs.Save(); }
        public static void SetVolume(float value) { PlayerPrefs.SetFloat("va_volume", Mathf.Clamp01(value)); PlayerPrefs.Save(); AudioListener.volume = Volume; }
        public static void SetQuality(int value) { QualitySettings.SetQualityLevel(value); PlayerPrefs.SetInt("va_quality", value); PlayerPrefs.Save(); }
        public static void SetFullScreen(bool value)
        {
            PlayerPrefs.SetInt("va_full", value ? 1 : 0); PlayerPrefs.Save();
            Screen.SetResolution(Screen.width, Screen.height, value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
        public static void SetResolution(Resolution resolution)
        {
            PlayerPrefs.SetInt("va_width", resolution.width); PlayerPrefs.SetInt("va_height", resolution.height); PlayerPrefs.Save();
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
        }
    }
}
