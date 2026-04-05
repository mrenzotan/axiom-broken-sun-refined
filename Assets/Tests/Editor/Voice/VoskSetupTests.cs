using NUnit.Framework;
using System.IO;
using UnityEngine;

public class VoskSetupTests
{
    [Test]
    public void ModelDirectory_ExistsInStreamingAssets()
    {
        string modelPath = Path.Combine(
            Application.streamingAssetsPath,
            "VoskModels",
            "vosk-model-en-us-0.22-lgraph");

        Assert.IsTrue(
            Directory.Exists(modelPath),
            $"Vosk model not found at: {modelPath}\n" +
            "Download vosk-model-en-us-0.22-lgraph from https://alphacephei.com/vosk/models " +
            "and place it under Assets/StreamingAssets/VoskModels/");
    }
}
