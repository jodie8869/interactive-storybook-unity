using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class AssetDownloader : MonoBehaviour {

    private int expectedNumSprites;
    private int expectedNumAudioClips;
    private ConcurrentDictionary<string, Sprite> sprites;
    private ConcurrentDictionary<string, AudioClip> audioClips;

    // Use this for initialization
    void Start() {
        this.sprites = new ConcurrentDictionary<string, Sprite>();
        this.audioClips = new ConcurrentDictionary<string, AudioClip>();
    }

    // Update is called once per frame
    void Update() {

    }

    // Called to download the images and audio files needed for a particular story.
    // The arguments are lists of strings that should be the identifying part of the URL to
    // download from, and those strings will be used as keys into StoryManager's dictionaries
    // so that the downloaded image sprites and audio clips can be located later.
    public IEnumerator DownloadStoryAssets(string storyName, List<string> imageFileNames,
                                           List<string> audioFileNames,
                                           Action<Dictionary<string, Sprite>,
                                           Dictionary<string, AudioClip>> onDownloadComplete) {

        // Duplication of clearing, but just to be safe.
        this.sprites.Clear();
        this.audioClips.Clear();

        this.expectedNumSprites = imageFileNames.Count;
        this.expectedNumAudioClips = audioFileNames.Count;

        foreach (string iFile in imageFileNames) {
            StartCoroutine(downloadImage(storyName, iFile, onDownloadComplete));
        }
        foreach (string aFile in audioFileNames) {
            StartCoroutine(downloadAudio(storyName, aFile, onDownloadComplete));
        }
        yield return null;
    }

    private IEnumerator downloadImage(string storyName, string imageFile,
                                      Action<Dictionary<string, Sprite>,
                                      Dictionary<string, AudioClip>> onDownloadComplete) {
        string url = Constants.IMAGE_BASE_URL + storyName + "/" + imageFile + ".png?raw=1";
        // Using yield return for the new www object will wait until the download is complete
        // but without blocking the rest of the game.
        WWW www = new WWW(url);
        yield return www;
        Sprite sprite = Sprite.Create(www.texture,
                                      new Rect(0, 0, www.texture.width, www.texture.height),
                                      new Vector2(0, 0));
        this.sprites[imageFile] = sprite;
        Logger.Log("completed download of image " + imageFile);
        this.checkEntireDownloadComplete(onDownloadComplete);
    }

    private IEnumerator downloadAudio(string storyName, string audioFile,
                                      Action<Dictionary<string, Sprite>,
                                      Dictionary<string, AudioClip>> onDownloadComplete) {
        string url = "https://s3.amazonaws.com/storycorpus-audio/" + storyName + "/" + audioFile + ".wav";
        WWW www = new WWW(url);
        yield return www;
        AudioClip audioClip = www.GetAudioClip();
        this.audioClips[audioFile] = audioClip;
        Logger.Log("completed downloaded of audio " + audioFile);
        this.checkEntireDownloadComplete(onDownloadComplete);
    }

    // If all files have been downloaded, call the onDownloadComplete callback with the newly
    // downloaded assets, and then clear them out of our local concurrent dictionaries.
    public void checkEntireDownloadComplete(Action<Dictionary<string, Sprite>,
                                            Dictionary<string, AudioClip>> onDownloadComplete) {
        if (this.sprites.Count == this.expectedNumSprites &&
            this.audioClips.Count == this.expectedNumAudioClips)
        {
            Dictionary<string, Sprite> nonConcurrentSprites =
                new Dictionary<string, Sprite>(this.sprites);
            Dictionary<string, AudioClip> nonConcurrentAudioClips =
                new Dictionary<string, AudioClip>(this.audioClips);

            onDownloadComplete(nonConcurrentSprites, nonConcurrentAudioClips);

            this.sprites.Clear();
            this.audioClips.Clear();
        }
    }
}
