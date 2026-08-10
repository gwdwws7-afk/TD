#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace TD.Editor
{
    /// <summary>
    /// Helper to create the EmberlineMixer.mixer asset.
    /// Unity does not expose a public API to create AudioMixer assets
    /// programmatically, so this menu item opens the Audio Mixer window
    /// with instructions.
    ///
    /// Manual steps (one-time setup):
    /// 1. Window > Audio > Audio Mixer
    /// 2. Click "+" to create a mixer named "EmberlineMixer"
    /// 3. Save it to Assets/Resources/Audio/EmberlineMixer.mixer
    /// 4. Create groups: Master (auto), Music, SFX, Ambience under Master
    /// 5. Under SFX, create: Routine, Tactical, Critical
    /// 6. Create snapshots: Normal (default), Resonance, Victory, Defeat
    ///
    /// If the mixer is missing at runtime, TDGameManager falls back to
    /// per- AudioSource.volume control (no ducking, no bus routing).
    /// </summary>
    public static class TDAudioMixerGenerator
    {
        [MenuItem("TD/Open Audio Mixer Setup Guide")]
        public static void ShowGuide()
        {
            const string guide = @"
EmberlineMixer Setup Guide
==========================

1. Open: Window > Audio > Audio Mixer
2. Click '+' to create mixer: 'EmberlineMixer'
3. Save to: Assets/Resources/Audio/EmberlineMixer.mixer

Groups to create (under Master):
  Master (auto-created)
  ├── Music
  ├── SFX
  │   ├── Routine
  │   ├── Tactical
  │   └── Critical
  └── Ambience

Snapshots to create:
  Normal    (default levels)
  Resonance (Music -2dB, SFX +1dB)
  Victory   (Music focus)
  Defeat    (Music focus)

TDGameManager will auto-detect and route all 5 AudioSources
to these groups. If missing, falls back to volume-only control.
";
            Debug.Log(guide);
            EditorUtility.DisplayDialog("EmberlineMixer Setup", guide, "OK");
            EditorApplication.ExecuteMenuItem("Window/Audio/Audio Mixer");
        }
    }
}
#endif
