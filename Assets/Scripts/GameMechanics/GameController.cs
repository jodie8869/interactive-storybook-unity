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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using NUnit.Framework.Internal;
using NUnit.Framework;
using System.Reflection.Emit;
using UnityEngine.Events;

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
    public Button landscapeStartStoryButton;
    public Button landscapeBackToLibraryButton;
    public Button landscapeHomeButton; // In reader mode, to exit the story.
    public Button landscapeToggleAudioButton;

    public Button portraitNextButton;
    public Button portraitBackButton;
    public Button portraitFinishButton;
    public Button portraitStartStoryButton;
    public Button portraitBackToLibraryButton;
    public Button portraitHomeButton; // In reader mode, to exit the story..
    public Button portraitToggleAudioButton;

    private Button nextButton;
    private Button backButton;
    private Button finishButton;
    private Button toggleAudioButton;
    private Button startStoryButton;
    private Button backToLibraryButton;

    public Button selectStoryButton;
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
    public Button readButton;
    public Button findMoreStoriesButton;

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

    public GameObject bookshelfPanel;
    private List<GameObject> libraryShelves;
    private List<GameObject> libraryBooks;
    private int selectedLibraryBookIndex = -1;

    // Some information about the current state of the storybook.
    private StoryMetadata currentStory;
    private ScreenOrientation orientation;
    // Stores the scene descriptions for the current story.
    private List<SceneDescription> storyPages;
    private int currentPageNumber = 0; // 0-indexed, index into this.storyPages, 0 is title page.

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

        // Do this in Awake() to avoid null references.
        StorybookStateManager.Init();
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

        this.landscapeBackToLibraryButton.onClick.AddListener(this.onBackToLibraryButtonClick);
        this.portraitBackToLibraryButton.onClick.AddListener(this.onBackToLibraryButtonClick);
        this.landscapeHomeButton.onClick.AddListener(this.onBackToLibraryButtonClick);
        this.portraitHomeButton.onClick.AddListener(this.onBackToLibraryButtonClick);

        this.landscapeStartStoryButton.onClick.AddListener(this.onNextButtonClick);
        this.portraitStartStoryButton.onClick.AddListener(this.onNextButtonClick);

        this.rosConnectButton.onClick.AddListener(this.onRosConnectButtonClick);
        this.enterLibraryButton.onClick.AddListener(this.onEnterLibraryButtonClick);
        this.selectStoryButton.onClick.AddListener(this.onStartStoryClick);

        this.landscapeToggleAudioButton.onClick.AddListener(this.toggleAudio);
        this.portraitToggleAudioButton.onClick.AddListener(this.toggleAudio);

        this.readButton.onClick.AddListener(this.onStartStoryClick);

        // Update the sizing of all of the panels depending on the actual
        // screen size of the device we're on.
        this.resizePanelsOnStartup();

        this.storyPages = new List<SceneDescription>();

        this.storyManager = GetComponent<StoryManager>();
        this.assetManager = GetComponent<AssetManager>();
        this.audioRecorder = GetComponent<AudioRecorder>();
        this.speechAceManager = GetComponent<SpeechAceManager>();

        this.libraryBooks = new List<GameObject>();
        this.libraryShelves = new List<GameObject>();

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
    void Update() {
        // Pop tasks from the task queue and perform them.
        // Tasks are added from other threads, usually in response to ROS msgs.
        if (this.taskQueue.Count > 0) {
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
    // Clean up.
    void OnApplicationQuit() {
        if (this.rosManager != null && this.rosManager.isConnected()) {
            // Stop the thread that's sending StorybookState messages.
            this.rosManager.StopSendingStorybookState();
            // Close the ROS connection cleanly.
            this.rosManager.CloseConnection();   
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

    private Action onLibraryBookClick(int index, StoryMetadata story) {
        return () => {
            Logger.Log("Clicked book: " + index + " " + story.GetName());
            // Make all books normal size.
            if (this.selectedLibraryBookIndex >= 0) {
                this.libraryBooks[this.selectedLibraryBookIndex]
                    .GetComponent<LibraryBook>().ReturnToOriginalSize();
            }

            if (this.selectedLibraryBookIndex == index) {
                this.selectedLibraryBookIndex = -1;
                this.hideElement(this.readButton.gameObject);
            } else {
                this.selectedLibraryBookIndex = index;
                // Enlarge the appropriate library book.
                this.libraryBooks[index].GetComponent<LibraryBook>().Enlarge();
                // Show the Read button.
                this.showElement(this.readButton.gameObject);
            }


        };
    }

    private void startStory(StoryMetadata story) {
        this.currentStory = story;

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
        this.orientation = story.GetOrientation();
        this.setOrientationButtons(this.orientation);
        foreach (StoryJson json in storyJsons) {
            this.storyPages.Add(new SceneDescription(json.GetText(), this.orientation));
        }
        // this.changeButtonText(this.nextButton, "Begin Story!");
        this.hideElement(this.backButton.gameObject);

        if (Constants.LOAD_ASSETS_LOCALLY ||
            this.assetManager.StoryHasBeenDownloaded(this.currentStory.GetName())) {
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
            if (!this.assetManager.StoryHasBeenDownloaded(this.currentStory.GetName())) {
                this.hideElement(this.nextButton.gameObject);
                this.hideElement(this.toggleAudioButton.gameObject);
                StartCoroutine(this.assetManager.DownloadStoryAssets(this.currentStory.GetName(), imageFileNames,
                                                                    audioFileNames, this.onSelectedStoryDownloaded));
            } else {
                // The assets have already been downloaded, so just begin the story.
                this.loadFirstPage();
            }
        }
    }

    // Handle the newly downloaded sprites and audio clips.
    private void onSelectedStoryDownloaded(Dictionary<string, Sprite> sprites,
                                    Dictionary<string, AudioClip> audioClips) {
        this.loadFirstPage();
    }

    private void loadFirstPage() {
        StorybookStateManager.SetStorybookMode(StorybookMode.Explore);
        this.loadPageAndSendRosMessage(this.storyPages[this.currentPageNumber]);
        this.showLibraryPanel(false);
        this.hideElement(this.loadingBar);
        this.showElement(this.nextButton.gameObject);
        this.showElement(this.toggleAudioButton.gameObject);
        this.setOrientationView(this.orientation);
    }

        // Show human readable story names and pull title images when possible.
    private void setupStoryDropdown() {
        int row = 0;
        int col = 0;
        for (int i = 0; i < this.stories.Count; i++) {
            if (col == 0) {
                this.libraryShelves.Add(Instantiate((GameObject)Resources.Load("Prefabs/Shelf")));
                this.libraryShelves[row].transform.SetParent(this.bookshelfPanel.transform);
                Util.UpdateShelfPosition(this.libraryShelves[row], row);
            }
            StoryMetadata story = this.stories[i];
            GameObject bookObject =
                Instantiate((GameObject)Resources.Load("Prefabs/LibraryBook"));
            LibraryBook libraryBook = bookObject.GetComponent<LibraryBook>();
            libraryBook.AddClickHandler(this.onLibraryBookClick(i, story));
            libraryBook.SetStory(story);
            libraryBook.SetSprite(this.assetManager.GetTitleSprite(story));
            bookObject.transform.SetParent(this.libraryShelves[row].transform);
            this.libraryBooks.Add(bookObject);
            col += 1;
            if (col / Constants.NUM_LIBRARY_COLS > 0) {
                col = col % Constants.NUM_LIBRARY_COLS;
                row += 1;
            }
        }
           

//        this.storyDropdown.ClearOptions();
//        List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
//        foreach (StoryMetadata story in this.stories) {
//            // Get human readable text and load the image.
//            Dropdown.OptionData newOption = new Dropdown.OptionData();
//            newOption.text = story.GetHumanReadableName();
//            newOption.image = this.assetManager.GetTitleSprite(story);
//            options.Add(newOption);
//        }
//
//        this.storyDropdown.AddOptions(options);
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
    private void onRosConnectButtonClick() {
        Logger.Log("Ros Connect Button clicked");
        string rosbridgeIp = Constants.DEFAULT_ROSBRIDGE_IP;
        // If user entered a different IP, use it, otherwise stick to default.
        if (this.rosInputText.text != "") {
            rosbridgeIp = this.rosInputText.text;
            Logger.Log("Trying to connect to roscore at " + rosbridgeIp);
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
                this.registerRosMessageHandlers();
                Thread.Sleep(1000); // Wait for a bit to make sure connection is established.
                this.rosManager.SendHelloWorldAction().Invoke();
                Logger.Log("Sent hello ping message");
            } else {
                this.rosStatusText.text = "Failed to connect, try again.";
                this.rosStatusText.color = Color.red;
            }
        } else {
            Logger.Log("Already connected to ROS, not trying to connect again");
        }
    }

    private void onEnterLibraryButtonClick() {
        // Prepares the assets for showing the library, and then displays the panel.
        this.downloadStoryTitles();
    }

    // When user clicks button to go back to the library and exit the current story they're in.
    private void onBackToLibraryButtonClick() {
        this.storyManager.ClearPage();
        this.storyManager.audioManager.StopAudio();
        this.currentPageNumber = 0;
        this.setLandscapeOrientation();
        this.showLibraryPanel(true);
    }

    private void onNextButtonClick() {
        Logger.Log("Next Button clicked.");
        this.currentPageNumber += 1;
        this.storyManager.ClearPage();
        this.loadPageAndSendRosMessage(this.storyPages[this.currentPageNumber]);
        if (this.currentPageNumber == 1) {
            // Special case, need to change the text and show the back button.
            // this.changeButtonText(this.nextButton, "Next Page");
            this.showElement(this.backButton.gameObject);
        }
        if (this.currentPageNumber == this.storyPages.Count - 1) {
            this.hideElement(this.nextButton.gameObject);
            this.showElement(this.finishButton.gameObject);
        }
	}

    private void onFinishButtonClick() {
        this.storyManager.ClearPage();
        this.storyManager.audioManager.StopAudio();
        this.currentPageNumber = 0;
        this.hideElement(this.finishButton.gameObject);
        this.showElement(this.nextButton.gameObject);

        // If in explore mode, then go to evaluate mode.
        if (StorybookStateManager.GetState().storybookMode == StorybookMode.Explore) {
            StorybookStateManager.SetStorybookMode(StorybookMode.Evaluate);
            Logger.Log(StorybookStateManager.GetState().storybookMode);
            this.hideElement(this.backButton.gameObject);
            this.loadPageAndSendRosMessage(this.storyPages[this.currentPageNumber]);
        }
        // If in evaluate mode then go to post-test.
        else if (StorybookStateManager.GetState().storybookMode == StorybookMode.Evaluate) {
            StorybookStateManager.SetStorybookMode(StorybookMode.PostTest);
            // TODO: send a message so controller can start telling us what pages to load,
            // via StorybookCommands.
        }
        // If in post test then return to story selection.
        else if (StorybookStateManager.GetState().storybookMode == StorybookMode.PostTest) {
            this.setLandscapeOrientation();
            this.showLibraryPanel(true);
            StorybookStateManager.SetStoryExited();
            // TODO: should send an event saying that we are done with the interaction.
        }
    }

    private void onBackButtonClick() {
        Logger.Log("Back Button clicked.");
        this.currentPageNumber -= 1;
        this.storyManager.ClearPage();
        this.loadPageAndSendRosMessage(this.storyPages[this.currentPageNumber]);
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

    private void onStartStoryClick() {
        // Read the selected value of the story dropdown and start that story.
//        int selectedIdx = this.storyDropdown.value;
//        this.startStory(this.stories[selectedIdx]);
        LibraryBook selectedBook = this.libraryBooks[this.selectedLibraryBookIndex]
            .GetComponent<LibraryBook>();
        this.hideElement(readButton.gameObject);
        this.startStory(selectedBook.story);
        selectedBook.ReturnToOriginalSize();
    }

    //
    // All ROS message handlers.
    // They should add tasks to the task queue.
    // Don't worry about this yet. Use ROS Manager class to handle this.
    //

    private void registerRosMessageHandlers() {
        this.rosManager.RegisterHandler(StorybookCommand.PING_TEST, this.onHelloWorldAckReceived);
        this.rosManager.RegisterHandler(StorybookCommand.HIGHLIGHT_WORD, this.onHighlightTinkerTextMessage);
        this.rosManager.RegisterHandler(StorybookCommand.HIGHLIGHT_SCENE_OBJECT, this.onHighlightSceneObjectMessage);
        this.rosManager.RegisterHandler(StorybookCommand.HIGHLIGHT_NEXT_SENTENCE, this.onHighlightNextSentenceMessage);
        this.rosManager.RegisterHandler(StorybookCommand.RECORD_AUDIO_AND_SPEECHACE, this.onRecordAudioAndSpeechaceMessage);
    }

    private void onHelloWorldAckReceived(Dictionary<string, object> args) {
        // Sanity check from the ping test after tablet app starts up.
        Logger.Log("in hello world ack received in game controller, " + args["obj1"]);
    }

    private void onHighlightTinkerTextMessage(Dictionary<string, object> args) {
        int index = Convert.ToInt32(args["index"]);
        this.taskQueue.Enqueue(this.highlightTinkerText(index));
    }

    private Action highlightTinkerText(int index) {
        return () => {
            if (index < this.storyManager.tinkerTexts.Count && index >= 0) {
                this.storyManager.tinkerTexts[index].GetComponent<TinkerText>()
                    .Highlight().Invoke();   
            } else {
                Logger.Log("No word at index: " + index);
            }
        };
    }

    private void onHighlightSceneObjectMessage(Dictionary<string, object> args) {
        int id = Convert.ToInt32(args["id"]);
        this.taskQueue.Enqueue(this.highlightSceneObject(id));
    }

    private Action highlightSceneObject(int id) {
        return () => {
            if (this.storyManager.sceneObjects.ContainsKey(id)) {
                this.storyManager.sceneObjects[id].GetComponent<SceneObjectManipulator>()
                    .Highlight(Constants.SCENE_OBJECT_HIGHLIGHT_COLOR).Invoke();   
            } else {
                Logger.Log("No scene object with id: " + id);
            }
        };
    }

    private void onHighlightNextSentenceMessage(Dictionary<string, object> args) {
        // Assert that we are highlighting the appropriate sentence.
        // Need to cast better.
        Logger.Log("onHighlightNextSentenceMessage");
        if (Convert.ToInt32(args["index"]) != StorybookStateManager.GetState().evaluatingSentenceIndex + 1) {
            Logger.LogError("Sentence index doesn't match " + args["index"] + " " +
            StorybookStateManager.GetState().evaluatingSentenceIndex + 1);
            throw new Exception("Sentence index doesn't match, fail fast");
        }
        this.taskQueue.Enqueue(this.showNextSentence((bool)args["child_turn"]));
    }

    private Action showNextSentence(bool childTurn) {
        return () => {
            Logger.Log("Showing next sentence from inside task queue");
            if (StorybookStateManager.GetState().evaluatingSentenceIndex + 1 <
                this.storyManager.stanzaManager.GetNumSentences()) {
                StorybookStateManager.IncrementEvaluatingSentenceIndex();
                int newIndex = StorybookStateManager.GetState().evaluatingSentenceIndex;
                Color color = new Color();
                if (childTurn) {
                    color = Constants.CHILD_READ_TEXT_COLOR;
                } else {
                    color = Constants.JIBO_READ_TEXT_COLOR;
                }
                this.storyManager.stanzaManager.GetSentence(newIndex).FadeIn(color);
                if (newIndex - 1 >= 0) {
                    this.storyManager.stanzaManager.GetSentence(newIndex - 1).Highlight(Constants.GREY_TEXT_COLOR);
                }
            } 
        };
    }

    private void onRecordAudioAndSpeechaceMessage(Dictionary<string, object> args) {
        Logger.Log("onRecordAudioAndSpeechaceMessage");
        this.taskQueue.Enqueue(this.recordAudioAndGetSpeechAceResult(Convert.ToInt32(args["index"])));
    }
        

    private Action recordAudioAndGetSpeechAceResult(int sentenceIndex) {
        return () => {
            Logger.Log("Recording audio from inside task queue");
            Sentence sentence = this.storyManager.stanzaManager.GetSentence(sentenceIndex);
            string text = sentence.GetSentenceText();
            // Approximately the length of the sentence, plus a little more.
            int duration = Convert.ToInt32(sentence.GetDuration() * 1.33) + 1;
            Logger.Log("Recording for sentence index " + sentenceIndex + " text: " + text + " duration: " + duration);
            string tempFileName = this.currentPageNumber + "_" + sentenceIndex + ".wav";
            // Test recording, saving and loading an audio clip.
            StartCoroutine(audioRecorder.RecordForDuration(duration, (clip) => {
                Logger.Log("Done recording, getting speechACE results and uploading file to S3...");
                AudioRecorder.SaveAudioAtPath(tempFileName, clip);
                StartCoroutine(this.speechAceManager.AnalyzeTextSample(
                    tempFileName, text, (speechAceResult) => {
                        if (Constants.USE_ROS) {
                            this.rosManager.SendSpeechAceResultAction(speechAceResult).Invoke();
                        }
                        // If we want to replay for debugging, uncomment this.
                        // AudioClip loadedClip = AudioRecorder.LoadAudioLocal(fileName);
                        // this.storyManager.audioManager.LoadAudio(loadedClip);
                        // this.storyManager.audioManager.PlayAudio();
                        this.assetManager.S3UploadChildAudio(tempFileName);
                    }));
            }));  
        };
    }
        

    //
    // Helpers.
    //

    // Helper function to wrap together two actions:
    // (1) loading a page and (2) sending the StorybookPageInfo message over ROS.
    private void loadPageAndSendRosMessage(SceneDescription sceneDescription) {
        // Load the page.
        this.storyManager.LoadPage(sceneDescription);

        // Send the ROS message to update the controller about what page we're on now.
        StorybookPageInfo updatedInfo = new StorybookPageInfo();
        updatedInfo.storyName = this.currentStory.GetName();
        updatedInfo.pageNumber = this.currentPageNumber;
        updatedInfo.sentences = this.storyManager.stanzaManager.GetAllSentenceTexts();

        // Update state (will get automatically sent to the controller.
        StorybookStateManager.SetStorySelected(this.currentStory.GetName(),
            this.currentStory.GetNumPages());

        // Gather information about scene objects.
        StorybookSceneObject[] sceneObjects =
            new StorybookSceneObject[sceneDescription.sceneObjects.Length];
        for (int i = 0; i < sceneDescription.sceneObjects.Length; i++) {
            SceneObject so = sceneDescription.sceneObjects[i];
            StorybookSceneObject sso = new StorybookSceneObject();
            sso.id = so.id;
            sso.label = so.label;
            sso.inText = so.inText;
            sceneObjects[i] = sso;
        }
        updatedInfo.sceneObjects = sceneObjects;

        // Gather information about tinker texts.
        StorybookTinkerText[] tinkerTexts =
            new StorybookTinkerText[this.storyManager.tinkerTexts.Count];
        for (int i = 0; i < this.storyManager.tinkerTexts.Count; i++) {
            TinkerText tt = this.storyManager.tinkerTexts[i].GetComponent<TinkerText>();
            StorybookTinkerText stt = new StorybookTinkerText();
            stt.word = tt.word;
            stt.hasSceneObject = false;
            stt.sceneObjectId = -1;
            tinkerTexts[i] = stt;
        }
        foreach (Trigger trigger in sceneDescription.triggers) {
            if (trigger.type == TriggerType.CLICK_TINKERTEXT_SCENE_OBJECT) {
                tinkerTexts[trigger.args.textId].hasSceneObject = true;
                tinkerTexts[trigger.args.textId].sceneObjectId = trigger.args.sceneObjectId;
            }
        }
        updatedInfo.tinkerTexts = tinkerTexts;
       
        // Send the message.
        if (Constants.USE_ROS) {
            this.rosManager.SendStorybookPageInfoAction(updatedInfo);
        }
    }

    private void toggleAudio() {
        this.showNextSentence(true);
        // TODO: change this back to what it's supposed to be, which is just toggling audio.
        // this.RecordAudioAndGetSpeechAceResult(6, "There once was a toad named toad", 0);
        // this.storyManager.ToggleAudio();
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

    private void setOrientationButtons(ScreenOrientation o) {
        this.orientation = o;
        switch (o) {
            case ScreenOrientation.Landscape:
                this.setLandscapeOrientation();
                break;
            case ScreenOrientation.Portrait:
                this.setPortraitOrientation();
                break;
            default:
                Logger.LogError("No orientation: " + o);
                break;
        }
    }

    private void setOrientationView(ScreenOrientation o) {
        this.orientation = o;
        switch (o) {
        case ScreenOrientation.Landscape:
            this.portraitPanel.SetActive(false);
            this.landscapePanel.SetActive(true);
            break;
        case ScreenOrientation.Portrait:
            this.landscapePanel.SetActive(false);
            this.portraitPanel.SetActive(true);
            break;
        default:
            Logger.LogError("No orientation: " + o);
            break;
        }
    }

    private void setLandscapeOrientation() {
        Logger.Log("Changing to Landscape orientation");

        this.nextButton = this.landscapeNextButton;
        this.backButton = this.landscapeBackButton;
        this.finishButton = this.landscapeFinishButton;
        this.toggleAudioButton = this.landscapeToggleAudioButton;
        this.startStoryButton = this.landscapeStartStoryButton;
        this.backToLibraryButton = this.landscapeBackToLibraryButton;

        // TODO: is this necessary?
        Screen.orientation = ScreenOrientation.Landscape;
    }

    private void setPortraitOrientation() {
        Logger.Log("Changing to Portrait orientation");

        this.nextButton = this.portraitNextButton;
        this.backButton = this.portraitBackButton;
        this.finishButton = this.portraitFinishButton;
        this.toggleAudioButton = this.portraitToggleAudioButton;
        this.startStoryButton = this.portraitStartStoryButton;
        this.backToLibraryButton = this.portraitBackToLibraryButton;
        Screen.orientation = ScreenOrientation.Portrait;
    }

    private void initStories() {
        // TODO: Read story metadata from the cloud here instead of hardcoding this stuff.
        // It should all be read from a single file, whose url is known.
        // In the future, consider using AmazonS3 API to manually read all the buckets.
        // Don't really want to do that now because it seems like more effort than worth.

        this.stories.Add(new StoryMetadata("a_dozen_dogs", 17, "landscape"));
        this.stories.Add(new StoryMetadata("at_bat", 9, "landscape"));
        this.stories.Add(new StoryMetadata("baby_pig_at_school", 15, "landscape"));
        this.stories.Add(new StoryMetadata("clifford_and_the_jet", 9, "landscape"));
        this.stories.Add(new StoryMetadata("freda_says_please", 17, "portrait"));
        this.stories.Add(new StoryMetadata("geraldine_first", 21, "landscape"));
        this.stories.Add(new StoryMetadata("henrys_happy_birthday", 29, "landscape"));
        this.stories.Add(new StoryMetadata("jane_and_jake_bake_a_cake", 15, "landscape"));
        this.stories.Add(new StoryMetadata("mice_on_ice", 15, "landscape"));
        this.stories.Add(new StoryMetadata("paws_and_claws", 14, "landscape"));
        this.stories.Add(new StoryMetadata("pete_the_cat_too_cool_for_school", 28, "portrait"));
        this.stories.Add(new StoryMetadata("the_biggest_cookie_in_the_world", 21, "landscape"));
        this.stories.Add(new StoryMetadata("the_hungry_toad", 15, "landscape"));
        this.stories.Add(new StoryMetadata("troll_tricks", 15, "portrait"));
        this.stories.Add(new StoryMetadata("who_hid_it", 9, "landscape"));


        // Other stories, commented out because they're not used in the study.
//        this.stories.Add(new StoryMetadata("will_clifford_win", 9, "landscape"));
//        this.stories.Add(new StoryMetadata("jazz_class", 12, "portrait"));
//        this.stories.Add(new StoryMetadata("a_rain_forest_day", 15,"portrait"));
//        this.stories.Add(new StoryMetadata("a_cub_can", 11,"portrait"));
    }

}
