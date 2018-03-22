using UnityEngine;
using System.Collections.Generic;

// A sentence is a wrapper around at least one stanza.
// Sentences support the play function, and are given a reference
// to the audio object.
public class Sentence {

    private List<GameObject> stanzas;
    private StoryAudioManager audio;

    // Keep track of the time interval in the audio that this sentence
    // corresponds to.
    private float earliestTimestamp;
    private float latestAudioPlayTimestamp;
    // Kind of hacky, sometimes latestAudioPlayTimestamp is set to max value for the audio,
    // but we want to also keep track of what the value was with no modification.
    private float latestTimestampNoModification;

    public Sentence(StoryAudioManager audio) {
        this.audio = audio;
        this.stanzas = new List<GameObject>();
    }

    public void AddStanza(GameObject stanza) {
        this.stanzas.Add(stanza);
    }

    // Some initialization stuff after stanzas have been added. Sets the timestamps for this
    // sentence, and lets each stanza know what index it is in the sentence.
    public void SetupAfterAddingStanzas(int indexInSentences) {
        if (this.stanzas.Count > 0) {
            this.earliestTimestamp = this.stanzas[0].GetComponent<Stanza>().GetStartTimestamp();
            this.latestAudioPlayTimestamp = this.stanzas[this.stanzas.Count - 1].GetComponent<Stanza>().GetEndTimestamp();
            this.latestTimestampNoModification = this.stanzas[this.stanzas.Count - 1].GetComponent<Stanza>().GetEndTimestampNoModification();
        }
        // Swiping on any stanza in the sentence will play that sentence starting from
        // the swiped stanza.
        for (int i = 0; i < this.stanzas.Count; i++) {
            Stanza stanza = this.stanzas[i].GetComponent<Stanza>();
            stanza.SetIndexInSentence(i);
            stanza.SetSentenceIndex(indexInSentences);
            stanza.SetSentenceTimestamps(this.stanzas[i].GetComponent<Stanza>().GetStartTimestamp(),
                this.latestAudioPlayTimestamp);
        }
    }
	
    // Get entire text of the sentence.
    public string GetSentenceText() {
        if (this.stanzas.Count > 0) {
            string text = "";
            foreach (GameObject stanza in this.stanzas) {
                text += stanza.GetComponent<Stanza>().GetStanzaText() + " ";
            }
            text = text.Substring(0, text.Length - 1); 
            return text;
        } else {
            return "";
        }
    }

    // Get the length of the sentence text.
    public float GetDuration() {
        return this.latestTimestampNoModification - this.earliestTimestamp;
    }

    public void FadeIn(Color color) {
        foreach (GameObject stanzaObject in this.stanzas) {
            stanzaObject.GetComponent<Stanza>().FadeIn(color);
        }
    }

    public void Show() {
        foreach (GameObject stanzaObject in this.stanzas) {
            stanzaObject.GetComponent<Stanza>().Show();
        }
    }

    public void Hide() {
        foreach (GameObject stanzaObject in this.stanzas) {
            stanzaObject.GetComponent<Stanza>().Hide();
        }
    }

    // Highlights the entire sentence. Used for turn taking reading interaction.
    public void ChangeTextColor(Color color) {
        foreach (GameObject stanzaObject in this.stanzas) {
            stanzaObject.GetComponent<Stanza>().ChangeTextColor(color);
        }
    }

    public void Unhighlight() {
        foreach (GameObject stanzaObject in this.stanzas) {
            stanzaObject.GetComponent<Stanza>().UnHighlight();
        }
    }

}