// StoryManager loads a scene based on a SceneDescription, including loading
// images, audio files, and drawing colliders and setting up callbacks to
// handle trigger events. StoryManager uses methods in TinkerText and
// SceneObjectManipulator for setting up these callbacks.

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Collections;

public class StoryManager : MonoBehaviour {

    // We may want to call methods on GameController or add to the task queue.
    public GameController gameController;
    public StoryAudioManager audioManager;
    public StanzaManager stanzaManager;
    private AssetManager assetManager;
    private RosManager rosManager;

	public GameObject portraitGraphicsPanel;
    public GameObject portraitTextPanel;
    public GameObject landscapeGraphicsPanel;
    public GameObject landscapeTextPanel;
	public GameObject landscapeWideGraphicsPanel;
	public GameObject landscapeWideTextPanel;
    public GameObject portraitOpenBookBackground;
    public GameObject landscapeOpenBookBackground;

    public GameObject landscapeReaderPanel;
    public GameObject portraitReaderPanel;

    public GameObject landscapeTitleImagePanel; // Panel for just the book cover image.
    public GameObject landscapeTitlePanel; // Panel for entire title page screen.
    public GameObject portraitTitleImagePanel;
    public GameObject portraitTitlePanel;

    public GameObject landscapeEndPagePanel;
    public GameObject portraitEndPagePanel;
    public GameObject landscapeConfettiObject;
    public GameObject portraitConfettiObject;

    public GameObject popupLabel;

    private bool autoplayAudio = false;

    // Used for internal references.
    private GameObject readerPanel;
    private GameObject graphicsPanel;
    private GameObject textPanel;
    private GameObject openBookBackground;
    private GameObject titleImagePanel;
    private GameObject titlePanel;
    private GameObject endPagePanel;
    private GameObject confettiObject;

    private float graphicsPanelWidth;
    private float graphicsPanelHeight;
    private float graphicsPanelAspectRatio;
    private float titlePanelImageAspectRatio;

    // These all need to be set after we determine the screen size.
    // They are set in initPanelSizesOnStartup() and used in resetPanelSizes().
    private float LANDSCAPE_GRAPHICS_WIDTH;
    private float LANDSCAPE_TEXT_WIDTH;
    private float LANDSCAPE_HEIGHT;
    private float PORTRAIT_GRAPHICS_HEIGHT;
    private float PORTRAIT_TEXT_HEIGHT;
    private float PORTRAIT_WIDTH;
    private float LANDSCAPE_WIDE_GRAPHICS_HEIGHT;
    private float LANDSCAPE_WIDE_TEXT_HEIGHT;
    private float LANDSCAPE_WIDE_WIDTH;

   
    // Dynamically created TinkerTexts specific to this scene.
    public List<GameObject> tinkerTexts { get; private set; }
    // Dynamically created SceneObjects, keyed by their id.
    public Dictionary<int, GameObject> sceneObjects { get; private set; }
    private Dictionary<string, List<int>> sceneObjectsLabelToId;
    // Map scene objects to arrays of tinkertexts.
    private Dictionary<int, List<int>> sceneObjectToTinkerText;
    // The image we loaded for this scene.
    private GameObject storyImage;
    // Need to know the actual dimensions of the background image, so that we
    // can correctly place new SceneObjects on the background.
    private float storyImageWidth;
    private float storyImageHeight;
    // The (x,y) coordinates of the upper left corner of the image in relation
    // to the upper left corner of the encompassing GameObject.
    private float storyImageX;
    private float storyImageY;
    // Ratio of the story image to the original texture size.
    private float imageScaleFactor;
    private DisplayMode displayMode;

    private int stanzaIndex;


    void Start() {
        Logger.Log("StoryManager start");

        this.assetManager = GetComponent<AssetManager>();

        this.tinkerTexts = new List<GameObject>();
        this.sceneObjects = new Dictionary<int, GameObject>();
        this.sceneObjectsLabelToId = new Dictionary<string, List<int>>();
        this.sceneObjectToTinkerText = new Dictionary<int, List<int>>();

        this.stanzaManager.SetTextPanel(this.textPanel);

        this.initPanelSizesOnStartup();

        this.stanzaIndex = 0;
    }

    void Update() {
        // Update whether or not we are accepting user interaction.
        Stanza.ALLOW_SWIPE_BECAUSE_AUDIO = !this.audioManager.IsPlaying();
    }

    public void SetRosManager(RosManager ros) {
        this.rosManager = ros;
        this.stanzaManager.SetRosManager(this.rosManager);
    }

