// This file contains the main Game Controller class.
//
// GameController handles the logic for the initial connection to ROS,
// as well as other metadata about the storybook interaction. GameController
// does not have to communicate over Ros, change behavior by setting the value
// of Constants.USE_ROS.
//
// GameController controls the high level progression of the story, and tells
// StoryManager which scenes to load.
//
// GameController is a singleton.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.UI;

public class GameController : MonoBehaviour {

    private bool foo = false;
    private AudioClip recorded;

    // The singleton instance.
    public static GameController instance = null;

    // Task queue.
    private Queue<Action> taskQueue = new Queue<Action>();

    // UI GameObjects. Make public so that they can be attached in Unity.
    public Button landscapeNextButton;
    public Button landscapeBackButton;
    public Button landscapeFinishButton;
    public Button portraitNextButton;
    public Button portraitBackButton;
    public Button portraitFinishButton;

    public Button landscapeToggleAudioButton;
    public Button portraitToggleAudioButton;

    private Button nextButton;
    private Button backButton;
    private Button finishButton;
    private Button toggleAudioButton;

    public Button startStoryButton;
    public GameObject loadingBar;

    public GameObject landscapePanel;
    public GameObject portraitPanel;

    // Objects for Splash Screen, Story Selection and Mode Selection.
    public GameObject splashPanel;
    public Dropdown storyDropdown;

    // Objects for ROS connection.
    public GameObject rosPanel;
    public Button connectButton;
    private RosManager ros;

    // RosManager for handling connection to Ros, sending messages, etc.
    private RosManager rosManager;

    // Reference to SceneManager so we can load and manipulate story scenes.
    private StoryManager storyManager;

    // Reference to AssetDownloader.
    private AssetManager assetManager;
    private bool downloadedTitles = false;

    // Reference to AudioReocrder for when we need to record child and stream to SpeechACE.
    private AudioRecorder audioRecorder;

    // List of stories to populate dropdown.
    private List<StoryMetadata> stories;

    // Stores the scene descriptions for the current story.
    private string storyName;
    private ScreenOrientation orientation;
    private List<SceneDescription> storyPages;
    private int currentPageNumber = 0; // 0-indexed, index into this.storyPages.

    void Awake()
    {
        // Enforce singleton pattern.
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Logger.Log("duplicate GameController, destroying");
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Set up all UI elements. (SetActive, GetComponent, etc.)
        // Get references to objects if necessary.
        Logger.Log("Game Controller start");
        this.landscapeNextButton.interactable = true;
        this.landscapeNextButton.onClick.AddListener(onNextButtonClick);
        this.portraitNextButton.interactable = true;
        this.portraitNextButton.onClick.AddListener(onNextButtonClick);

        this.landscapeBackButton.interactable = true;
        this.landscapeBackButton.onClick.AddListener(onBackButtonClick);
        this.portraitBackButton.interactable = true;
        this.portraitBackButton.onClick.AddListener(onBackButtonClick);

        this.landscapeFinishButton.interactable = true;
        this.landscapeFinishButton.onClick.AddListener(onFinishButtonClick);
        this.portraitFinishButton.interactable = true;
        this.portraitFinishButton.onClick.AddListener(onFinishButtonClick);

        this.startStoryButton.onClick.AddListener(onStartStoryClicked);

        this.landscapeToggleAudioButton.onClick.AddListener(toggleAudio);
        this.portraitToggleAudioButton.onClick.AddListener(toggleAudio);

        // Update the sizing of all of the panels depending on the actual
        // screen size of the device we're on.
        this.resizePanelsOnStartup();

        this.storyPages = new List<SceneDescription>();

        this.storyManager = GetComponent<StoryManager>();
        this.assetManager = GetComponent<AssetManager>();
        this.audioRecorder = GetComponent<AudioRecorder>();

        this.stories = new List<StoryMetadata>();
        this.initStories();

        // TODO: Check if we are using ROS or not.
        // Either launch the splash screen to connect to ROS, or go straight
        // into the story selection process.

        if (Constants.USE_ROS) {
            this.rosManager = new RosManager(Constants.DEFAULT_ROSBRIDGE_IP,
                                             Constants.DEFAULT_ROSBRIDGE_PORT, this);
            // TODO: move this to when someone clicks the connect to ROS button.
            if (this.rosManager.Connect()) {
                Logger.Log("Sent hello world, status: " + this.rosManager.SendHelloWorld());
            }

        }

        this.storyManager.SetAutoplay(true);

    }

