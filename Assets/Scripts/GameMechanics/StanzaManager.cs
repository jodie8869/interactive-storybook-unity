using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

public class StanzaManager : MonoBehaviour {

    private GameObject textPanel;
    public StoryAudioManager audioManager;
    private RosManager rosManager;

    // Constants for loading TinkerTexts.
    private float STANZA_SPACING = 20; // Matches Prefab.

    private float remainingStanzaWidth = 0;
    private bool isMidPhrase = false; // True the current stanza is part of a phrase that began in an earlier stanza
    private bool stanzaIsOnePhrase = false; // True when there is only text from one phrase in the current stanza
    private bool prevWordEndsPhrase = false; // True if the previous word ended in punctuation that should end a phrase
    private bool prevWordEndsSentence = true; // True if we should start a new sentence.
    private bool quoteOpen = false; // True if there's an unclosed quote in the current phrase.

    private List<GameObject> tinkerTextPhraseBuffer; // Store the phrase we would need to push to the next stanza if overflow
    private GameObject lastPhraseStartStanza; // The last stanza that started a phrase

    // Array of sentences where each sentence is an array of stanzas.
    private List<Sentence> sentences;

    // Dynamically created Stanzas.
    public List<GameObject> stanzas { get; private set; }

    // Use this for initialization.
    void Awake() {
        this.stanzas = new List<GameObject>();
        this.tinkerTextPhraseBuffer = new List<GameObject>();
        this.sentences = new List<Sentence>();
    }

    // Called when we transition from one page to another.
    public void ClearPage() {
        // Destroy stanzas.
        foreach (GameObject stanza in this.stanzas){
            Destroy(stanza);
        }
        this.stanzas.Clear();
        this.sentences.Clear();

        // Start with a blank slate, reset all variables.
        this.remainingStanzaWidth = 0;
        foreach (GameObject tt in this.tinkerTextPhraseBuffer) {
            Destroy(tt);
        }
        this.tinkerTextPhraseBuffer.Clear();

        this.lastPhraseStartStanza = null;
        this.isMidPhrase = false;
        this.stanzaIsOnePhrase = false;
        this.prevWordEndsPhrase = false;
        this.prevWordEndsSentence = true;
    }

    public void SetTextPanel(GameObject textPanel) {
        this.textPanel = textPanel;
    }

    public void SetRosManager(RosManager ros) {
        this.rosManager = ros;
    }