    // Shows the TheEnd page.
    public void ShowTheEndPage(bool show) {
        if (show) {
            this.titlePanel.SetActive(false);
            this.readerPanel.SetActive(false);
            this.endPagePanel.SetActive(true);
            StartCoroutine(this.fadeInConfetti());
        } else {
            this.endPagePanel.SetActive(false);
        }
    }

    private IEnumerator fadeInConfetti() {
        this.confettiObject.GetComponent<CanvasGroup>().alpha = 0f;
        while (this.confettiObject.GetComponent<CanvasGroup>().alpha < 1) {
            this.confettiObject.GetComponent<CanvasGroup>().alpha += Time.deltaTime * .5f;
            yield return null;
        }
        yield return null;
    }

    // Main function to be called by GameController.
    // Passes in a description received over ROS or hardcoded.
    // LoadScene is responsible for loading all resources and putting them in
    // place, and attaching callbacks to created GameObjects, where these
    // callbacks involve functions from SceneManipulatorAPI.
    public void LoadPage(SceneDescription description) {

        this.setDisplayModeFromSceneDescription(description);
        this.resetPanelSizes();

        // Load audio.
        if (description.audioFile != "") {
            this.audioManager.LoadAudio(description.audioFile, this.assetManager.GetAudioClip(description.audioFile));
        }

        if (description.isTitle) {
            // Show only the title panel.
            this.titlePanel.SetActive(true);
            this.readerPanel.SetActive(false);

            // Special case for title page.
            // No TinkerTexts, and image takes up a larger space.
            this.loadTitlePage(description);
        } else {
            // Show only the text and graphics panels.
            this.titlePanel.SetActive(false);
            this.readerPanel.SetActive(true);
           
            // Load image.
            this.loadImage(description.storyImageFile);

            List<string> textWords =
                new List<string>(description.text.Split(' '));
            // Need to remove any empty or only punctuation words.
            textWords.RemoveAll(String.IsNullOrEmpty);
            List<string> filteredTextWords = new List<string>();
            foreach (string word in textWords) {
                if (Util.WordHasNoAlphanum(word)) {
                    filteredTextWords[filteredTextWords.Count - 1] += word;
                } else {
                    filteredTextWords.Add(word);
                }
            }
            if (filteredTextWords.Count != description.timestamps.Length) {
                Logger.LogError("textWords doesn't match timestamps length " +
                                filteredTextWords.Count + " " + description.timestamps.Length);
            }
            // Depending on how many words there are, update the sizing and spacing heuristically.
            this.resizeSpacingAndFonts(filteredTextWords.Count);

            for (int i = 0; i < filteredTextWords.Count; i++) {
                this.loadTinkerText(i, filteredTextWords[i], description.timestamps[i],
                                      i == filteredTextWords.Count - 1);
            }

            // After all TinkerTexts and Stanzas have been formatted, set up all the sentences and
            // set the stanza swipe handlers.
            this.stanzaManager.SetupSentences();
            // If we are in evaluate mode, all stanzas should be hidden by default.
            if (StorybookStateManager.GetState().storybookMode == StorybookMode.Evaluate) {
                this.stanzaManager.HideAllSentences();
            }
            // This will send StorybookEvent ROS messages to the controller when stanzas are swiped.
            if (Constants.USE_ROS) {
                this.stanzaManager.SetSentenceSwipeHandlers();
            }

            // Load audio triggers for TinkerText.
            if (description.audioFile != "") {
                this.loadAudioTriggers();
            }
        }

        // Load all scene objects.
        foreach (SceneObject sceneObject in description.sceneObjects) {
            this.loadSceneObject(sceneObject);
        }

        // Sort scene objects by size (smallest on top).
        this.sortSceneObjectLayering();

        // Load triggers.
        foreach (Trigger trigger in description.triggers) {
            this.loadTrigger(trigger);
        }

        // Pair up the tinkertexts that are in the same label.
        if (StorybookStateManager.GetState().storybookMode == StorybookMode.Explore) {
            this.loadTinkerTextLabelPairTriggers();
        }

        // Set up ros handlers for tapping on TinkerTexts.
        if (Constants.USE_ROS) {
            this.setUpTinkerTextRosHandlers();   
        }

        // If we are set to autoplay, then autoplay, obviously.
        if (this.autoplayAudio && description.audioFile != "") {
            this.audioManager.PlayAudio();
        }
        // Also, if it's the title page in explore mode, have to autoplay,
        // otherwise that sound never plays.
        if (description.isTitle && StorybookStateManager.GetState().storybookMode == StorybookMode.Explore) {
            Logger.Log("Playing title audio in explore mode");
            this.audioManager.PlayAudio();
        }
    }

