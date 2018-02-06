// StoryManager loads a scene based on a SceneDescription, including loading
// images, audio files, and drawing colliders and setting up callbacks to
// handle trigger events. StoryManager uses methods in TinkerText and
// SceneObjectManipulator for setting up these callbacks.

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class StoryManager : MonoBehaviour {

    // We may want to call methods on GameController or add to the task queue.
    public GameController gameController;
    public StoryAudioManager audioManager;
    public StanzaManager stanzaManager;

	public GameObject portraitGraphicsPanel;
    public GameObject portraitTextPanel;
    public GameObject landscapeGraphicsPanel;
    public GameObject landscapeTextPanel;
	public GameObject landscapeWideGraphicsPanel;
	public GameObject landscapeWideTextPanel;

    public GameObject portraitTitlePanel;
    public GameObject landscapeTitlePanel;

    private bool autoplayAudio = false;

    // Used for internal references.
    private GameObject graphicsPanel;
    private GameObject textPanel;
    private GameObject titlePanel;
    private GameObject currentStanza;

    private float graphicsPanelWidth;
    private float graphicsPanelHeight;
    private float graphicsPanelAspectRatio;
    private float titlePanelAspectRatio;

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

    // Store sprites and audio downloaded from the cloud.
    private Dictionary<string, bool> downloadedStories;
    private Dictionary<string, Sprite> storySprites;
    private Dictionary<string, AudioClip> storyAudios;

    // Dynamically created TinkerTexts specific to this scene.
    private List<GameObject> tinkerTexts;
    // Dynamically created SceneObjects, keyed by their id.
    private Dictionary<int, GameObject> sceneObjects;
    private Dictionary<string, List<int>> sceneObjectsLabelToId;
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


    void Start() {
        Logger.Log("StoryManager start");

        this.downloadedStories = new Dictionary<string, bool>();
        this.storySprites = new Dictionary<string, Sprite>();
        this.storyAudios = new Dictionary<string, AudioClip>();
        this.tinkerTexts = new List<GameObject>();
        this.sceneObjects = new Dictionary<int, GameObject>();
        this.sceneObjectsLabelToId = new Dictionary<string, List<int>>();

        this.stanzaManager.SetTextPanel(this.textPanel);
        this.initPanelSizesOnStartup();
    }

    void Update() {
        // Update whether or not we are accepting user interaction.
        Stanza.allowSwipe = !this.audioManager.IsPlaying();
    }

    // Main function to be called by GameController.
    // Passes in a description received over ROS or hardcoded.
    // LoadScene is responsible for loading all resources and putting them in
    // place, and attaching callbacks to created GameObjects, where these
    // callbacks involve functions from SceneManipulatorAPI.
    public void LoadPage(SceneDescription description) {
        this.setDisplayMode(description.displayMode);
        this.resetPanelSizes();

        // Load audio.
        this.loadAudio(description.audioFile);

        if (description.isTitle) {
            // Special case for title page.
            // No TinkerTexts, and image takes up a larger space.
            this.loadTitlePage(description);
        } else {
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
                                filteredTextWords.Count.ToString() + " " + 
                               description.timestamps.Length.ToString());
            }
            for (int i = 0; i < filteredTextWords.Count; i++) {
                this.loadTinkerText(filteredTextWords[i], description.timestamps[i],
                                      i == filteredTextWords.Count - 1);
            }

            // Load audio triggers for TinkerText.
            this.loadAudioTriggers();
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

        if (this.autoplayAudio) {
            this.audioManager.PlayAudio();
        }
    }

    private void loadAudio(string audioFile) {
        // TODO: Don't forget to remove this! 
        //Constants.LOAD_ASSETS_LOCALLY = true;
        if (Constants.LOAD_ASSETS_LOCALLY) {
            string storyName = Util.FileNameToStoryName(audioFile);
            this.audioManager.LoadAudio(Resources.Load("StoryAudio/" + storyName + "/" +
                                                                   audioFile) as AudioClip);
        } else {
            // Make sure that the audio is actually there.
            if (!this.storyAudios.ContainsKey(audioFile)) {
                Logger.Log("no audio file found " + audioFile);
                Logger.Log(this.storyAudios.Count);
            } else {
                this.audioManager.LoadAudio(this.storyAudios[audioFile]);
            }
        }
    }

    private Sprite getSprite(string imageFile) {
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

    private void loadTitlePage(SceneDescription description) {
        // Load the into the title panel without worrying about anything except
        // for fitting the space and making the aspect ratio correct.
        // Basically the same as first half of loadImage() function.
        string imageFile = description.storyImageFile;
        GameObject newObj = new GameObject();
        newObj.AddComponent<Image>();
        newObj.AddComponent<AspectRatioFitter>();
        newObj.transform.SetParent(this.titlePanel.transform, false);
        newObj.transform.localPosition = Vector3.zero;
        newObj.GetComponent<AspectRatioFitter>().aspectMode =
                  AspectRatioFitter.AspectMode.FitInParent;
        newObj.GetComponent<AspectRatioFitter>().aspectRatio =
                  this.titlePanelAspectRatio;

        Logger.Log("loading title page");
        newObj.GetComponent<Image>().sprite = this.getSprite(imageFile);
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
        Sprite imageSprite = this.getSprite(imageFile);
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
        if (imageAspectRatio > this.graphicsPanelAspectRatio) {
            // Width is the constraining factor.
            this.storyImageWidth = this.graphicsPanelWidth;
            this.storyImageHeight = this.graphicsPanelWidth / imageAspectRatio;
            this.storyImageX = 0;
            this.storyImageY =
                -(this.graphicsPanelHeight - this.storyImageHeight) / 2;
        } else {
            // Height is the constraining factor.
            this.storyImageHeight = this.graphicsPanelHeight;
            this.storyImageWidth = this.graphicsPanelHeight * imageAspectRatio;
            if (this.displayMode == DisplayMode.Landscape)
            {
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
    private void loadTinkerText(string word, AudioTimestamp timestamp, bool isLastWord) {
        if (word.Length == 0) {
            return;
        }
		GameObject newTinkerText =
            Instantiate((GameObject)Resources.Load("Prefabs/TinkerText"));
        newTinkerText.GetComponent<TinkerText>()
             .Init(this.tinkerTexts.Count, word, timestamp, isLastWord);
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
                    Logger.Log("Detected overlap for object " + sceneObject.label);
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
        // Add a dummy handler to check things.
        manip.AddClickHandler(() =>
        {
            Logger.Log("SceneObject clicked " +
                       manip.label);
        });
        // TODO: If sceneObject.inText is false, set up whatever behavior we
        // want for these words.
        if (!sceneObject.inText) {
            manip.AddClickHandler(() =>
            {
                Logger.Log("Not in text! " + manip.label);
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
                Action action = manip.Highlight(Constants.SceneObjectHighlightColor);
                tinkerText.AddClickHandler(action);
                manip.AddClickHandler(tinkerText.Highlight());
                break;
            default:
                Logger.LogError("Unknown TriggerType: " +
                                trigger.type.ToString());
                break;
                
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
                tinkerText.audioEndTime, tinkerText.OnEndAudioTrigger); 
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

    // Called by GameController to determine if a particular story's assets have been downloaded.
    public bool StoryHasBeenDownloaded(string storyName) {
        return this.downloadedStories.ContainsKey(storyName);
    }

    // Called by GameController after a new set of assets has been downloaded.
    // GameController should have called CheckAlreadyDownloaded before attempting to download,
    // so here we assume that these are new assets. If this happens to be false, the only cost
    // is overwriting ~15 objects in a dictionary, so it's not the worst.
    public void StoreDownloadedAssets(string storyName, Dictionary<string, Sprite> newSprites,
                                      Dictionary<string,AudioClip> newAudioClips) {
        this.downloadedStories.Add(storyName, true);
        foreach (KeyValuePair<string, Sprite> s in newSprites) {
            this.storySprites.Add(s.Key, s.Value);
        }
        foreach (KeyValuePair<string, AudioClip> a in newAudioClips) {
            this.storyAudios.Add(a.Key, a.Value);
        }
    }

    // Called by GameController when we should remove all elements we've added
    // to this page (usually in preparration for the creation of another page).
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
        // Remove all images.
        Destroy(this.storyImage.gameObject);
        this.storyImage = null;
        // Remove audio triggers.
        this.audioManager.ClearTriggersAndReset();
    }

    // Update the display mode. We need to update our internal references to
    // textPanel and graphicsPanel.
    private void setDisplayMode(DisplayMode newMode) {
        if (this.displayMode != newMode) {
            this.displayMode = newMode;
            if (this.graphicsPanel != null) {
                this.graphicsPanel.SetActive(false);
                this.textPanel.SetActive(false);
                this.titlePanel.SetActive(false);
            }
            switch (this.displayMode)
            {
                case DisplayMode.Landscape:
                    this.graphicsPanel = this.landscapeGraphicsPanel;
                    this.textPanel = this.landscapeTextPanel;
                    this.titlePanel = this.landscapeTitlePanel;
                    break;
                case DisplayMode.LandscapeWide:
                    this.graphicsPanel = this.landscapeWideGraphicsPanel;
                    this.textPanel = this.landscapeWideTextPanel;
                    this.titlePanel = this.landscapeTitlePanel;
                    break;
                case DisplayMode.Portrait:
                    this.graphicsPanel = this.portraitGraphicsPanel;
                    this.textPanel = this.portraitTextPanel;
                    this.titlePanel = this.portraitTitlePanel;
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
            this.titlePanel.SetActive(true);
            Vector2 rect =
                this.graphicsPanel.GetComponent<RectTransform>().sizeDelta;
            this.graphicsPanelWidth = (float)rect.x;
            this.graphicsPanelHeight = (float)rect.y;
            this.graphicsPanelAspectRatio =
                this.graphicsPanelWidth / this.graphicsPanelHeight;
            rect = this.titlePanel.GetComponent<RectTransform>().sizeDelta;
            this.titlePanelAspectRatio = (float)rect.x / (float)rect.y;
        }
        this.stanzaManager.SetTextPanel(this.textPanel);
    }

    // Called once on startup to size the layout panels correctly. Saves the
    // new values as constants so that resetPanelSizes() can use them to
    // dynamically resize the panels between scenes.
    private void initPanelSizesOnStartup() {
        float landscapeWidth = (float)Util.GetScreenWidth() - 100f; // Subtract border
        float landscapeHeight = (float)Util.GetScreenHeight() - 330f; // Subtract border + buttons
        float portraitWidth = (float)Util.GetScreenHeight() - 100f; // Subtract border
        float portraitHeight = (float)Util.GetScreenWidth() - 330f; // Subtract border + buttons

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
            new Vector2(this.LANDSCAPE_WIDE_WIDTH, this.LANDSCAPE_WIDE_GRAPHICS_HEIGHT));
        Util.SetSize(
            this.landscapeWideTextPanel,
            new Vector2(this.LANDSCAPE_WIDE_WIDTH, this.LANDSCAPE_WIDE_TEXT_HEIGHT));

        // And the title panels.
        Util.SetSize(this.landscapeTitlePanel, new Vector2(landscapeWidth, landscapeHeight));
        Util.SetSize(this.portraitTitlePanel, new Vector2(portraitWidth, portraitHeight));
    }

    private void resetPanelSizes() {
        Vector2 graphicsSize = new Vector2();
        Vector2 textSize = new Vector2();
        switch(this.displayMode) {
            case DisplayMode.Landscape:
                graphicsSize = new Vector2(this.LANDSCAPE_GRAPHICS_WIDTH, this.LANDSCAPE_HEIGHT);
                textSize = new Vector2(this.LANDSCAPE_TEXT_WIDTH, this.LANDSCAPE_HEIGHT);
                break;
            case DisplayMode.Portrait:
                graphicsSize = new Vector2(this.PORTRAIT_WIDTH, this.PORTRAIT_GRAPHICS_HEIGHT);
                textSize = new Vector2(this.PORTRAIT_WIDTH, this.PORTRAIT_TEXT_HEIGHT);
                break;
            case DisplayMode.LandscapeWide:
                graphicsSize = new Vector2(this.LANDSCAPE_WIDE_WIDTH,
                                           this.LANDSCAPE_WIDE_GRAPHICS_HEIGHT);
                textSize = new Vector2(this.LANDSCAPE_WIDE_WIDTH, this.PORTRAIT_TEXT_HEIGHT);
                break;
            default:
                Logger.LogError("Unknown display mode: " + displayMode.ToString());
                break;
        }
        Util.SetSize(this.graphicsPanel, graphicsSize);
        Util.SetSize(this.textPanel, textSize);
    }

}
