using Voyage.EarthquakeWarning.Settings;

namespace Voyage.EarthquakeWarning.Services;

public sealed class SettingsStore
{
    private readonly string _path;
    public SettingsStore()
    {
        _path = Path.Combine(Plugin.Current!.ConfigDirectory, "settings.json");
    }

    public void Save()
    {
        Plugin.Current!.Settings.Save(_path);
    }
}