    private void loadTitlePage(SceneDescription description) {
        // Load the into the title panel without worrying about anything except
        // for fitting the space and making the aspect ratio correct.
        // Basically the same as first half of loadImage() function.
        string imageFile = description.storyImageFile;
        GameObject newObj = new GameObject();
        newObj.AddComponent<Image>();
        newObj.AddComponent<AspectRatioFitter>();
        newObj.transform.SetParent(this.titleImagePanel.transform, false);
        newObj.transform.localPosition = Vector3.zero;
        newObj.GetComponent<AspectRatioFitter>().aspectMode =
                  AspectRatioFitter.AspectMode.FitInParent;
        newObj.GetComponent<AspectRatioFitter>().aspectRatio =
                  this.titlePanelImageAspectRatio;

        Logger.Log("loading title page");
        newObj.GetComponent<Image>().sprite = this.assetManager.GetSprite(imageFile);
        newObj.GetComponent<Image>().preserveAspect = true;
        this.storyImage = newObj;
    }

    private void loadImage(string imageFile) {
        GameObject newObj = new GameObject();
        newObj.AddComponent<Image>();
        newObj.AddComponent<AspectRatioFitter>();
        newObj.GetComponent<AspectRatioFitter>().aspectMode =
          AspectRatioFitter.AspectMode.FitInParent;

        // Set the sprite.
        Sprite imageSprite = this.assetManager.GetSprite(imageFile);
        newObj.GetComponent<Image>().sprite = imageSprite;
        newObj.GetComponent<Image>().preserveAspect = true;
        newObj.transform.SetParent(this.graphicsPanel.transform, false);
        newObj.transform.localPosition = Vector3.zero;

        Texture imageTexture = imageSprite.texture;

        // Figure out sizing so that later scene objects can be loaded relative
        // to the background image for accurate overlay.
        float imageAspectRatio = (float)imageTexture.width / (float)imageTexture.height;
        newObj.GetComponent<AspectRatioFitter>().aspectRatio =
          imageAspectRatio;

        // TODO: If height is constraining factor, then use up all possible
        // width by pushing the image over, only in landscape mode though.
        // Do the symmetric thing in portrait mode if width is constraining.
        // ^ Kind of done, maybe not perfect.
        if (imageAspectRatio > this.graphicsPanelAspectRatio) {
            // Width is the constraining factor.
            this.storyImageWidth = this.graphicsPanelWidth;
            this.storyImageHeight = this.graphicsPanelWidth / imageAspectRatio;
            if (this.displayMode == DisplayMode.Portrait) {
                Logger.Log("fixing width is constraining");
                float heightDiff = this.graphicsPanelHeight - this.storyImageHeight;
                this.graphicsPanelHeight = this.storyImageHeight;
                this.graphicsPanel.GetComponent<RectTransform>().sizeDelta = 
                    new Vector2(this.storyImageWidth, this.storyImageHeight);
                Vector2 currentTextPanelSize =
                    this.textPanel.GetComponent<RectTransform>().sizeDelta;
                this.textPanel.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(currentTextPanelSize.x, currentTextPanelSize.y + heightDiff);
            }
            this.storyImageX = 0;
            this.storyImageY =
                -(this.graphicsPanelHeight - this.storyImageHeight) / 2;
        } else {
            // Height is the constraining factor.
            this.storyImageHeight = this.graphicsPanelHeight;
            this.storyImageWidth = this.graphicsPanelHeight * imageAspectRatio;
            if (this.displayMode == DisplayMode.Landscape) {
                float widthDiff = this.graphicsPanelWidth - this.storyImageWidth;
                this.graphicsPanelWidth = this.storyImageWidth;
                this.graphicsPanel.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(this.storyImageWidth, this.storyImageHeight);
                Vector2 currentTextPanelSize =
                    this.textPanel.GetComponent<RectTransform>().sizeDelta;
                this.textPanel.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(currentTextPanelSize.x + widthDiff,
                                currentTextPanelSize.y);
            }
            this.storyImageY = 0;
            this.storyImageX =
                (this.graphicsPanelWidth - this.storyImageWidth) / 2;
        }

        this.imageScaleFactor = this.storyImageWidth / imageTexture.width;
        // TODO: Not sure if should destroy object, but I think it's safer to do so, check later.
        Destroy(this.storyImage);
        this.storyImage = newObj;
    }

