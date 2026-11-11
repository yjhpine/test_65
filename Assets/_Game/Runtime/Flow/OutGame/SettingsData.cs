using System;
using UnityEngine;

namespace ActionPlatformer.Flow
{
    public enum ScreenModeSetting { ProjectDefault = -1, Windowed = 0, FullScreen = 1 }

    // Saved settings (planning doc 7.1). Every field is checked on load and only a bad one falls back to its default.
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentVersion = 1;
        public const int DefaultVolume = 100, DefaultIntensity = 100;

        public int version = CurrentVersion;
        public int masterVolume = DefaultVolume;
        public int musicVolume = DefaultVolume;
        public int sfxVolume = DefaultVolume;
        // ProjectDefault leaves the window as the player settings opened it; only a kept change is written.
        public int screenMode = (int)ScreenModeSetting.ProjectDefault;
        public int resolutionWidth;
        public int resolutionHeight;
        public int shake = DefaultIntensity;
        public int distortion = DefaultIntensity;
        public int flash = DefaultIntensity;
        public string bindingOverrides = "";

        public ScreenModeSetting ScreenMode => (ScreenModeSetting)screenMode;
        public bool HasResolution => resolutionWidth > 0 && resolutionHeight > 0;

        public SettingsData Clone() => (SettingsData)MemberwiseClone();

        // Returns how many fields were replaced by their default.
        public int Sanitize()
        {
            int fixedFields = 0;
            version = CurrentVersion;
            masterVolume = Percent(masterVolume, DefaultVolume, ref fixedFields);
            musicVolume = Percent(musicVolume, DefaultVolume, ref fixedFields);
            sfxVolume = Percent(sfxVolume, DefaultVolume, ref fixedFields);
            shake = Percent(shake, DefaultIntensity, ref fixedFields);
            distortion = Percent(distortion, DefaultIntensity, ref fixedFields);
            flash = Percent(flash, DefaultIntensity, ref fixedFields);
            if (screenMode < -1 || screenMode > 1) { screenMode = (int)ScreenModeSetting.ProjectDefault; fixedFields++; }
            bool widthOk = resolutionWidth == 0 || (resolutionWidth >= 320 && resolutionWidth <= 16384);
            bool heightOk = resolutionHeight == 0 || (resolutionHeight >= 200 && resolutionHeight <= 16384);
            if (!widthOk || !heightOk || (resolutionWidth == 0) != (resolutionHeight == 0))
            {
                resolutionWidth = resolutionHeight = 0;
                fixedFields++;
            }
            if (bindingOverrides == null) { bindingOverrides = ""; fixedFields++; }
            return fixedFields;
        }

        private static int Percent(int value, int fallback, ref int fixedFields)
        {
            if (value >= 0 && value <= 100) return value;
            fixedFields++;
            return fallback;
        }

        public void ResetEnvironment()
        {
            masterVolume = musicVolume = sfxVolume = DefaultVolume;
            screenMode = (int)ScreenModeSetting.ProjectDefault;
            resolutionWidth = resolutionHeight = 0;
        }

        public void ResetPresentation() => shake = distortion = flash = DefaultIntensity;
        public void ResetBindings() => bindingOverrides = "";

        public string ToJson() => JsonUtility.ToJson(this, true);

        // A broken or missing record gives defaults; a readable one keeps every valid field.
        public static SettingsData FromJson(string json, out int fixedFields)
        {
            fixedFields = 0;
            SettingsData data;
            try { data = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<SettingsData>(json); }
            catch (ArgumentException) { data = null; }
            if (data == null) { fixedFields = -1; return new SettingsData(); }
            fixedFields = data.Sanitize();
            return data;
        }
    }
}
