using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections;


// TinkerText is the script added to all GameObjects dynamically created and
// that are intended to be text.
//
// A TinkerText consists of a simple text box and in the future potentially an
// associated animated gif.
//
// A TinkerText can also be paired with other sprites on the story image, and
// triggers between the TinkerText and the sprites are orchestrated by
// StoryManager via methods in TinkerText and SceneObjectManipulator. 
public class TinkerText : MonoBehaviour
{

    private int id;
    public string word;
    private float textWidth;
    public float audioStartTime, audioEndTime, triggerAudioEndTime;

    public bool isFirstInStanza;

    // Have a reference to the child objects of this TinkerText.
    public GameObject textButton;
    public GameObject text;
    public GameObject graphicPanel;
    private AnimationClip graphic;

    // UnityActions for various UI interactions (e.g. clicking).
    private UnityAction clickUnityAction;

    // Normal color should be black.
    private Color unhighlightedColor = Color.black;

    // These numbers should match the prefab, putting them here is just for
    // convenience when setting sizeDelta.
    // Height of entire TinkerText, including graphic.
    public static float TINKER_TEXT_HEIGHT = 165;
    //public static float MIN_WIDTH = 120; // Based on size of GIFs.
    // Height of the button and text components.
    public static float BUTTON_TEXT_HEIGHT = 85;
    public static float GRAPHIC_PANEL_WIDTH = 80;
    public static float GRAPHIC_PANEL_HEIGHT = 80;

    // Can set the text size depending on how many words there are.
    public static int TINKER_TEXT_FONT_SIZE = 50;

    // Set up click handler.
    void Start() {
        // It's important to do += here and not = for clickUnityAction.
        // Need to initialize otherwise will get NullReferenceException.
        this.clickUnityAction += () => { };
        this.textButton.GetComponent<Button>()
            .onClick.AddListener(this.clickUnityAction);
    }

    public int GetId() {
        return this.id;
    }

    // StoryManager will call this to give TinkerText a chance to readjust the
    // width of all the text and the textButton and the overall TinkerText.
    // Also need to set the component to active. 
    // Don't need to know anything about its position, the layout groups
    // should automatically handle that.
    public void Init(int id, string word, AudioTimestamp timestamp, bool isLastWord) {
        this.id = id;
        this.word = word;
        this.text.GetComponent<Text>().text = word;
        this.text.GetComponent<Text>().fontSize = TINKER_TEXT_FONT_SIZE;
        this.audioStartTime = timestamp.start;
        this.audioEndTime = timestamp.end;
        this.triggerAudioEndTime = timestamp.end;
        // TODO: think of a better way to handle this.
        // Kind of hacky, but this stops the audio clipping.
        // And prevents words from getting stuck on the pink highlight at the end.
        if (isLastWord) {
            this.audioEndTime = float.MaxValue;
            // Take diff into account so the subtraction doesn't make "end" occur before "start". 
            float diff = timestamp.end - timestamp.start;
            this.triggerAudioEndTime -= Math.Min(.15f, diff*.25f);
            Logger.Log("triggerAudioEndTime " + triggerAudioEndTime);
        }
        this.gameObject.SetActive(true);
        // When a TinkerText is clicked, it should highlight.
        // TODO: consider somehow making all other tinkertexts with the same text highlight?
        this.AddClickHandler(Highlight ());
    }

    public void SetFirstInStanza() {
        this.isFirstInStanza = true;
    }

    public void SetWidth(float newWidth) {
        // Update size of TinkerText.
        GetComponent<RectTransform>().sizeDelta =
            new Vector2(newWidth, TINKER_TEXT_HEIGHT);
        // Update size of the Graphics Panel.
        float newGraphicsWidth = Math.Min(GRAPHIC_PANEL_WIDTH, newWidth);
        this.graphicPanel.GetComponent<RectTransform>().sizeDelta =
                new Vector2(newGraphicsWidth, GRAPHIC_PANEL_HEIGHT);
        // Update size of Button.
        this.textButton.GetComponent<RectTransform>().sizeDelta =
            new Vector2(newWidth, BUTTON_TEXT_HEIGHT);
        this.text.GetComponent<RectTransform>().sizeDelta =
            new Vector2(newWidth, BUTTON_TEXT_HEIGHT);
        this.textWidth = newWidth;
    }

    // TODO: also increase font size or something maybe, other visual cues.
    // If we do increase font size, will need to increase the size of the object too.
    public Action Highlight() {
        return () =>
        {
            this.ChangeTextColor(Constants.TINKERTEXT_CLICK_HIGHLIGHT_COLOR);
            // After some amount of time, remove highlighting.
            StartCoroutine(undoHighlight(Constants.SCENE_OBJECT_DISPLAY_TIME));
        };
    }

    private IEnumerator undoHighlight(float secondsDelay) {
        yield return new WaitForSeconds(secondsDelay);
        this.ChangeTextColor(this.unhighlightedColor);
    }

    public void ChangeTextColor(Color color) {
        this.text.GetComponent<Text>().color = color;
    }

    // Set whether or not the TinkerText is clickable.
    // (E.g. turn off clicking when auto reading is happening, then turn back
    // on when in explore mode on the page.
    public void SetClickable(bool clickable) {
        this.textButton.GetComponent<Button>().interactable = clickable;
    }

    // Add a new action to the UnityAction click handler.
    public void AddClickHandler(Action action) {
        this.clickUnityAction += new UnityAction(action);
    }

    public void OnStartAudioTrigger() {
        // Change the text color.
        this.ChangeTextColor(Constants.TINKERTEXT_AUDIO_HIGHLIGHT_COLOR);
    }

    public void OnEndAudioTrigger() {
        this.ChangeTextColor(Color.black);
    }
}