    // Add a new TinkerText for the given word.
    private void loadTinkerText(int index, string word, AudioTimestamp timestamp, bool isLastWord) {
        if (word.Length == 0) {
            return;
        }
		GameObject newTinkerText =
            Instantiate((GameObject)Resources.Load("Prefabs/TinkerText"));
        newTinkerText.GetComponent<TinkerText>()
             .Init(index, word, timestamp, isLastWord);
        this.tinkerTexts.Add(newTinkerText);
        // Place it correctly within the stanzas.
        this.stanzaManager.AddTinkerText(newTinkerText);
    }

    // Adds a SceneObject to the story scene.
    private void loadSceneObject(SceneObject sceneObject) {
        // Allow multiple scene objects per label as long as we believe that they are referring to
        // different objects.
        if (this.sceneObjectsLabelToId.ContainsKey(sceneObject.label)) {
            // Check for overlap.
            foreach (int existingObject in this.sceneObjectsLabelToId[sceneObject.label]) {
                if (Util.RefersToSameObject(
                        sceneObject.position,
                    this.sceneObjects[existingObject].GetComponent<SceneObjectManipulator>().position)) {
                    // Logger.Log("Detected overlap for object " + sceneObject.label);
                    return;
                }
            }
        }
        // Save this id under its label.
        if (!this.sceneObjectsLabelToId.ContainsKey(sceneObject.label)) {
            this.sceneObjectsLabelToId[sceneObject.label] = new List<int>();
        }
        this.sceneObjectsLabelToId[sceneObject.label].Add(sceneObject.id);

        GameObject newObj = 
            Instantiate((GameObject)Resources.Load("Prefabs/SceneObject"));
        newObj.transform.SetParent(this.graphicsPanel.transform, false);
        newObj.GetComponent<RectTransform>().SetAsLastSibling();
        // Set the position.
        SceneObjectManipulator manip =
            newObj.GetComponent<SceneObjectManipulator>();
        Position pos = sceneObject.position;
        manip.id = sceneObject.id;
        manip.label = sceneObject.label;
        manip.position = pos; 
        manip.MoveToPosition(
            new Vector3(this.storyImageX + pos.left * this.imageScaleFactor,
                        this.storyImageY - pos.top * this.imageScaleFactor)
        )();
        manip.ChangeSize(
            new Vector2(pos.width * this.imageScaleFactor,
                        pos.height * this.imageScaleFactor)
        )();

        // TODO: find the appropriate sprite and assign it here.
        // Sprite toadSprite = Resources.Load<Sprite>("toad_sprite");
        //manip.SetSprite(toadSprite);

        // Set the pivot.
        manip.SetPivotToCenter();
        manip.Scale(new Vector3(1.1f, 1.1f));


        // Add a dummy handler to check things.
        manip.AddClickHandler(() =>
        {
            Logger.Log("SceneObject clicked " +
                       manip.label);
        });
        // Add a click handler to send a ROS message.
        if (Constants.USE_ROS) {
            manip.AddClickHandler(
                this.rosManager.SendSceneObjectTappedAction(sceneObject.id, sceneObject.label));   
        }
        // Add additional click handlers if the scene object's label is not in the story text.
        if (!sceneObject.inText) {
            manip.AddClickHandler(() =>
            {
                Logger.Log("Not in text! " + manip.label);
                this.showPopupText(manip.label, manip.position);
            });
        }
        // Name the GameObject so we can inspect in the editor.
        newObj.name = sceneObject.label;
        this.sceneObjects[sceneObject.id] = newObj;
    }

    // Places smallest scene objects higher up in the z direction.
    // This guarantees that larger objects do not prevent smaller ones from being clickable.
    private void sortSceneObjectLayering() {
        Dictionary<int, GameObject>.KeyCollection idKeys = this.sceneObjects.Keys;
        List<int> ids = new List<int>();
        foreach (int id in idKeys) {
            ids.Add(id);
        }
        ids.Sort((id1, id2) => {
            Position pos1 = this.sceneObjects[id1].GetComponent<SceneObjectManipulator>().position;
            Position pos2 = this.sceneObjects[id2].GetComponent<SceneObjectManipulator>().position;
            return pos2.width * pos2.height - pos1.width * pos1.height;
        });
        // Now that they are in reverse sorted order, move them to the front in sequence.
        foreach (int id in ids) {
            GameObject sceneObject = this.sceneObjects[id];
            sceneObject.GetComponent<RectTransform>().SetAsLastSibling();
        }
    }


