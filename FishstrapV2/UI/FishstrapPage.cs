using System.Windows.Controls;

namespace FishstrapV2.UI;

/// <summary>Base class for all settings pages.</summary>
public class FishstrapPage : UserControl
{
    /// <summary>Called when the page becomes the active page.</summary>
    public virtual void OnShown()
    {
    }

    /// <summary>Persists pending settings changes (respects test mode).</summary>
    protected void Persist() => Core.SettingsStore.AutoPersist();
}