    // Update() is called once per frame.
    void Update()
    {
        // Pop all tasks from the task queue and perform them.
        // Tasks are added from other threads, usually in response to ROS msgs.
        while (this.taskQueue.Count > 0) {
            try {
                this.taskQueue.Dequeue().Invoke();
            } catch (Exception e) {
                Logger.LogError("Error invoking action on main thread!\n" + e);
            }
        }
        // Kinda sketch, make sure this happens once after everyone's start
        // has been called.
        if (!this.downloadedTitles) {
            this.downloadedTitles = true;
            // Set up the dropdown, load splash screen.
            if (Constants.LOAD_ASSETS_LOCALLY)
            {
                this.setupStoryDropdown();
                this.showSplashScreen(true);
            }
            else
            {
                List<string> storyNames = new List<string>();
                foreach (StoryMetadata story in this.stories) {
                    storyNames.Add(story.GetName());
                }
                StartCoroutine(this.assetManager.DownloadTitlePages(storyNames,
                (Dictionary<string, Sprite> images, Dictionary<string, AudioClip> audios) => {
                // Callback for when download is complete.
                    this.setupStoryDropdown();
                    this.showSplashScreen(true);
                }));
            }
        }
    }

    private void startStory(StoryMetadata story) {
        this.storyName = story.GetName();

        // Check if we need to download the json files.
        if (!Constants.LOAD_ASSETS_LOCALLY && !this.assetManager.JsonHasBeenDownloaded(story.GetName())) {
            this.showElement(this.loadingBar);
            StartCoroutine(this.assetManager.DownloadStoryJson(story, (_) => {
                List<StoryJson> storyJsons = this.assetManager.GetStoryJson(story);
                this.startStoryHelper(story, storyJsons);    
            }));
        } else {
            List<StoryJson> storyJsons = this.assetManager.GetStoryJson(story);
            this.startStoryHelper(story, storyJsons);
        }
    }

    private void startStoryHelper(StoryMetadata story, List<StoryJson> storyJsons) {
        // Sort to ensure pages are in order.
        storyJsons.Sort((s1, s2) => string.Compare(s1.GetName(), s2.GetName()));
        this.storyPages.Clear();
        // Figure out the orientation of this story and tell SceneDescription.
        this.setOrientation(story.GetOrientation());
        foreach (StoryJson json in storyJsons) {
            this.storyPages.Add(new SceneDescription(json.GetText(), this.orientation));
        }
        this.setOrientation(this.orientation);
        this.changeButtonText(this.nextButton, "Begin Story!");
        this.hideElement(this.backButton.gameObject);

        if (Constants.LOAD_ASSETS_LOCALLY ||
            this.assetManager.StoryHasBeenDownloaded(this.storyName)) {
            // Either we load from memory or we've already cached a previous download.
            this.loadFirstPage();
        } else {
            // Choose to pass lists of strings instead of the SceneDescriptions objects,
            // unnecessary but just easier to avoid possibility of mutation down the line.
            List<string> imageFileNames = new List<string>();
            List<string> audioFileNames = new List<string>();
            foreach (SceneDescription d in this.storyPages) {
                imageFileNames.Add(d.storyImageFile);
                audioFileNames.Add(d.audioFile);
            }
            if (!this.assetManager.StoryHasBeenDownloaded(this.storyName)) {
                this.hideElement(this.nextButton.gameObject);
                StartCoroutine(this.assetManager.DownloadStoryAssets(this.storyName, imageFileNames,
                                                                    audioFileNames, this.onSelectedStoryDownloaded));
            } else {
                // The assets have already been downloaded, so just begin the story.
                this.storyManager.LoadPage(this.storyPages[this.currentPageNumber]); 
            }
        }
    }