    // Sets up a trigger between TinkerTexts and SceneObjects.
    private void loadTrigger(Trigger trigger) {
        switch (trigger.type) {
        case TriggerType.CLICK_TINKERTEXT_SCENE_OBJECT:
                // It's possible this sceneObject was not added because we found that it
                // overlapped with a previous object. This is fine, just skip it.
            if (!this.sceneObjects.ContainsKey(trigger.args.sceneObjectId)) {
                return;
            }
            SceneObjectManipulator manip = 
                this.sceneObjects[trigger.args.sceneObjectId]
                    .GetComponent<SceneObjectManipulator>();
            TinkerText tinkerText = this.tinkerTexts[trigger.args.textId]
                                            .GetComponent<TinkerText>();
            Action action = manip.Highlight(Constants.SCENE_OBJECT_HIGHLIGHT_COLOR);
            tinkerText.AddClickHandler(action);
            manip.AddClickHandler(tinkerText.Highlight());
            if (!this.sceneObjectToTinkerText.ContainsKey(manip.id)) {
                this.sceneObjectToTinkerText[manip.id] = new List<int>();
            }
            this.sceneObjectToTinkerText[manip.id].Add(trigger.args.textId);
            break;
        default:
            Logger.LogError("Unknown TriggerType: " + trigger.type);
            break;
                
        }
    }
        
    private void loadTinkerTextLabelPairTriggers() {
        foreach (KeyValuePair<int, List<int>> item in this.sceneObjectToTinkerText) {
            // Need to break these into actual phrases. (Don't want Clifford Clifford).
            List<List<int>> phrases = new List<List<int>>();
            item.Value.Sort();
            int prevIndex = -2;
            foreach (int index in item.Value) {
                if (index - prevIndex > 1) {
                    // Start a new phrase.
                    phrases.Add(new List<int>());
                }
                // Add this index to latest phrase.
                phrases[phrases.Count - 1].Add(index);
                prevIndex = index;
            }
            // For each phrase, link the tinker texts.
            foreach (List<int> phraseIndexes in phrases) {
                string phrase = "";
                foreach (int index in phraseIndexes) {
                    TinkerText tt = this.tinkerTexts[index].GetComponent<TinkerText>();
                    phrase += tt.word + " ";
                }
                phrase = phrase.Substring(0, phrase.Length - 1);

                foreach (int index in phraseIndexes) {
                    TinkerText tt = this.tinkerTexts[index].GetComponent<TinkerText>();
                    tt.SetSceneObjectId(item.Key);
                    tt.SetPhraseIndexes(item.Value);
                    tt.phrase = phrase;
                    foreach (int j in item.Value) {
                        TinkerText tt_j = this.tinkerTexts[j].GetComponent<TinkerText>();
                        if (j != index) {
                            tt.AddClickHandler(tt_j.Highlight());
                        }
                    }
                }
            }
        }
    }

    // Sets up a timestamp trigger on the audio manager.
    private void loadAudioTriggers() {
        foreach (GameObject t in this.tinkerTexts) {
            TinkerText tinkerText = t.GetComponent<TinkerText>();
            this.audioManager.AddTrigger(tinkerText.audioStartTime,
                                         tinkerText.OnStartAudioTrigger,
                                         tinkerText.isFirstInStanza);
            this.audioManager.AddTrigger(
                tinkerText.triggerAudioEndTime, tinkerText.OnEndAudioTrigger);
            if (tinkerText.word == "out.") {
                Logger.Log("out!!! " + tinkerText.audioEndTime + " " + tinkerText.triggerAudioEndTime);
            }
        }
    }

    private void setUpTinkerTextRosHandlers() {
        // If we're using ROS, attach a click handler to the tinkertext so that there's a message
        // sent over ROS whenever the user taps on a word.
        foreach (GameObject obj in this.tinkerTexts) {
            TinkerText tt = obj.GetComponent<TinkerText>();
                tt.AddClickHandler(this.rosManager.SendTinkerTextTappedAction(
                    tt.GetIndex(), tt.word, tt.phrase));
        }
    }

    // Displays a popup with the provided text, knowing that the desired labeled
    // object is at the given position.
    //
    // Only show it for a short amount of time.
    private void showPopupText(string label, Position objectPosition) {
        Logger.Log("popup text!");
        this.popupLabel.GetComponent<PopupLabel>().Configure(label, objectPosition);
        this.popupLabel.SetActive(true);
        StartCoroutine(this.hidePopupText(Constants.SCENE_OBJECT_DISPLAY_TIME));
    }

