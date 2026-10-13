using UnityEditor;
using UnityEngine;

/// <summary>Small effects stay decoded; music streams instead of allocating full PCM tracks.</summary>
public sealed class ZombieAudioImporter : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/GameMain/Sounds/ZombieSurvivor/")) return;
        bool music = assetPath.Contains("/Music/");
        var importer = (AudioImporter)assetImporter;
        importer.forceToMono = !music;
        importer.loadInBackground = music;
        var settings = importer.defaultSampleSettings;
        settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
        settings.quality = .75f;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
        var serialized = new SerializedObject(importer);
        var preload = serialized.FindProperty("m_PreloadAudioData");
        if (preload != null) preload.boolValue = !music;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
