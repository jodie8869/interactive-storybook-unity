using System.Threading;
using UnityEngine.Events;
using UnityEngine;
using System;
using System.Collections;

// A Stanza is a sequence of TinkerTexts.
// Stanza is mostly a structural concept used to organize TinkerTexts.
// It has a reference to the audio, so that it is able to support operations
// such as playAudio when it is swiped.
// This Stanza script is automatically attached to each stanza object.
public class Stanza : MonoBehaviour {

    public static bool ALLOW_SWIPE;
    public static float STANZA_HEIGHT = 165; // Matches prefab.
    public static float ANIMATE_IN_SPEED = 1.0f;

    public GameObject stanzaPanel;
    private RectTransform rect;
    private CanvasGroup canvasGroup;

    private StoryAudioManager audioManager;

    // Know boundaries of the stanza.
    private Vector2 textPanelPos; // Parent text panel.
    private float leftX;
    private float topY;
    private float bottomY;

    // Detect swipe positions.
    private Vector2 mouseDownPos;
    private Vector2 mouseUpPos;

    private float startTimestamp;
    private float endTimestamp;
    private float sentenceStartTimestamp;
    private float sentenceEndTimestamp;

    // Know which sentence this stanza is a part of.
    // Index should be into the stanza manager's list of sentences.
    private int sentenceIndex;
    private int indexInSentence;

    public bool specificStanzaAllowSwipe;
    public int index;

    private UnityAction swipeUnityAction;

    private void Awake() {
        this.stanzaPanel = gameObject;
        this.rect = this.stanzaPanel.GetComponent<RectTransform>();
        this.canvasGroup = this.stanzaPanel.GetComponent<CanvasGroup>();
        Stanza.ALLOW_SWIPE = true;
        this.specificStanzaAllowSwipe = true;

        this.swipeUnityAction += () => {};
    }

