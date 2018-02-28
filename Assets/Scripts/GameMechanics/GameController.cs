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
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.UI;

public class GameController : MonoBehaviour {
    public GameObject testObjectRos;
    public GameObject testObjectRosSpeechace;

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

    // Objects for Library Screen, Story Selection and Mode Selection.
    public GameObject libraryPanel;
    public Dropdown storyDropdown;

    // Objects for ROS connection.
    public GameObject rosPanel;
    public Button rosConnectButton;
    public Text rosStatusText;
    public Text rosInputText;
    public Text rosPlaceholderText;

    public Button enterLibraryButton;

    // RosManager for handling connection to Ros, sending messages, etc.
    private RosManager rosManager;

    // Reference to SceneManager so we can load and manipulate story scenes.
    private StoryManager storyManager;

    // Reference to AssetDownloader.
    private AssetManager assetManager;
    private bool downloadedTitles = false;

    // Reference to AudioRecorder for when we need to record child and stream to SpeechACE.
    private AudioRecorder audioRecorder;
    // SpeechAceManager sends web requests and gets responses from SpeechACE.
    private SpeechAceManager speechAceManager;

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
        this.landscapeNextButton.onClick.AddListener(this.onNextButtonClick);
        this.portraitNextButton.interactable = true;
        this.portraitNextButton.onClick.AddListener(this.onNextButtonClick);

        this.landscapeBackButton.interactable = true;
        this.landscapeBackButton.onClick.AddListener(this.onBackButtonClick);
        this.portraitBackButton.interactable = true;
        this.portraitBackButton.onClick.AddListener(this.onBackButtonClick);

        this.landscapeFinishButton.interactable = true;
        this.landscapeFinishButton.onClick.AddListener(this.onFinishButtonClick);
        this.portraitFinishButton.interactable = true;
        this.portraitFinishButton.onClick.AddListener(this.onFinishButtonClick);

        this.rosConnectButton.onClick.AddListener(this.onRosConnectButtonClicked);
        this.enterLibraryButton.onClick.AddListener(this.onEnterLibraryButtonClicked);
        this.startStoryButton.onClick.AddListener(this.onStartStoryClicked);

        this.landscapeToggleAudioButton.onClick.AddListener(this.toggleAudio);
        this.portraitToggleAudioButton.onClick.AddListener(this.toggleAudio);

        // Update the sizing of all of the panels depending on the actual
        // screen size of the device we're on.
        this.resizePanelsOnStartup();

        this.storyPages = new List<SceneDescription>();

        this.storyManager = GetComponent<StoryManager>();
        this.assetManager = GetComponent<AssetManager>();
        this.audioRecorder = GetComponent<AudioRecorder>();
        this.speechAceManager = GetComponent<SpeechAceManager>();

        this.stories = new List<StoryMetadata>();
        this.initStories();

        // Either show the rosPanel to connect to ROS, or wait to go into story selection.

        if (Constants.USE_ROS) {
            this.setupRosScreen();
        }

