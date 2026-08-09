"""Generate Unity .meta files for all audio assets under Assets/Resources/Audio.

Creates:
  - folder .meta for every directory (DefaultImporter, folderAsset: yes)
  - file .meta for every .wav (AudioImporter, Unity 2022.3 defaults)

GUIDs are deterministic (sha1 of the repo-relative asset path truncated to
32 hex chars) so re-running is idempotent and the committed GUIDs stay stable.
"""
import hashlib
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AUDIO_ROOT = os.path.join(ROOT, "Assets", "Resources", "Audio")

if not os.path.isdir(AUDIO_ROOT):
    print("ERROR: %s not found" % AUDIO_ROOT)
    sys.exit(1)


def guid_for(rel_path):
    """Deterministic 32-hex GUID from the asset path (forward slashes)."""
    canonical = rel_path.replace("\\", "/")
    h = hashlib.sha1(canonical.encode("utf-8")).hexdigest()
    return h[:32]


def folder_meta(rel_dir):
    g = guid_for(rel_dir)
    return (
        "fileFormatVersion: 2\n"
        "guid: %s\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n" % g
    )


def wav_meta(rel_file):
    g = guid_for(rel_file)
    return (
        "fileFormatVersion: 2\n"
        "guid: %s\n"
        "AudioImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 7\n"
        "  defaultSettings:\n"
        "    serializedVersion: 2\n"
        "    loadType: 0\n"
        "    sampleRateSetting: 0\n"
        "    sampleRateOverride: 44100\n"
        "    compressionFormat: 1\n"
        "    quality: 0.7\n"
        "    conversionMode: 0\n"
        "    preloadAudioData: 1\n"
        "  platformSettingOverrides: {}\n"
        "  forceToMono: 0\n"
        "  normalize: 1\n"
        "  loadInBackground: 0\n"
        "  ambisonic: 0\n"
        "  3D: 1\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n" % g
    )


def main():
    dirs_made = files_made = dirs_skipped = files_skipped = 0

    for dirpath, dirnames, filenames in os.walk(AUDIO_ROOT):
        # Directory meta
        rel_dir = os.path.relpath(dirpath, ROOT)
        meta_path = dirpath + ".meta"
        content = folder_meta(rel_dir)
        if os.path.exists(meta_path):
            dirs_skipped += 1
        else:
            with open(meta_path, "w", newline="\n") as f:
                f.write(content)
            dirs_made += 1

        # File metas
        for name in filenames:
            if not name.endswith(".wav"):
                continue
            wav_path = os.path.join(dirpath, name)
            rel_file = os.path.relpath(wav_path, ROOT)
            wav_meta_path = wav_path + ".meta"
            content = wav_meta(rel_file)
            if os.path.exists(wav_meta_path):
                files_skipped += 1
            else:
                with open(wav_meta_path, "w", newline="\n") as f:
                    f.write(content)
                files_made += 1

    print("Created %d folder metas (%d skipped)" % (dirs_made, dirs_skipped))
    print("Created %d wav metas (%d skipped)" % (files_made, files_skipped))


if __name__ == "__main__":
    main()