    // Handle the newly downloaded sprites and audio clips.
    private void onSelectedStoryDownloaded(Dictionary<string, Sprite> sprites,
                                    Dictionary<string, AudioClip> audioClips) {
        this.loadFirstPage();
    }

    private void loadFirstPage() {
        this.storyManager.LoadPage(this.storyPages[this.currentPageNumber]);
        this.showSplashScreen(false);
        this.hideElement(this.loadingBar);
        this.showElement(this.nextButton.gameObject);
    }

        // Show human readable story names and pull title images when possible.
    private void setupStoryDropdown() {
        this.storyDropdown.ClearOptions();
        List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
        foreach (StoryMetadata story in this.stories) {
            // Get human readable text and load the image.
            Dropdown.OptionData newOption = new Dropdown.OptionData();
            newOption.text = story.GetHumanReadableName();
            newOption.image = Util.GetTitleSprite(story);
            options.Add(newOption);
        }

        this.storyDropdown.AddOptions(options);
    }

    private void showSplashScreen(bool show) {
        if (show) {
            this.splashPanel.SetActive(true);
            this.landscapePanel.SetActive(false);
            this.portraitPanel.SetActive(false);
        } else {
            this.splashPanel.SetActive(false);
        }
    }

    // All button handlers.
    private void onNextButtonClick() {
        Logger.Log("Next Button clicked.");
        this.currentPageNumber += 1;
        this.storyManager.ClearPage();
        this.storyManager.LoadPage(this.storyPages[this.currentPageNumber]);
        if (this.currentPageNumber == 1) {
            // Special case, need to change the text and show the back button.
            this.changeButtonText(this.nextButton, "Next Page");
            this.showElement(this.backButton.gameObject);
        }
        if (this.currentPageNumber == this.storyPages.Count - 1) {
            this.hideElement(this.nextButton.gameObject);
            this.showElement(this.finishButton.gameObject);
        }
	}

    private void onFinishButtonClick() {
        // For now, just reset and return to the splash screen.
        this.storyManager.ClearPage();
        this.storyManager.audioManager.StopAudio();
        this.currentPageNumber = 0;
        this.hideElement(this.finishButton.gameObject);
        this.showElement(this.nextButton.gameObject);
        this.setLandscapeOrientation();
        this.showSplashScreen(true);
    }

    private void onBackButtonClick() {
        Logger.Log("Back Button clicked.");
        this.currentPageNumber -= 1;
        this.storyManager.ClearPage();
        this.storyManager.LoadPage(this.storyPages[this.currentPageNumber]);
        if (this.currentPageNumber == 0) {
            // Hide the back button because we're at the beginning.
            this.hideElement(this.backButton.gameObject);
        }
        // Switch away from finish story to next button if we backtrack from the last page.
        if (this.currentPageNumber == this.storyPages.Count - 2) {
            this.hideElement(this.finishButton.gameObject);
            this.showElement(this.nextButton.gameObject);
        }
    }

    private void onStartStoryClicked() {
        // Read the selected value of the story dropdown and start that story.
        //int selectedIdx = this.storyDropdown.value;
        //this.startStory(this.stories[selectedIdx]);
        if (!foo)
        {
            this.AudioTest();
            Logger.Log("happens at all?");
        } else {
            Logger.Log("second time");
            this.audioRecorder.EndRecording((clip) => {
                Logger.Log("callback!!");
                //this.storyManager.audioManager.LoadAudio(clip);
                //this.storyManager.audioManager.PlayAudio();
                //Logger.Log("Audio test over"); 


                // Save it.
                this.audioRecorder.SaveAudioAtPath("test_audio.wav", clip);

                // Try loading it back.
                StartCoroutine(this.audioRecorder.LoadAudioLocal("test_audio.wav", (AudioClip loadedClip) => {
                    Logger.Log("In callback");
                    Logger.Log(loadedClip.length);
                    this.storyManager.audioManager.LoadAudio(loadedClip);
                    this.storyManager.audioManager.PlayAudio();
                }));
                Logger.Log("Before callback");
            });
        }
    }