    private IEnumerator hidePopupText(float secondsDelay) {
        yield return new WaitForSeconds(secondsDelay);
        this.popupLabel.SetActive(false);
    }

    // Change the vertical spacing between stanzas, and the font size of tinkertexts,
    // to react to how many words are on the page. For now, just heuristic, later
    // we can figure out the best way to fit the words on the page.
    private void resizeSpacingAndFonts(int numWordsOnPage) {
        if (numWordsOnPage > 32) {
            TinkerText.TINKER_TEXT_FONT_SIZE = 48;
            this.textPanel.GetComponent<VerticalLayoutGroup>().spacing = -50;
        } else if (numWordsOnPage > 25) {
            TinkerText.TINKER_TEXT_FONT_SIZE = 48;
            this.textPanel.GetComponent<VerticalLayoutGroup>().spacing = -20;
        } else if (numWordsOnPage< 10) {
            TinkerText.TINKER_TEXT_FONT_SIZE = 54;
            this.textPanel.GetComponent<VerticalLayoutGroup>().spacing = 0;
        } else {
            TinkerText.TINKER_TEXT_FONT_SIZE = 50;
            this.textPanel.GetComponent<VerticalLayoutGroup>().spacing = 0;
        }
    }

    // Called by GameController to change whether we autoplay o not.
    public void SetAutoplay(bool newValue) {
        this.autoplayAudio = newValue;
    } 

    // Begin playing the audio. Can be called by GameController in response
    // to UI events like button clicks or swipes.
    public void ToggleAudio() {
        this.audioManager.ToggleAudio();
    }
        
    // Called by GameController when we should remove all elements we've added
    // to this page (usually in preparation for the creation of another page).
    public void ClearPage() {
        // Destroy stanzas.
        this.stanzaManager.ClearPage();

        // Destroy TinkerText objects we have a reference to, and reset list.
        foreach (GameObject tinkertext in this.tinkerTexts) {
            Destroy(tinkertext);
        }
        this.tinkerTexts.Clear();
        // Destroy SceneObjects we have a reference to, and empty dictionary.
        foreach (KeyValuePair<int,GameObject> obj in this.sceneObjects) {
            Destroy(obj.Value);
        }
        this.sceneObjects.Clear();
        this.sceneObjectsLabelToId.Clear();
        this.sceneObjectToTinkerText.Clear();
        // Remove all images.
        if (this.storyImage != null) {
            Destroy(this.storyImage.gameObject);
            this.storyImage = null;   
        }
        // Remove audio triggers.
        this.audioManager.ClearTriggersAndReset();
    }

    // Based on the image and orientation, determine an aspect ratio and decide the display mode.
    private void setDisplayModeFromSceneDescription(SceneDescription description) {
        if (description.isTitle) {
            if (description.orientation == ScreenOrientation.Landscape) {
                this.setDisplayMode(DisplayMode.Landscape);
            } else {
                this.setDisplayMode(DisplayMode.Portrait);
            }
        } else {
            if (description.orientation == ScreenOrientation.Landscape) {
                // Need to look at aspect ratio to decide between Landscape and LandscapeWide.
                Texture texture = this.assetManager.GetSprite(description.storyImageFile).texture;
//                // TODO: this is where we set the display mode.
//                // For now, just only allow landscape, because otherwise sentences are too
//                // tall and the text takes up too much space.
//                float aspectRatio = (float)texture.width / (float)texture.height;
//                if (aspectRatio > 2.0) {
//                    this.setDisplayMode(DisplayMode.LandscapeWide);
//                } else {
//                    this.setDisplayMode(DisplayMode.Landscape);
//                }
                this.setDisplayMode((DisplayMode.Landscape));
            } else if (description.orientation == ScreenOrientation.Portrait) {
                this.setDisplayMode(DisplayMode.Portrait);
            } else {
                // If it's something else, then idk, put it as DisplayMode.Landscape as default.
                this.setDisplayMode(DisplayMode.Landscape);
            }
        }
    }

