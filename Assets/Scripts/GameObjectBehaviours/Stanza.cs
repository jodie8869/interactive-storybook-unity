using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;

// A Stanza is a sequence of TinkerTexts.
// Stanza is mostly a structural concept used to organize TinkerTexts.
// It has a reference to the audio, so that it is able to support operations
// such as playAudio when it is swiped.
// This Stanza script is automatically attached to each stanza object.
public class Stanza : MonoBehaviour {

    public static bool allowSwipe;

    public GameObject stanzaPanel;
    private RectTransform rect;
    private StoryAudioManager audioManager;

    // Know boundaries of the stanza.
    private Vector2 textPanelPos; // Parent text panel.
    private float leftX;
    private float topY;
    private float bottomY;

    // Detect swipe positions.
    private Vector2 mouseDownPos;
    private Vector2 mouseUpPos;

    // Know which TinkerTexts belong to this stanza.
    private int firstTinkerTextIndex; // Global index.
    private int lastTinkerTextIndex; // Global index.

    private float startTimestamp;
    private float endTimestamp;

    // Know which sentence this stanza is a part of.
    private int sentenceIndex;

    public bool specificStanzaAllowSwipe;
    public int index;

    // References to all of the TinkerText objects that belong to this stanza.
    private List<GameObject> tinkerTexts;

    private void Awake() {
        this.stanzaPanel = gameObject;
        this.rect = this.stanzaPanel.GetComponent<RectTransform>();
        Stanza.allowSwipe = true;
        this.specificStanzaAllowSwipe = true;
    }

    void Update() {
        // Check for swipes, start the audio for this stanza if swiped.
        if (Stanza.allowSwipe && this.specificStanzaAllowSwipe) {
            if (Input.GetMouseButtonDown(0)) {
                this.mouseDownPos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0)) {
                this.mouseUpPos = Input.mousePosition;

            }
            if (this.stanzaWasSwiped()) {
                // Delay for a short while.
                // TODO: not sure if this is necessary.
                Thread.Sleep(200);
                this.audioManager.PlayInterval(this.startTimestamp,
                                               this.endTimestamp);
                // Reset positions so we don't keep trying to play audio.
                this.mouseDownPos = new Vector2(0, 0);
                this.mouseUpPos = new Vector2(0, 0);
            }
        }
    }

    public void Init(StoryAudioManager audio, Vector2 textPanelPos) {
        this.audioManager = audio;
        this.textPanelPos = textPanelPos;
    }

    public void SetStartTimestamp(float start) {
        this.startTimestamp = start;
    }

    public void SetEndTimestamp(float end) {
        this.endTimestamp = end;
    }

    public void SetSwipeable(bool swipeable) {
        this.specificStanzaAllowSwipe = swipeable;
    }

    public void PlayStanza() {
        this.audioManager.PlayInterval(this.startTimestamp, this.endTimestamp);
    }

    private bool stanzaWasSwiped() {
        // Special case for first time.
        if (this.topY.Equals(0)) {
            Vector2 pos = this.GetComponent<RectTransform>().position;
            Vector2 size = this.GetComponent<RectTransform>().sizeDelta;
            this.leftX = pos.x - size.x / 2.0f;
            this.topY = pos.y + size.y / 2.0f;
            this.bottomY = pos.y - size.y / 2.0f;
            Logger.Log(this.leftX + " " + this.topY + " " + this.bottomY);
        }

        // Both mouse down and mouse up must be within vertical range of stanza.
        if (this.mouseDownPos.y < this.bottomY || this.mouseDownPos.y > this.topY ||
            this.mouseUpPos.y < this.bottomY || this.mouseUpPos.y > this.topY) {
            return false;
        }
        // Swipe must be approximately level, y difference must be small.
        if (Math.Abs(this.mouseUpPos.y - this.mouseDownPos.y) > 50) {
            return false;
        }
        // Swipe must be from left to right and be 150 to 400 pixels long.
        if (this.mouseUpPos.x - this.mouseDownPos.x > 800 ||
           this.mouseUpPos.x - this.mouseDownPos.x < 150) {
            return false;
        }
        // Swipe must end within the stanza.
        if (this.mouseUpPos.x < this.leftX) {
            return false;
        }
        Logger.Log("swiped! " + this.startTimestamp + " " + this.endTimestamp);
        return true;
    }
}