    // All ROS message handlers.
    // They should add tasks to the task queue.
    // Don't worry about this yet. Use ROS Manager class to handle this.

    private void onStopReadingReceived() {
        // Robot wants to intervene, so we should stop the automatic reading.    
    }

    // Helpers.

    private void AudioTest() {
        Logger.Log("In audio test");
        //StartCoroutine(this.audioRecorder.RecordForDuration(3, (clip) => {
        //    recorded = clip;
        //    foo = true;
        //}));
        this.audioRecorder.StartRecording();
        foo = true;
        Logger.Log("happens without blocking??");
    }

    private void toggleAudio() {
        this.storyManager.ToggleAudio();
    }

    private void changeButtonText(Button button, string text) {
        button.GetComponentInChildren<Text>().text = text;
    }

    private void showElement(GameObject go) {
        go.SetActive(true);
    }

    private void hideElement(GameObject go) {
        go.SetActive(false);
    }

    private void resizePanelsOnStartup() {
        // Panels that need to be resized are landscapePanel, portraitPanel,
        // and splashPanel.
        int width = Util.GetScreenWidth();
        int height = Util.GetScreenHeight();
        Vector2 landscape = new Vector2(width, height);
        Vector2 portrait = new Vector2(height, width);

        this.landscapePanel.GetComponent<RectTransform>().sizeDelta = landscape;
        this.portraitPanel.GetComponent<RectTransform>().sizeDelta = portrait;
        this.splashPanel.GetComponent<RectTransform>().sizeDelta = landscape;
    }

    private void setOrientation(ScreenOrientation o) {
        this.orientation = o;
        switch (o) {
            case ScreenOrientation.Landscape:
                this.setLandscapeOrientation();
                break;
            case ScreenOrientation.Portrait:
                this.setPortraitOrientation();
                break;
            default:
                Logger.LogError("No orientation: " + o.ToString());
                break;
        }
    }

    private void setLandscapeOrientation() {
        Logger.Log("Changing to Landscape orientation");
        this.portraitPanel.SetActive(false);
        this.landscapePanel.SetActive(true);

        this.nextButton = this.landscapeNextButton;
        this.backButton = this.landscapeBackButton;
        this.finishButton = this.landscapeFinishButton;
        this.toggleAudioButton = this.landscapeToggleAudioButton;

        // TODO: is this necessary?
        Screen.orientation = ScreenOrientation.Landscape;
    }

    private void setPortraitOrientation() {
        Logger.Log("Changing to Portrait orientation");
        this.landscapePanel.SetActive(false);
        this.portraitPanel.SetActive(true);

        this.nextButton = this.portraitNextButton;
        this.backButton = this.portraitBackButton;
        this.finishButton = this.portraitFinishButton;
        this.toggleAudioButton = this.portraitToggleAudioButton;
        Screen.orientation = ScreenOrientation.Portrait;
    }

    private void initStories() {
        // TODO: Read story metadata from the cloud here instead of hardcoding this stuff.
        // It should all be read from a single file, whose url is known.
        // In the future, consider using AmazonS3 API to manually read all the buckets.
        // Don't really want to do that now because it seems like more effort than worth.
        this.stories.Add(new StoryMetadata("the_hungry_toad", 15, "landscape"));
        this.stories.Add(new StoryMetadata("will_clifford_win", 9, "landscape"));
        this.stories.Add(new StoryMetadata("henrys_happy_birthday", 29, "landscape"));
        this.stories.Add(new StoryMetadata("freda_says_please", 17, "portrait"));
        this.stories.Add(new StoryMetadata("jazz_class", 12, "portrait"));
        this.stories.Add(new StoryMetadata("a_rain_forest_day", 15,"portrait"));
        this.stories.Add(new StoryMetadata("a_cub_can", 11,"portrait"));
    }

}
