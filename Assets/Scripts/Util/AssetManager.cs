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
    private int expectedNumStoryMetadatas;
    private ConcurrentDictionary<string, Sprite> spritesMidDownload;
    private ConcurrentDictionary<string, AudioClip> audioClipsMidDownload;
    private ConcurrentDictionary<string, StoryJson> jsonMidDownload;
    private ConcurrentDictionary<string, StoryMetadata> storyMetadataMidDownload;


    // The assets we have already downloaded and are storing in memory until the app
    // is shut down.
    private Dictionary<string, bool> localStories; // Keep track of stories we know are local.

    private Dictionary<string, bool> downloadedStories; // Names of stories we have downloaded assets for (sprites + audio).
    private Dictionary<string, Sprite> storySprites;
    private Dictionary<string, AudioClip> storyAudioClips;
    private Dictionary<string, List<StoryJson>> storyJsons;
    private Dictionary<string, StoryMetadata> storyMetadatas;

    private AmazonS3Client s3Client;
    private AWSCredentials awsCredentials;

    void Awake() {
        this.spritesMidDownload = new ConcurrentDictionary<string, Sprite>();
        this.audioClipsMidDownload = new ConcurrentDictionary<string, AudioClip>();
        this.jsonMidDownload = new ConcurrentDictionary<string, StoryJson>();
        this.storyMetadataMidDownload = new ConcurrentDictionary<string, StoryMetadata>();

        this.localStories = new Dictionary<string, bool>();
        this.downloadedStories = new Dictionary<string, bool>();
        this.storySprites = new Dictionary<string, Sprite>();
        this.storyAudioClips = new Dictionary<string, AudioClip>();
        this.storyJsons = new Dictionary<string, List<StoryJson>>();
        this.storyMetadatas = new Dictionary<string, StoryMetadata>();
    }

    void Start() {
        // Set up S3 credentials and client.
        AWSConfigs.HttpClient = AWSConfigs.HttpClientOption.UnityWebRequest;
        this.awsCredentials = new CognitoAWSCredentials(
            Constants.IDENTITY_POOL_ID,
            RegionEndpoint.GetBySystemName(Constants.COGNITO_IDENTITY_REGION)
        );
        this.s3Client = new AmazonS3Client(this.awsCredentials, RegionEndpoint.USEast1);
    }

    void Update() {

    }

    // Test downloading a json file from S3.
    // TODO: think about using S3 API for all of the downloading, right now it's straight WWW.
    //
    // Note that this happens asynchronously (caller doesn't block) even though Unity says it
    // happens synchronously, and also that the errors in Visual Studio are actually ok in Unity.
    public void S3GetStoryJson (string jsonFileName, Action<StoryJson> callback = null) {
        string storyName = Util.FileNameToStoryName(jsonFileName);
        this.s3Client.GetObjectAsync(
            Constants.S3_JSON_BUCKET, storyName + "/" + jsonFileName + ".json",
            (responseObj) => {
                 using (Stream responseStream = responseObj.Response.ResponseStream)
                 using (StreamReader reader = new StreamReader(responseStream))
                 {
                     string responseBody = reader.ReadToEnd();
                     Logger.Log(responseBody);
                     StoryJson json = new StoryJson(jsonFileName, responseBody);
                     callback?.Invoke(json);
                 }
             });
    }

    // Upload an audio file to the collected child audio bucket in S3.
    // Argument audioPath should be the same as what was passed into SaveAudioAtPath in AudioRecorder.
    public void S3UploadChildAudio(string audioPath) {
        // Use a prefix that includes story, page number, first 2 words of stanza, and date.
        string s3Path = DateTime.Now.ToString("yyyy-MM-dd") + "/" + Constants.PARTICIPANT_ID + "/" +
            StorybookStateManager.GetState().currentStory + "/" + 
            StorybookStateManager.GetState().storybookMode + "/" +
            DateTime.Now.ToString("HH:mm:ss") + "_" + audioPath;
        PutObjectRequest request = new PutObjectRequest {
            BucketName = Constants.S3_CHILD_AUDIO_BUCKET,
            Key = s3Path,
            FilePath = Application.persistentDataPath + "/" + audioPath
        };
        this.s3Client.PutObjectAsync(request, (responseObj) => {
            if (responseObj.Exception == null) {
                Logger.Log("Successful upload " + s3Path);
            } else {
                Logger.Log("Upload failed");
            }
        });
    }

    // Let the asset manager know about story metadatas that we already have.
    // Basically just used for initialization, with the stories that GameController
    // is initialized with.
    public void AddStoryMetadatas(List<StoryMetadata> metadatas) {
        foreach (StoryMetadata m in metadatas) {
            this.storyMetadatas.Add(m.GetName(), m);
        }
    }

    // Use StoryMetadata to get the existing storybooks that we haven't already downloaded.
    public void GetNewStoryMetadatas(Action<List<StoryMetadata>> onDownloadComplete = null) {
        ListObjectsRequest request = new ListObjectsRequest {
            BucketName = Constants.S3_STORY_METADATA_BUCKET
        };
        this.s3Client.ListObjectsAsync(request, (responseObject) =>  {
            if (responseObject.Exception == null) {
                List<string> newStories = new List<string>();
                foreach (S3Object obj in responseObject.Response.S3Objects) {
                    if (obj.Key.EndsWith(".json")) {
                        string storyName = obj.Key.Substring(0, obj.Key.Length - ".json".Length);
                        if (!this.storyMetadatas.ContainsKey(storyName)) {
                            // TODO: remove this check, excluding the test story.
                            if (storyName != "my_dog") {
                                newStories.Add(storyName);
                            }
                        }
                    } else {
                        Logger.Log("Skipping non-json files: " + obj.Key);
                    }
                }
                // Download any new stories.
                if (newStories.Count > 0) {
                    this.expectedNumStoryMetadatas = newStories.Count;
                    foreach(string storyName in newStories) {
                        this.downloadOneStoryMetadata(storyName, onDownloadComplete); 
                    }
                } else {
                    // Directly invoke callback with an empty list.
                    onDownloadComplete?.Invoke(new List<StoryMetadata>());
                }
            } else {
                Logger.LogError("Couldn't get StoryMetadatas: " + responseObject.Exception.Data);
                throw new Exception("Couldn't get StoryMetadatas");
            }
        });
    }

    private void downloadOneStoryMetadata(string storyName, Action<List<StoryMetadata>> callback) {
        this.s3Client.GetObjectAsync(
            Constants.S3_STORY_METADATA_BUCKET, storyName + ".json",
            (responseObj) => {
                using (Stream responseStream = responseObj.Response.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string responseBody = reader.ReadToEnd();
                    // Parse the JSON into a StoryMetadata, add it to mid download dictionary.
                    // Logger.Log(responseBody);
                    this.storyMetadataMidDownload[storyName] = new StoryMetadata(responseBody);
                    this.checkStoryMetadataDownloadComplete(callback);

                }
            });
    }

    // Called after each async download finishes so that we know when the parallel download of
    // many assets is complete and we can invoke the given callback with the aggregated results.
    private void checkStoryMetadataDownloadComplete(Action<List<StoryMetadata>> callback = null) {
        if (this.expectedNumStoryMetadatas == this.storyMetadataMidDownload.Count) {
            // Return a copy of the data.
            List<StoryMetadata> metadatasToReturn = new List<StoryMetadata>();
            foreach(KeyValuePair<string, StoryMetadata> data in this.storyMetadataMidDownload) {
                this.storyMetadatas.Add(data.Key, data.Value);
                metadatasToReturn.Add(data.Value);
            }

            callback?.Invoke(metadatasToReturn);
            this.storyMetadataMidDownload.Clear();
        }
    }

