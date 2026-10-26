using Digi21.WinUI.Docking.Interaction;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;

namespace Digi21.WinUI.Docking.Primitives;

// The attention cue of one tab, kept apart from the rest of the tab's state so the three kinds of
// tab agree on it, and so an attention change does not replay everything else a tab does when its
// window changes, such as announcing a selection to UI Automation.
internal sealed class TabAttention
{
    // How often a lit tab looks at the settings that decide between pulsing and holding still.
    //
    // Asked rather than told: in a desktop application UISettings.AnimationsEnabledChanged is never
    // raised, not even when the setting is changed the way the Settings app changes it, and
    // AccessibilitySettings.HighContrastChanged cannot even be subscribed to (ERROR_NOT_FOUND). Only
    // a tab that is showing the cue asks, so a layout with nothing to point at costs nothing.
    private static readonly TimeSpan SettingsCheckInterval = TimeSpan.FromSeconds(1);

    private static readonly AccessibilitySettings Accessibility = new();

    private readonly Control tab;
    private DockingWindow? window;
    private long token = -1;
    private string? state;
    private string? itemStatus;
    private DispatcherQueueTimer? settingsCheck;

    internal TabAttention(Control tab)
    {
        this.tab = tab;
    }

    // Follows a new window, or none.
    internal void Observe(DockingWindow? next)
    {
        window?.UnregisterPropertyChangedCallback(DockingWindow.IsAttentionRequestedProperty, token);
        window = next;

        if (window is not null)
        {
            token = window.RegisterPropertyChangedCallback(DockingWindow.IsAttentionRequestedProperty, (_, _) => Apply());
        }

        Apply();
    }

    // Brings the tab in line with its window's request. Called again once the template is applied,
    // since going to a state before there is a template does nothing.
    internal void Apply()
    {
        var requested = window?.IsAttentionRequested == true;

        state = requested ? RequestedState() : "NoAttention";
        VisualStateManager.GoToState(tab, state, true);
        SetItemStatus(requested ? DockingThemeResources.Value("DockingAttentionStatusText", "New content") : null);
        WatchSettings(requested);
    }

    // A pulse is motion, so it stops when Windows says animations are off. In high contrast the cue
    // is an outline in the system highlight color, which holds still as well.
    private static string RequestedState() =>
        ScreenInterop.SystemAnimationsEnabled && !Accessibility.HighContrast ? "AttentionPulsing" : "AttentionSteady";

    private void WatchSettings(bool watch)
    {
        if (!watch)
        {
            settingsCheck?.Stop();
            return;
        }

        if (settingsCheck is null)
        {
            if (tab.DispatcherQueue is not { } queue)
            {
                return;
            }

            settingsCheck = queue.CreateTimer();
            settingsCheck.Interval = SettingsCheckInterval;
            settingsCheck.Tick += (_, _) =>
            {
                if (state != RequestedState())
                {
                    Apply();
                }
            };
        }

        if (!settingsCheck.IsRunning)
        {
            settingsCheck.Start();
        }
    }

    // Color says nothing to a screen reader: the tab says it in words as well, and tells a listening
    // client the words changed, since setting the property raises nothing by itself. Clearing rather
    // than writing an empty string leaves any status the application gave the tab in place.
    private void SetItemStatus(string? status)
    {
        if (status == itemStatus)
        {
            return;
        }

        var old = itemStatus;
        itemStatus = status;

        if (status is null)
        {
            tab.ClearValue(AutomationProperties.ItemStatusProperty);
        }
        else
        {
            AutomationProperties.SetItemStatus(tab, status);
        }

        if (AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged)
            && FrameworkElementAutomationPeer.FromElement(tab) is { } peer)
        {
            peer.RaisePropertyChangedEvent(
                AutomationElementIdentifiers.ItemStatusProperty,
                old ?? string.Empty,
                AutomationProperties.GetItemStatus(tab));
        }
    }
}