    void Update() {
        // Check for swipes, start the audio for this stanza if swiped.
        if (Stanza.ALLOW_SWIPE && this.specificStanzaAllowSwipe) {
            if (Input.GetMouseButtonDown(0)) {
                this.mouseDownPos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0)) {
                this.mouseUpPos = Input.mousePosition;

            }
            if (this.stanzaWasSwiped()) {
                // Delay for a short while.
                Thread.Sleep(200);
                // this.PlayStanza();
                this.PlaySentence();
                // Reset positions so we don't keep trying to play audio.
                this.mouseDownPos = new Vector2(0, 0);
                this.mouseUpPos = new Vector2(0, 0);
                this.swipeUnityAction.Invoke();
            }
        }
    }

    public void Init(StoryAudioManager audio, Vector2 textPanelPos) {
        this.audioManager = audio;
        this.textPanelPos = textPanelPos;
    }


    public float GetStartTimestamp() {
        return this.startTimestamp;
    }

    public float GetEndTimestamp() {
        return this.endTimestamp;
    }

    public float GetEndTimestampNoModification() {
        GameObject stanzaObject = this.gameObject;
        RectTransform rectTransform = stanzaObject.GetComponent<RectTransform>();
        if (rectTransform.childCount > 0) {
            GameObject tinkerTextObject = rectTransform.GetChild(rectTransform.childCount - 1).gameObject;
            return tinkerTextObject.GetComponent<TinkerText>().triggerAudioEndTime;
        } else {
            return -1f;
        }
    }

    public int GetSentenceIndex() {
        return this.sentenceIndex;
    }

    public int GetIndexInSentence() {
        return this.indexInSentence;
    }

    // Which stanza this is within its sentence.
    public void SetIndexInSentence(int index) {
        this.indexInSentence = index;
    }

    // Which sentence this stanza belongs to.
    public void SetSentenceIndex(int index) {
        this.sentenceIndex = index;
    }
        
    public void SetStartTimestamp(float start) {
        this.startTimestamp = start;
    }

    public void SetEndTimestamp(float end) {
        this.endTimestamp = end;
    }

    public void SetSentenceTimestamps(float start, float end) {
        this.sentenceStartTimestamp = start;
        this.sentenceEndTimestamp = end;
    }

    public void SetSwipeable(bool swipeable) {
        this.specificStanzaAllowSwipe = swipeable;
    }

    public void PlayStanza() {
        this.audioManager.PlayInterval(this.startTimestamp, this.endTimestamp);
    }

    public void PlaySentence() {
        Logger.Log("Playing sentence " + this.sentenceIndex + " " + this.sentenceStartTimestamp + " " + this.sentenceEndTimestamp);
        this.audioManager.PlayInterval(this.sentenceStartTimestamp, this.sentenceEndTimestamp);
    }

    public void AddSwipeHandler(Action action) {
        this.swipeUnityAction += new UnityAction(action);
    }

    public void FadeIn(Color color) {
        this.Show();
        this.ChangeTextColor(color);
        StartCoroutine(this.fadeInStanza());
    }

    // Show and Hide are used when we only want to have the stanzas appear one at a time.
    public void Show() {
        this.gameObject.SetActive(true);
    }

    private IEnumerator fadeInStanza() {
        while (this.canvasGroup.alpha < 1) {
            canvasGroup.alpha += Time.deltaTime * ANIMATE_IN_SPEED;
            yield return null;
        }
        yield return null;
    }

    public void Hide() {
        this.gameObject.SetActive(false);
        this.canvasGroup.alpha = 0;
    }

    // Highlight the entire stanza with a given color.
    public void ChangeTextColor(Color color) {
        GameObject stanzaObject = this.gameObject;
        RectTransform rectTransform = stanzaObject.GetComponent<RectTransform>();
        for (int j = 0; j < rectTransform.childCount; j++) {
            GameObject tinkerTextObject = rectTransform.GetChild(j).gameObject;
            tinkerTextObject.GetComponent<TinkerText>().ChangeTextColor(color);
        }
    }

    // Return stanza to black.
    public void UnHighlight() {
        GameObject stanzaObject = this.gameObject;
        RectTransform rectTransform = stanzaObject.GetComponent<RectTransform>();
        for (int j = 0; j < rectTransform.childCount; j++) {
            GameObject tinkerTextObject = rectTransform.GetChild(j).gameObject;
            tinkerTextObject.GetComponent<TinkerText>().ChangeTextColor(Color.black);
        }
    }

    public string GetStanzaText() {
        GameObject stanzaObject = this.gameObject;
        RectTransform rectTransform = stanzaObject.GetComponent<RectTransform>();
        string text = "";
        for (int j = 0; j < rectTransform.childCount; j++) {
            GameObject tinkerTextObject = rectTransform.GetChild(j).gameObject;
            text += tinkerTextObject.GetComponent<TinkerText>().word + " ";
        }
        // Cut off the last space.
        text = text.Substring(0, text.Length - 1);
        return text;
    }

    public string GetLastWord() {
        GameObject stanzaObject = this.gameObject;
        RectTransform rectTransform = stanzaObject.GetComponent<RectTransform>();
        if (rectTransform.childCount > 0) {
            return rectTransform.GetChild(rectTransform.childCount - 1).GetComponent<TinkerText>().word;
        } else {
            return "";
        }
    }

    private bool stanzaWasSwiped() {
        // Special case for first time.
        if (this.topY.Equals(0)) {
            Vector2 pos = this.GetComponent<RectTransform>().position;
            Vector2 size = this.GetComponent<RectTransform>().sizeDelta;
            this.leftX = pos.x - size.x / 2.0f;
            this.topY = pos.y + size.y / 2.0f;
            this.bottomY = pos.y - size.y / 2.0f;

            // Give some leeway because children's swiping will be imprecise.
            // Just make sure there's no chance of overlapping multiple stanzas.
            this.topY += 7f;
            this.bottomY -= 7f;

            // Logger.Log(this.leftX + " " + this.topY + " " + this.bottomY);
        }

        // Both mouse down and mouse up must be within vertical range of stanza.
        if (this.mouseDownPos.y < this.bottomY || this.mouseDownPos.y > this.topY ||
            this.mouseUpPos.y < this.bottomY || this.mouseUpPos.y > this.topY) {
            return false;
        }
        // Swipe must be approximately level, y difference must be less than one stanza.
        if (Math.Abs(this.mouseUpPos.y - this.mouseDownPos.y) > STANZA_HEIGHT) {
            return false;
        }
        // Swipe must be from left to right and be 200 to 1200 pixels long.
        if (this.mouseUpPos.x - this.mouseDownPos.x > 1200 ||
           this.mouseUpPos.x - this.mouseDownPos.x < 200) {
            return false;
        }
        // Swipe must end within the stanza.
        if (this.mouseUpPos.x < this.leftX) {
            return false;
        }
        Logger.Log("Stanza swiped! " + this.sentenceIndex + " " + this.startTimestamp + " " + this.endTimestamp);
        return true;
    }
}