        // TODO: figure out when to actually set this, should be dependent on game mode.
        this.storyManager.SetAutoplay(false);

    }

    // Update() is called once per frame.
    void Update()
    {
        // Pop all tasks from the task queue and perform them.
        // Tasks are added from other threads, usually in response to ROS msgs.
        while (this.taskQueue.Count > 0) {
            try {
                Logger.Log("Got a task from queue in GameController");
                this.taskQueue.Dequeue().Invoke();
            } catch (Exception e) {
                Logger.LogError("Error invoking action on main thread!\n" + e);
            }
        }
        // Kinda sketch, make sure this happens once after everyone's Start
        // has been called.
        // TODO: Move some things in other classes to Awake and then call this in Start?
        if (!this.downloadedTitles && !Constants.USE_ROS) {
            this.downloadedTitles = true;
            // Set up the dropdown, load library panel.
            if (Constants.LOAD_ASSETS_LOCALLY) {
                this.setupStoryDropdown();
                this.showLibraryPanel(true);
            }
            else {
                this.downloadStoryTitles();
            }
        }
    }

    private void downloadStoryTitles() {
        List<string> storyNames = new List<string>();
        foreach (StoryMetadata story in this.stories) {
            storyNames.Add(story.GetName());
        }
        StartCoroutine(this.assetManager.DownloadTitlePages(storyNames,
        (Dictionary<string, Sprite> images, Dictionary<string, AudioClip> audios) => {
            // Callback for when download is complete.
            this.setupStoryDropdown();
            this.showLibraryPanel(true);
        }));
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
                this.hideElement(this.toggleAudioButton.gameObject);
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
        this.showLibraryPanel(false);
        this.hideElement(this.loadingBar);
        this.showElement(this.nextButton.gameObject);
        this.showElement(this.toggleAudioButton.gameObject);
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

    private void showLibraryPanel(bool show) {
        if (show) {
            this.libraryPanel.SetActive(true);
            this.landscapePanel.SetActive(false);
            this.portraitPanel.SetActive(false);
            this.rosPanel.SetActive(false);
        } else {
            this.libraryPanel.SetActive(false);
        }
    }

    // Called at startup if Constants.USE_ROS is true.
    private void setupRosScreen() {
        // Set placeholder text to be default IP.
        this.rosPlaceholderText.text = Constants.DEFAULT_ROSBRIDGE_IP;
        this.rosPanel.SetActive(true);
        this.landscapePanel.SetActive(false);
        this.portraitPanel.SetActive(false);
        this.libraryPanel.SetActive(false);
    }

    // All button handlers.
    private void onRosConnectButtonClicked() {
        Logger.Log("Ros Connect Button clicked");
        string rosbridgeIp = Constants.DEFAULT_ROSBRIDGE_IP;
        // If user entered a different IP, use it, otherwise stick to default.
        if (this.rosInputText.text != "") {
            rosbridgeIp = this.rosInputText.text;
        }
        if (this.rosManager == null || !this.rosManager.isConnected()) {
            this.rosManager = new RosManager(rosbridgeIp, Constants.DEFAULT_ROSBRIDGE_PORT, this);
            this.storyManager.SetRosManager(this.rosManager);
            if (this.rosManager.Connect()) {
                // If connection successful, update status text.
                this.rosStatusText.text = "Connected!";
                this.rosStatusText.color = Color.green;
                this.hideElement(this.rosConnectButton.gameObject);
                this.showElement(this.enterLibraryButton.gameObject);
                // Set up the command handlers, happens the first time connection is established.
                this.rosManager.RegisterHandler(StorybookCommand.PING_TEST, this.onHelloWorldAckReceived);
                Thread.Sleep(500); // Wait for a bit to make sure connection is established.
                this.rosManager.SendHelloWorld().Invoke();
                Logger.Log("Sent hello message");
            } else {
                this.rosStatusText.text = "Failed to connect, try again.";
                this.rosStatusText.color = Color.red;
            }
        } else {
            Logger.Log("Already connected to ROS, not trying to connect again");
        }
    }

    private void onEnterLibraryButtonClicked() {
        // Prepares the assets for showing the library, and then displays the panel.
        this.downloadStoryTitles();
    }

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
        // For now, just reset and return to the library panel.
        this.storyManager.ClearPage();
        this.storyManager.audioManager.StopAudio();
        this.currentPageNumber = 0;
        this.hideElement(this.finishButton.gameObject);
        this.showElement(this.nextButton.gameObject);
        this.setLandscapeOrientation();
        this.showLibraryPanel(true);
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
        int selectedIdx = this.storyDropdown.value;
        this.startStory(this.stories[selectedIdx]);
    }

    // All ROS message handlers.
    // They should add tasks to the task queue.
    // Don't worry about this yet. Use ROS Manager class to handle this.

    private void onHelloWorldAckReceived(Dictionary<string, object> properties) {
        // Test that this is called from rosManager.
        Logger.Log("in hello world ack received in game controller");
        // TODO: remove later.
        this.taskQueue.Enqueue(() =>
        {
            // this.testObjectRos.SetActive(true);
            this.testObjectRosSpeechace.SetActive(true);
        });
    }

    // Helpers.

    public void TestAudio() {
        string fileName = "foo.wav";
        // Test recording, saving and loading an audio clip.
        StartCoroutine(audioRecorder.RecordForDuration(6, (clip) => {
            this.audioRecorder.SaveAudioAtPath(fileName, clip);
            Logger.Log("in recorder callback");
            StartCoroutine(this.speechAceManager.AnalyzeTextSample(
                fileName, "there once was a toad named toad",
                (speechAceResult) => {
                    Logger.Log("in SpeechACE callback");
                    if (Constants.USE_ROS) {
                        this.rosManager.SendSpeechAceResult(speechAceResult).Invoke();
                    }
                    Logger.Log("happens immediately after recorder callback");
                    //            AudioClip loadedClip = this.audioRecorder.LoadAudioLocal(fileName);
                    //            this.storyManager.audioManager.LoadAudio(loadedClip);
                    //            this.storyManager.audioManager.PlayAudio();
                    Logger.Log("pushing to S3 now");
                    this.assetManager.S3UploadChildAudio (fileName);
                    Logger.Log("happens immediately after calling upload");
                }));
        }));
    }

    private void toggleAudio() {
        // TODO: uncomment this and change it back.
        //this.storyManager.ToggleAudio();
        this.TestAudio();
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
        // and libraryPanel.
        int width = Util.GetScreenWidth();
        int height = Util.GetScreenHeight();
        Vector2 landscape = new Vector2(width, height);
        Vector2 portrait = new Vector2(height, width);

        this.landscapePanel.GetComponent<RectTransform>().sizeDelta = landscape;
        this.portraitPanel.GetComponent<RectTransform>().sizeDelta = portrait;
        this.libraryPanel.GetComponent<RectTransform>().sizeDelta = landscape;
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