    // Update the display mode. We need to update our internal references to
    // textPanel and graphicsPanel.
    private void setDisplayMode(DisplayMode newMode) {
        if (this.displayMode != newMode) {
            this.displayMode = newMode;
            if (this.graphicsPanel != null) {
                this.graphicsPanel.SetActive(false);
                this.textPanel.SetActive(false);
                this.titleImagePanel.SetActive(false);
            }
            switch (this.displayMode)
            {
            case DisplayMode.Landscape:
                this.graphicsPanel = this.landscapeGraphicsPanel;
                this.textPanel = this.landscapeTextPanel;
                this.openBookBackground = this.landscapeOpenBookBackground;
                this.titleImagePanel = this.landscapeTitleImagePanel;
                this.titlePanel = this.landscapeTitlePanel;
                this.endPagePanel = this.landscapeEndPagePanel;
                this.readerPanel = this.landscapeReaderPanel;
                this.confettiObject = this.landscapeConfettiObject;
                break;
            case DisplayMode.LandscapeWide:
                this.graphicsPanel = this.landscapeWideGraphicsPanel;
                this.textPanel = this.landscapeWideTextPanel;
                this.openBookBackground = this.landscapeOpenBookBackground;
                this.titleImagePanel = this.landscapeTitleImagePanel;
                this.titlePanel = this.landscapeTitlePanel;
                this.endPagePanel = this.landscapeEndPagePanel;
                this.readerPanel = this.landscapeReaderPanel;
                this.confettiObject = this.landscapeConfettiObject;
                break;
            case DisplayMode.Portrait:
                this.graphicsPanel = this.portraitGraphicsPanel;
                this.textPanel = this.portraitTextPanel;
                this.openBookBackground = this.portraitOpenBookBackground;
                this.titleImagePanel = this.portraitTitleImagePanel;
                this.titlePanel = this.portraitTitlePanel;
                this.endPagePanel = this.portraitEndPagePanel;
                this.readerPanel = this.portraitReaderPanel;
                this.confettiObject = this.portraitConfettiObject;
                // Resize back to normal.
                this.graphicsPanel.GetComponent<RectTransform>().sizeDelta =
                        new Vector2(this.PORTRAIT_WIDTH,
                                    this.PORTRAIT_GRAPHICS_HEIGHT);
                break;
            default:
                Logger.LogError("unknown display mode " + newMode);
                break;
            }
            this.graphicsPanel.SetActive(true);
            this.textPanel.SetActive(true);
            this.titleImagePanel.SetActive(true);
            Vector2 rect =
                this.graphicsPanel.GetComponent<RectTransform>().sizeDelta;
            this.graphicsPanelWidth = rect.x;
            this.graphicsPanelHeight = rect.y;
            this.graphicsPanelAspectRatio =
                this.graphicsPanelWidth / this.graphicsPanelHeight;
            Logger.Log("width: " + graphicsPanelWidth + " height: " + graphicsPanelHeight);
            rect = this.titleImagePanel.GetComponent<RectTransform>().sizeDelta;
            this.titlePanelImageAspectRatio = rect.x / rect.y;

            // TODO: verify this is correct.
            // Update the popup panel to have its parent be the correct graphicsPanel.
            this.popupLabel.GetComponent<RectTransform>().SetParent(
                this.graphicsPanel.GetComponent<RectTransform>());
        }
        this.stanzaManager.SetTextPanel(this.textPanel);
    }