    // Bulk of the work is here, add a new TinkerText to the stanzas, following the rules:
    //  1) Don't break phrases (unless the entire thing doesn't fit in one stanza).
    //  2) Fill up each stanza as much as possible.
    // and don't forget about these requirements:
    //  1) Must tell a TinkerText if it's the first in a stanza.
    //  2) Stanzas that start mid-phrase should not be swipeable.
    //  3) Stanzas that are swipeable must have accurate start and end times for audio.
    //
    // General Logic:
    //
    // If preferred <= remaining, easy case.
    // Add the text to the stanza via the move to stanza function.
    // If it's first in stanza and if it's isMidPhrase, set this stanza to be unswipeable,
    // and if not isMidPhrase, then set the start time of this stanza, tell it that it's
    // first in stanza and change lastStartStanza to this new one.
    // (or can just always set start time since it just won't be swipeable anyway).
    // Bookkeeping: add it to a phrase, check if there's punctuation to
    // update stanzaIsOnePhrase variable.
    //
    // If preferred > remaining, need to do stuff.
    // If stanzaIsOnePhrase, then just clear the buffer, reset remaining width,
    // create a new stanza, and add the tinkertext again recursively, setting isMidPhrase.
    // If not stanzaIsOnePhrase, then move the latest phrase to this new stanza,
    // update the remaining width, update the end time of lastStartStanza, set isMidphrase
    // to false and set stanzaIsOnePhrase to true because new stanzas always have it as true,
    // and try adding the tinkertexts in the buffer recursively.
    // Finally, add the new tinkertext to the newly created stanza.
    //
    public void AddTinkerText(GameObject tinkerTextObject) {
        TinkerText tinkerText = tinkerTextObject.GetComponent<TinkerText>();
        float preferredWidth = LayoutUtility.GetPreferredWidth(
            tinkerText.text.GetComponent<RectTransform>());
        // Set the correct width.
        tinkerText.SetWidth(preferredWidth);

        // Case where we don't need to make a new stanza.
        if (preferredWidth <= this.remainingStanzaWidth && !this.prevWordEndsSentence) {
            bool firstInStanza = System.Math.Abs(
                                     this.remainingStanzaWidth - this.maxStanzaWidth()) < Constants.EPSILON;
            if (firstInStanza) {
                if (this.isMidPhrase) {
                    // Beginning a new stanza but continuing a previous phrase.
                    // TODO: commented out because stanza swipeability is now controlled by Sentence.
                    // this.currentStanza().GetComponent<Stanza>().SetSwipeable(false);
                } else {
                    // Beginning a new stanza and a new phrase.
                    this.lastPhraseStartStanza = this.currentStanza();
                }
                tinkerText.SetFirstInStanza();
                this.currentStanza().GetComponent<Stanza>().SetStartTimestamp(
                    tinkerText.audioStartTime);
            } else {
                // Check if we've started a new phrase in this stanza.
                // Only check for this if it's not the first word in the stanza,
                // otherwise we can't actually know if stanza is one phrase or not.
                if (this.prevWordEndsPhrase) {
                    this.isMidPhrase = false; // Possibly repetitive setting of this variable.
                    this.stanzaIsOnePhrase = false;
                }
            }

            // Add tinkertext to the current stanza.
            this.moveTinkerTextToStanza(tinkerTextObject, this.currentStanza());
            this.currentStanza().GetComponent<Stanza>().SetEndTimestamp(tinkerText.audioEndTime);
            this.setQuoteState(tinkerText.word);

            // Check for phrase endings.
            if (Util.WordShouldEndStanza(tinkerText.word)) {
                this.prevWordEndsPhrase = true;
                this.isMidPhrase = false;
                // Set the end timestamp of this stanza. It will be overwritten later if
                // a phrase is moved out of this stanza to the next one.
                this.lastPhraseStartStanza.GetComponent<Stanza>().SetEndTimestamp(
                    tinkerText.audioEndTime);
                // Clear the buffer.
                this.tinkerTextPhraseBuffer.Clear();
            } else {
                this.prevWordEndsPhrase = false;
                // Add tinkertext to current phrase.
                this.tinkerTextPhraseBuffer.Add(tinkerTextObject);    
            }
            // Update whether or not we should try to force a new stanza/sentence next round.
            this.prevWordEndsSentence = Util.WordEndsSentence(tinkerText.word, this.quoteOpen);
        } else {
            // Create a new stanza.
            GameObject newStanza =
                Instantiate((GameObject)Resources.Load("Prefabs/StanzaPanel"));
            newStanza.transform.SetParent(this.textPanel.transform, false);
            newStanza.GetComponent<Stanza>().Init(
                this.audioManager,
                this.textPanel.GetComponent<RectTransform>().position
            );
            // Save the new stanza.
            this.stanzas.Add(newStanza);
            // Reset remainingStanzaWidth.
            this.remainingStanzaWidth = this.maxStanzaWidth();

            // Check for quotes.
            this.setQuoteState(tinkerText.word);
            // If the previous word ended a sentence, add a new sentence and force a new stanza.
            if (this.prevWordEndsSentence) {
                this.sentences.Add(new Sentence(this.audioManager));
                Logger.Log(this.sentences.Count);
                // Force this to be false.
                this.prevWordEndsSentence = false;
            } else {
                // Update whether or not we should try to force a new stanza/sentence next round.
                this.prevWordEndsSentence = Util.WordEndsSentence(tinkerText.word, this.quoteOpen);
            }

            // Add to the latest sentence.
            this.currentSentence().AddStanza(this.currentStanza());

            if (this.stanzaIsOnePhrase && !this.prevWordEndsPhrase) {
                // Overflow but everything is one phrase, so we can't do anything.
                // We just set isMidPhrase to true so we know the next stanza is
                // a continuation of this one.
                this.isMidPhrase = true;
                this.tinkerTextPhraseBuffer.Clear();
            } else {
                // Set the end time of the previous stanza.
                if (this.lastPhraseStartStanza != null) {
                    if (this.tinkerTextPhraseBuffer.Count > 0) {
                        float endTime = this.tinkerTextPhraseBuffer[0].GetComponent<TinkerText>().audioStartTime;
                        this.lastPhraseStartStanza.GetComponent<Stanza>().SetEndTimestamp(endTime);
                    }
                }
                // Add everything from the phrase buffer, recursively.
                this.isMidPhrase = false;
                this.stanzaIsOnePhrase = true;
                // Use a dummy buffer, otherwise will be modifying the real buffer
                // while also iterating over it, which will cause problems.
                List<GameObject> buffer = new List<GameObject>();
                foreach (GameObject tt in this.tinkerTextPhraseBuffer) {
                    buffer.Add(tt);
                }
                // Set prevWordEndsStanza for the previous stanza.
                if (buffer.Count > 0) {
                    this.prevWordEndsSentence = Util.WordEndsSentence(
                        this.GetStanza(this.stanzas.Count - 2).GetLastWord(), this.quoteOpen);
                }
                foreach(GameObject tt in buffer) {
                    this.AddTinkerText(tt);
                }
                buffer.Clear();
            }

            this.prevWordEndsSentence = false;
            // Add the new TinkerText again.
            this.AddTinkerText(tinkerTextObject);
        }
    }

