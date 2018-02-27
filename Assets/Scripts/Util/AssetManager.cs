using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Amazon.CognitoIdentity;

public class AssetManager : MonoBehaviour {

    private int expectedNumSprites;
    private int expectedNumAudioClips;
    private int expectedNumJsons;
    private ConcurrentDictionary<string, Sprite> spritesMidDownload;
    private ConcurrentDictionary<string, AudioClip> audioClipsMidDownload;
    private ConcurrentDictionary<string, StoryJson> jsonMidDownload;

    // The assets we have already downloaded and are storing in memory until the app
    // is shut down.
    private Dictionary<string, bool> downloadedStories; // Names of stories we have downloaded assets for (sprites + audio).
    private Dictionary<string, Sprite> storySprites;
    private Dictionary<string, AudioClip> storyAudioClips;
    private Dictionary<string, List<StoryJson>> storyJsons;

    private AmazonS3Client s3Client;
    private AWSCredentials awsCredentials;

    void Start() {
        Logger.Log("Starting Amazon S3 testing...");
        AWSConfigs.HttpClient = AWSConfigs.HttpClientOption.UnityWebRequest;

        this.awsCredentials = new CognitoAWSCredentials(
            Constants.IDENTITY_POOL_ID,
            RegionEndpoint.GetBySystemName(Constants.COGNITO_IDENTITY_REGION)
        );
        this.s3Client = new AmazonS3Client(this.awsCredentials, Amazon.RegionEndpoint.USEast1);

        GetObjectRequest request = new GetObjectRequest{
            BucketName = "storycorpus-interactive-storybook-json",
            Key = "the_hungry_toad/the_hungry_toad_01.json"
        };
        Logger.Log("here");
        this.s3Client.GetObjectAsync("storybook-collected-child-audio",
                                     "test/test.json",
                                     (response) =>
                                     {
                                         Logger.Log("got a response...");
                                         using (Stream responseStream = response.Response.ResponseStream)
                                         using (StreamReader reader = new StreamReader(responseStream))
                                         {
                                             string responseBody = reader.ReadToEnd();
                                             Logger.Log(responseBody);
                                         }
                                     });

        this.spritesMidDownload = new ConcurrentDictionary<string, Sprite>();
        this.audioClipsMidDownload = new ConcurrentDictionary<string, AudioClip>();
        this.jsonMidDownload = new ConcurrentDictionary<string, StoryJson>();

        this.downloadedStories = new Dictionary<string, bool>();
        this.storySprites = new Dictionary<string, Sprite>();
        this.storyAudioClips = new Dictionary<string, AudioClip>();
        this.storyJsons = new Dictionary<string, List<StoryJson>>();
    }

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

    // Called when GameController wants to get the json files for a story.
    public List<StoryJson> GetStoryJson(StoryMetadata story) {
        string storyName = story.GetName();
        if (Constants.LOAD_ASSETS_LOCALLY) {
            TextAsset[] textAssets = Resources.LoadAll<TextAsset>("SceneDescriptions/" + storyName);
            List<StoryJson> jsons = new List<StoryJson>();
            foreach (TextAsset t in textAssets) {
                jsons.Add(new StoryJson(t.name, t.text));
            }
            return jsons;
        } else {
            if (!this.storyJsons.ContainsKey(storyName)) {
                Logger.Log("No json files found for " + storyName);
                return null;
            } else {
                return this.storyJsons[storyName];
            }
        }
    }

    // Called by GameController to check if the json of a story has already been downloaded.
    public bool JsonHasBeenDownloaded(string storyName) {
        return this.storyJsons.ContainsKey(storyName);
    }

    // Called by GameController to check if a story's assets has already been downloaded.
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