    // Called once on startup to size the layout panels correctly. Saves the
    // new values as constants so that resetPanelSizes() can use them to
    // dynamically resize the panels between scenes.
    private void initPanelSizesOnStartup() {
        // Size of the graphics + text panel in the reader panel.
        float screenHeight =  Util.GetScreenHeight();
        float screenWidth = Util.GetScreenWidth();
        float landscapeWidth = screenWidth - 198f; // Subtract border
        float landscapeHeight = screenHeight - 480f; // Graphics panel is shorter.
        float portraitWidth = screenHeight - 161f; // Subtract border
        float portraitHeight = screenWidth - 498f; // Subtract border

        Logger.Log(landscapeWidth + " " + landscapeHeight + " " + portraitWidth + " " + portraitHeight);

        this.LANDSCAPE_GRAPHICS_WIDTH =
                Constants.LANDSCAPE_GRAPHICS_WIDTH_FRACTION * landscapeWidth;
        this.LANDSCAPE_TEXT_WIDTH = landscapeWidth - this.LANDSCAPE_GRAPHICS_WIDTH;
        this.LANDSCAPE_HEIGHT = landscapeHeight;
        Util.SetSize(
            this.landscapeGraphicsPanel,
            new Vector2(this.LANDSCAPE_GRAPHICS_WIDTH, this.LANDSCAPE_HEIGHT));
        Util.SetSize(
            this.landscapeTextPanel,
            new Vector2(this.LANDSCAPE_TEXT_WIDTH, this.LANDSCAPE_HEIGHT));

        this.PORTRAIT_GRAPHICS_HEIGHT =
                Constants.PORTRAIT_GRAPHICS_HEIGHT_FRACTION * portraitHeight;
        this.PORTRAIT_TEXT_HEIGHT = portraitHeight - this.PORTRAIT_GRAPHICS_HEIGHT;
        this.PORTRAIT_WIDTH = portraitWidth;
        Util.SetSize(
            this.portraitGraphicsPanel,
            new Vector2(this.PORTRAIT_WIDTH, this.PORTRAIT_GRAPHICS_HEIGHT));
        Util.SetSize(
            this.portraitTextPanel,
            new Vector2(this.PORTRAIT_WIDTH, this.PORTRAIT_TEXT_HEIGHT));
        
        this.LANDSCAPE_WIDE_GRAPHICS_HEIGHT =
                Constants.LANDSCAPE_WIDE_GRAPHICS_HEIGHT_FRACTION * landscapeHeight;
        this.LANDSCAPE_WIDE_TEXT_HEIGHT =
                landscapeHeight - this.LANDSCAPE_WIDE_GRAPHICS_HEIGHT;
        this.LANDSCAPE_WIDE_WIDTH = landscapeWidth;
        Util.SetSize(
            this.landscapeWideGraphicsPanel,
            new Vector2(landscapeWidth, this.LANDSCAPE_WIDE_GRAPHICS_HEIGHT));
        Util.SetSize(
            this.landscapeWideTextPanel,
            new Vector2(this.LANDSCAPE_WIDE_WIDTH, this.LANDSCAPE_WIDE_TEXT_HEIGHT));

        // And the title panels.
        // COMMENTED OUT BECAUSE THESE ARE NOW HARDCODED TO MATCH THE BOOK IMAGE ASSET.
        // Util.SetSize(this.landscapeTitleImagePanel, new Vector2(landscapeWidth, landscapeHeight));
        // Util.SetSize(this.portraitTitleImagePanel, new Vector2(portraitWidth, portraitHeight));
    }

    private void resetPanelSizes() {
        Vector2 graphicsSize = new Vector2();
        Vector2 textSize = new Vector2();
        Vector2 bookSize = new Vector2();
        switch(this.displayMode) {
        case DisplayMode.Landscape:
            graphicsSize = new Vector2(this.LANDSCAPE_GRAPHICS_WIDTH, this.LANDSCAPE_HEIGHT);
            textSize = new Vector2(this.LANDSCAPE_TEXT_WIDTH, this.LANDSCAPE_HEIGHT);
            bookSize = new Vector2(this.LANDSCAPE_TEXT_WIDTH + this.LANDSCAPE_GRAPHICS_WIDTH,
                this.LANDSCAPE_HEIGHT);
            break;
        case DisplayMode.Portrait:
            graphicsSize = new Vector2(this.PORTRAIT_WIDTH, this.PORTRAIT_GRAPHICS_HEIGHT);
            textSize = new Vector2(this.PORTRAIT_WIDTH, this.PORTRAIT_TEXT_HEIGHT);
            bookSize = new Vector2(this.PORTRAIT_WIDTH,
                this.PORTRAIT_TEXT_HEIGHT + this.PORTRAIT_GRAPHICS_HEIGHT);
            break;
        case DisplayMode.LandscapeWide:
            graphicsSize = new Vector2(this.LANDSCAPE_WIDE_WIDTH,
                this.LANDSCAPE_WIDE_GRAPHICS_HEIGHT);
            textSize = new Vector2(this.LANDSCAPE_WIDE_WIDTH, this.LANDSCAPE_WIDE_TEXT_HEIGHT); // used to be this.PORTRAIT_TEXT_HEIGHT
            bookSize = new Vector2(this.LANDSCAPE_WIDE_WIDTH,
                this.LANDSCAPE_WIDE_GRAPHICS_HEIGHT + this.LANDSCAPE_WIDE_TEXT_HEIGHT);
            break;
        default:
            Logger.LogError("Unknown display mode: " + displayMode);
            break;
        }
        Util.SetSize(this.graphicsPanel, graphicsSize);
        Util.SetSize(this.textPanel, textSize);
        Util.SetSize(this.openBookBackground, bookSize + new Vector2(Constants.BOOK_EXTRA_WIDTH,
            Constants.BOOK_EXTRA_HEIGHT));
    }

}