//    // Commented out because this functionality is replaced by S3GetNewStoryMetadata().
//
//    // Check for all storybooks that exist. Gives callback a list of story names.
//    public void S3GetAvailableStoryNames(Action<List<string>> callback = null) {
//        List<string> storyNames = new List<string>();
//        ListObjectsRequest request = new ListObjectsRequest {
//            BucketName = Constants.S3_STORY_METADATA_BUCKET
//        };
//        this.s3Client.ListObjectsAsync(request, (responseObject) =>  {
//            if (responseObject.Exception == null) {
//                this.expectedNumStoryMetadatas = responseObject.Response.S3Objects.Count;
//                foreach (S3Object obj in responseObject.Response.S3Objects) {
//                    if (obj.Key.EndsWith(".json")) {
//                        storyNames.Add(obj.Key.Substring(0, obj.Key.Length - ".json".Length));
//                    } else {
//                        Logger.Log("Skipping non-json files: " + obj.Key);
//                    }
//                }
//            } else {
//                Logger.LogError("Couldn't get StoryMetadatas: " + responseObject.Exception.Data);
//                throw new Exception("Couldn't get StoryMetadatas");
//            }
//            callback.Invoke(storyNames);
//        });
//    }

    // Get the title sprite for the dropdown menu.
    public Sprite GetTitleSprite(StoryMetadata story) {
        return this.GetSprite(story.GetName() + "_01");
    }

    // Called when another part of the app wants to load a sprite.
    public Sprite GetSprite(string imageFile) {
        string storyName = Util.FileNameToStoryName(imageFile);
        if (this.storyExistsLocal(storyName)) {
            return Util.GetStorySprite(imageFile);
        } else {
            // Make sure the sprite is there.
            if (!this.storySprites.ContainsKey(imageFile)) {
                Logger.Log("no sprite found " + imageFile);
                Logger.Log(this.storySprites.Count);
                return null;
            } else {
                return this.storySprites[imageFile];   
            }
        }
    }

    // Called when another part of the app wants to load an audio clip.
    public AudioClip GetAudioClip(string audioFile) {
        string storyName = Util.FileNameToStoryName(audioFile);
        if (this.storyExistsLocal(storyName)) {
            return Resources.Load("StoryAudio/" + storyName + "/" + audioFile) as AudioClip;
        } else {
            // Make sure that the audio is actually there.
            if (!this.storyAudioClips.ContainsKey(audioFile)) {
                Logger.Log("No audio file found " + audioFile);
                Logger.Log(this.storyAudioClips.Count);
                return null;
            } else {
                Logger.Log("Found and returning audio file " + audioFile);
                return this.storyAudioClips[audioFile];
            }
        }
    }

    // Called when GameController wants to get the json files for a story.
    public List<StoryJson> GetStoryJson(StoryMetadata story) {
        string storyName = story.GetName();
        if (this.StoryExistsLocal(story)) {
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

    // Called by GameController to check if a story's assets exist in Resources folder locally.
    public bool StoryExistsLocal(StoryMetadata story) {
        return this.storyExistsLocal(story.GetName());
    }

    private bool storyExistsLocal(string storyName) {
        if (this.localStories.ContainsKey(storyName) && this.localStories[storyName] == true) {
            return true;
        }
        string path = "SceneDescriptions/" + storyName + "/" + storyName + "_01";
        TextAsset test = Resources.Load<TextAsset>(path);
        bool exists = test != null;
        if (exists) {
            this.localStories.Add(storyName, true);
        }
        Logger.Log("Story " + storyName + " exists: " + exists);
        return exists;
    }

    // Called by GameController to check if the json of a story has already been downloaded.
    public bool JsonHasBeenDownloaded(string storyName) {
        return this.storyJsons.ContainsKey(storyName);
    }

    // Called by GameController to check if a story's assets has already been downloaded.
    public bool StoryHasBeenDownloaded(string storyName) {
        return this.downloadedStories.ContainsKey(storyName);
    }

    // Called to check if a story's title page has already been downloaded.
    public bool ImageHasBeenDownloaded(string imageFileName) {
        return this.storySprites.ContainsKey(imageFileName);
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
        Logger.Log(expectedNumSprites);
        this.expectedNumAudioClips = 0;

        Logger.Log("DownloadTitlePages");

        foreach (string storyName in storyNames) {
            // Brittle, but our files in the cloud all have this format, ok for now.
            string imageFile = storyName + "_01";
            if (!this.ImageHasBeenDownloaded(imageFile)) {
                Logger.Log("Title page hasn't been downloaded yet " + imageFile);
                StartCoroutine(downloadImage(storyName, imageFile, onDownloadComplete, false));
            }
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
            Logger.Log("Not downloading story assets, already have them for story: " + storyName);
            yield return null;
        } else {
            // Duplication of clearing, but just to be safe.
            Logger.Log("Downloading story assets for story: " + storyName);
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
        // Logger.Log("started download of image " + imageFile);
        WWW www = new WWW(url);
        yield return www;
        Sprite sprite = Sprite.Create(www.texture,
                                      new Rect(0, 0, www.texture.width, www.texture.height),
                                      new Vector2(0, 0));
        this.spritesMidDownload[imageFile] = sprite;
        this.storySprites[imageFile] = sprite;
        // Logger.Log("completed download of image " + imageFile);
        this.checkStoryAssetDownloadComplete(onDownloadComplete, storyName, wholeStory);
    }

    private IEnumerator downloadAudio(string storyName, string audioFile,
                                      Action<Dictionary<string, Sprite>,
                                      Dictionary<string, AudioClip>> onDownloadComplete,
                                      bool wholeStory=true) {
        string url = Constants.AUDIO_BASE_URL + storyName + "/" + audioFile + ".wav";
//        Logger.Log("started downloaded of audio " + audioFile);
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
        this.jsonMidDownload[jsonFile] = json;
        // Logger.Log("completed download of json " + url);
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
