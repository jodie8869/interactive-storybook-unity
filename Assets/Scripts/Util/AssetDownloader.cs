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

    // Return true if the download has completed.
    public bool checkDownloadComplete() {
        // TODO: add audio clips count too
        return (this.sprites.Count == this.expectedNumSprites) ;
    }

    // Called to download the images and audio files needed for a particular story.
    // The arguments are lists of strings that should be the identifying part of the URL to
    // download from, and those strings will be used as keys into StoryManager's dictionaries
    // so that the downloaded image sprites and audio clips can be located later.
    public IEnumerator DownloadStoryAssets(string storyName, List<string> imageFileNames,
                                           List<string> audioFileNames,
                                           Action<Dictionary<string, Sprite>,
                                           Dictionary<string, AudioClip>> callback) {

        // Duplication of clearing, but just to be safe.
        this.sprites.Clear();
        this.audioClips.Clear();

        this.expectedNumSprites = imageFileNames.Count;
        this.expectedNumAudioClips = audioFileNames.Count;

        foreach (string iFile in imageFileNames) {
            StartCoroutine(downloadImage(storyName, iFile, callback));
        }
        //foreach (string aFile in audioFileNames) {
        //    StartCoroutine(downloadAudio(storyName, aFile));
        //}
        yield return null;
    }

    private IEnumerator downloadImage(string storyName, string imageFile,
                                      Action<Dictionary<string, Sprite>,
                                      Dictionary<string, AudioClip>> callback) {
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
        if (this.checkDownloadComplete()) {
            Dictionary<string, Sprite> nonConcurrentSprites =
                new Dictionary<string, Sprite>(this.sprites);
            Dictionary<string, AudioClip> nonConcurrentAudioClips =
                new Dictionary<string, AudioClip>(this.audioClips);

            callback(nonConcurrentSprites, nonConcurrentAudioClips);

            this.sprites.Clear();
            this.audioClips.Clear();
        }
    }

    private IEnumerator downloadAudio(string storyName, string audioFile,
                                      Action<Dictionary<string, Sprite>,
                                      Dictionary<string, AudioClip>> callback)
    {
        string url = "https://www.dropbox.com/work/Story%20Corpus/audios/contentroot/stories/" + audioFile + ".wav";
        WWW www = new WWW(url);
        yield return www;
        AudioClip audioClip = www.GetAudioClip();
        this.audioClips[audioFile] = audioClip;
    }
}