    // Called to download story json files.
    public IEnumerator DownloadStoryJson(StoryMetadata story,
                                         Action<Dictionary<string, StoryJson>> onDownloadComplete) {
        yield return null;
        string storyName = story.GetName();
        int numPages = story.GetNumPages();
        if (this.storyJsons.ContainsKey(storyName)) {
            Logger.Log("Not downloading json files, already have them: " + storyName);
        } else {
            Logger.Log("Downloading json files for " + storyName);
            this.jsonMidDownload.Clear();
            this.expectedNumJsons = numPages;

            for (int i = 0; i < numPages; i ++) {
                string jFile = storyName + "_" + Util.TwoDigitStringFromInt(i + 1);
                StartCoroutine(downloadJson(storyName, jFile, onDownloadComplete));
            }
        }
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
            Logger.Log("Not downloading story assets, already have them: " + storyName);
            yield return null;
        } else {
            // Duplication of clearing, but just to be safe.
            Logger.Log("Downloading story assets: " + storyName);
            this.spritesMidDownload.Clear();
            this.audioClipsMidDownload.Clear();

            this.expectedNumSprites = imageFileNames.Count;
            this.expectedNumAudioClips = audioFileNames.Count;

            foreach (string iFile in imageFileNames) {
                Logger.Log(iFile);
                StartCoroutine(downloadImage(storyName, iFile, onDownloadComplete));
            }
            foreach (string aFile in audioFileNames) {
                Logger.Log(aFile);
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
        //Logger.Log("started download of image " + imageFile);
        WWW www = new WWW(url);
        yield return www;
        Sprite sprite = Sprite.Create(www.texture,
                                      new Rect(0, 0, www.texture.width, www.texture.height),
                                      new Vector2(0, 0));
        this.spritesMidDownload[imageFile] = sprite;
        this.storySprites[imageFile] = sprite;
        //Logger.Log("completed download of image " + imageFile);
        this.checkStoryAssetDownloadComplete(onDownloadComplete, storyName, wholeStory);
    }

    private IEnumerator downloadAudio(string storyName, string audioFile,
                                      Action<Dictionary<string, Sprite>,
                                      Dictionary<string, AudioClip>> onDownloadComplete,
                                      bool wholeStory=true) {
        string url = Constants.AUDIO_BASE_URL + storyName + "/" + audioFile + ".wav";
        //Logger.Log("started downloaded of audio " + audioFile);
        WWW www = new WWW(url);
        yield return www;
        AudioClip audioClip = www.GetAudioClip();
        this.audioClipsMidDownload[audioFile] = audioClip;
        this.storyAudioClips[audioFile] = audioClip;
        //Logger.Log("completed downloaded of audio " + audioFile);
        this.checkStoryAssetDownloadComplete(onDownloadComplete, storyName, wholeStory);
    }

    private IEnumerator downloadJson(string storyName, string jsonFile,
                                     Action<Dictionary<string, StoryJson>> onDownloadComplete) {
        string url = Constants.JSON_BASE_URL + storyName + '/' + jsonFile + ".json";
        //Logger.Log("started download of json " + url);
        WWW www = new WWW(url);
        yield return www;
        StoryJson json = new StoryJson(jsonFile, www.text);
        Logger.Log(jsonFile);
        Logger.Log(www.text);
        this.jsonMidDownload[jsonFile] = json;
        Logger.Log("completed download of json " + url);
        this.checkJsonDownloadComplete(storyName, onDownloadComplete);
    }

    // If all files have been downloaded, call the onDownloadComplete callback with the newly
    // downloaded assets, and then clear them out of our local concurrent dictionaries.
    public void checkStoryAssetDownloadComplete(Action<Dictionary<string, Sprite>,
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

    // If all json files have been downloaded, then call the callback and make sure to save the
    // downloaded files in this.storyJsons.
    public void checkJsonDownloadComplete(string storyName,
                                          Action<Dictionary<string, StoryJson>> onDownloadComplete) {
        if (this.jsonMidDownload.Count == this.expectedNumJsons) {
            // Save them all.
            this.storyJsons[storyName] = new List<StoryJson>();
            foreach (KeyValuePair<string, StoryJson> j in this.jsonMidDownload) {
                this.storyJsons[storyName].Add(j.Value);
            }

            Dictionary<string, StoryJson> nonConcurrentJsons =
                new Dictionary<string, StoryJson>(this.jsonMidDownload);
            onDownloadComplete(nonConcurrentJsons);

            this.jsonMidDownload.Clear();

        }
    }
}
