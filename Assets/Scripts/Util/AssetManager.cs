using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class AssetManager : MonoBehaviour {

    private int expectedNumSprites;
    private int expectedNumAudioClips;
    private ConcurrentDictionary<string, Sprite> spritesMidDownload;
    private ConcurrentDictionary<string, AudioClip> audioClipsMidDownload;

    // The assets we have already downloaded and are storing in memory until the app
    // is shut down.
    private Dictionary<string, bool> downloadedStories; // Names of stories we have downloaded completely.
    private Dictionary<string, Sprite> storySprites;
    private Dictionary<string, AudioClip> storyAudioClips;

    // Use this for initialization
    void Start() {
        this.spritesMidDownload = new ConcurrentDictionary<string, Sprite>();
        this.audioClipsMidDownload = new ConcurrentDictionary<string, AudioClip>();

        this.downloadedStories = new Dictionary<string, bool>();
        this.storySprites = new Dictionary<string, Sprite>();
        this.storyAudioClips = new Dictionary<string, AudioClip>();
    }

    // Update is called once per frame
    void Update() {

    }

    // Called when another part of the app wants to load a sprite.
    public Sprite GetSprite(string imageFile) {
        if (Constants.LOAD_ASSETS_LOCALLY) {
            return Util.GetStorySprite(imageFile);
        } else {
            // Make sure the sprite is there.
            if (!this.storySprites.ContainsKey(imageFile)) {
                Logger.Log("no sprite found " + imageFile);
                Logger.Log(this.storySprites.Count);
                return null;
            } else {
                Logger.Log("found this sprite!!!");
                return this.storySprites[imageFile];   
            }
        }
    }

    // Called when another part of the app wants to load an audio clip.
    public AudioClip GetAudioClip(string audioFile) {
        if (Constants.LOAD_ASSETS_LOCALLY) {
            string storyName = Util.FileNameToStoryName(audioFile);
            return Resources.Load("StoryAudio/" + storyName + "/" + audioFile) as AudioClip;
        } else {
            // Make sure that the audio is actually there.
            if (!this.storyAudioClips.ContainsKey(audioFile)) {
                Logger.Log("no audio file found " + audioFile);
                Logger.Log(this.storyAudioClips.Count);
                return null;
            } else {
                return this.storyAudioClips[audioFile];
            }
        }
    }

    // Called by GameController to check if a story has already been downloaded.
    public bool StoryHasBeenDownloaded(string storyName) {
        return this.downloadedStories.ContainsKey(storyName);
    }

    // Called on app startup to download the title pages so we can show them in
    // the dropdown before the user actually selects a story and download all assets.
    public IEnumerator DownloadTitlePages(List<string> storyNames,
                                          Action<Dictionary<string, Sprite>,
                                          Dictionary<string, AudioClip>> onDownloadComplete) {
        // Duplication of clearing, but just to be safe.
        this.spritesMidDownload.Clear();
        this.audioClipsMidDownload.Clear();

        this.expectedNumSprites = storyNames.Count;
        this.expectedNumAudioClips = 0;

        Logger.Log("DownloadTitlePages");

        foreach (string storyName in storyNames) {
            // Brittle, but our files in the cloud all have this format, ok for now.
            string imageFile = storyName + "_01";
            StartCoroutine(downloadImage(storyName, imageFile, onDownloadComplete, false));
        }
        yield return null;
    }

    // Called to download the images and audio files needed for a particular story.
    // The arguments are lists of strings that should be the identifying part of the URL to
    // download from, and those strings will be used as keys into StoryManager's dictionaries
    // so that the downloaded image sprites and audio clips can be located later.
    public IEnumerator DownloadStoryAssets(string storyName, List<string> imageFileNames,
                                           List<string> audioFileNames,
                                           Action<Dictionary<string, Sprite>,
                                           Dictionary<string, AudioClip>> onDownloadComplete) {

        // Don't redo downloads, this is an extra check, caller should have already verified this.
        if (this.downloadedStories.ContainsKey(storyName)) {
            yield return null;
        } else {
            // Duplication of clearing, but just to be safe.
            this.spritesMidDownload.Clear();
            this.audioClipsMidDownload.Clear();

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
    }

    private IEnumerator downloadImage(string storyName, string imageFile,
                                      Action<Dictionary<string, Sprite>,
                                      Dictionary<string, AudioClip>> onDownloadComplete,
                                      bool wholeStory=true) {
        string url = Constants.IMAGE_BASE_URL + storyName + "/" + imageFile + ".png?raw=1";
        // Using yield return for the new www object will wait until the download is complete
        // but without blocking the rest of the game.
        WWW www = new WWW(url);
        yield return www;
        Sprite sprite = Sprite.Create(www.texture,
                                      new Rect(0, 0, www.texture.width, www.texture.height),
                                      new Vector2(0, 0));
        this.spritesMidDownload[imageFile] = sprite;
        this.storySprites[imageFile] = sprite;
        Logger.Log("completed download of image " + imageFile);
        this.checkEntireDownloadComplete(onDownloadComplete, storyName, wholeStory);
    }

    private IEnumerator downloadAudio(string storyName, string audioFile,
                                      Action<Dictionary<string, Sprite>,
                                      Dictionary<string, AudioClip>> onDownloadComplete,
                                      bool wholeStory=true) {
        string url = "https://s3.amazonaws.com/storycorpus-audio/" + storyName + "/" + audioFile + ".wav";
        WWW www = new WWW(url);
        yield return www;
        AudioClip audioClip = www.GetAudioClip();
        this.audioClipsMidDownload[audioFile] = audioClip;
        this.storyAudioClips[audioFile] = audioClip;
        Logger.Log("completed downloaded of audio " + audioFile);
        this.checkEntireDownloadComplete(onDownloadComplete, storyName, wholeStory);
    }

    // If all files have been downloaded, call the onDownloadComplete callback with the newly
    // downloaded assets, and then clear them out of our local concurrent dictionaries.
    public void checkEntireDownloadComplete(Action<Dictionary<string, Sprite>,
                                            Dictionary<string, AudioClip>> onDownloadComplete,
                                            string storyName=null, bool wholeStory=true) {
        if (this.spritesMidDownload.Count == this.expectedNumSprites &&
            this.audioClipsMidDownload.Count == this.expectedNumAudioClips)
        {
            Dictionary<string, Sprite> nonConcurrentSprites =
                new Dictionary<string, Sprite>(this.spritesMidDownload);
            Dictionary<string, AudioClip> nonConcurrentAudioClips =
                new Dictionary<string, AudioClip>(this.audioClipsMidDownload);

            if (wholeStory) {
                if (storyName == null) {
                    Logger.LogError("storyName is null when trying to save download status");
                }
                this.downloadedStories[storyName] = true;
            }
            onDownloadComplete(nonConcurrentSprites, nonConcurrentAudioClips);

            this.spritesMidDownload.Clear();
            this.audioClipsMidDownload.Clear();
        }
    }
}