    // Gets the stanza component at the given index.
    public Stanza GetStanza(int index) {
        if (index < this.stanzas.Count && index >= 0) {
            return this.stanzas[index].GetComponent<Stanza>();
        } else {
            return null;
        }
    }

    // Gets the sentence at the given index.
    public Sentence GetSentence(int index) {
        if (index < this.sentences.Count && index >= 0) {
            return this.sentences[index];
        } else {
            return null;
        }
    }

    public void SetupSentences() {
        for (int i = 0; i < this.sentences.Count; i++) {
            Sentence s = this.sentences[i];
            s.SetupAfterAddingStanzas(i);
            Logger.Log("sentence text: " + s.GetSentenceText());
        }
    }

    // Should be called by StoryManager after all stanzas are loaded.
    // Attaches swipe handlers so that StorybookEvent ROS messages get sent when stanzas are swiped.
    public void SetSentenceSwipeHandlers() {
        for (int i = 0; i < this.stanzas.Count; i++) {
            Stanza stanza = this.stanzas[i].GetComponent<Stanza>();
            // TODO: consider only first stanza in the sentence needs a swipe handler.
            // Decided not to since it might confuse kids.
            int sentenceIndex = stanza.GetSentenceIndex();
            string text = this.sentences[sentenceIndex].GetSentenceText();
            stanza.AddSwipeHandler(this.rosManager.SendSentenceSwipedAction(sentenceIndex, text));
        }
    }
        
    // Returns an array of strings, where each string represents the text of a single stanza.
    public string[] GetAllStanzaTexts() {
        string[] stanzaTexts = new string[this.stanzas.Count];
        for (int i = 0; i < this.stanzas.Count; i++) {
            stanzaTexts[i] = this.stanzas[i].GetComponent<Stanza>().GetStanzaText();
        }
        return stanzaTexts;
    }

    public string[] GetAllSentenceTexts() {
        string[] sentenceTexts = new string[this.sentences.Count];
        for (int i = 0; i < this.sentences.Count; i++) {
            sentenceTexts[i] = this.sentences[i].GetSentenceText();
        }
        return sentenceTexts;
    }

    private GameObject currentStanza() {
        return this.stanzas[this.stanzas.Count - 1];
    }

    private Sentence currentSentence() {
        return this.sentences[this.sentences.Count - 1];
    }

    // Helper for keeping track of the state of if quotes have been opened and not closed.
    private void setQuoteState(string word) {
        if (word.StartsWith("\"", StringComparison.CurrentCulture)) {
            this.quoteOpen = true;
        }
        if (word.EndsWith("\"", StringComparison.CurrentCulture)) {
            this.quoteOpen = false;
        }
    }

    private float maxStanzaWidth() {
        return this.textPanel.GetComponent<RectTransform>().sizeDelta.x;
    }

    // Helper that simply moves an existing TinkerText game object into the current stanza.
    private void moveTinkerTextToStanza(GameObject tinkerText, GameObject stanza) {
        tinkerText.transform.SetParent(stanza.transform, false);
        // Subtract the width of this TinkerText and the standard stanza spacing.
        this.remainingStanzaWidth -= tinkerText.GetComponent<RectTransform>().sizeDelta.x;
        this.remainingStanzaWidth -= STANZA_SPACING;
    }

    // Update is called once per frame
    void Update() {

    }
}
