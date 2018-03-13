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
    private float latestTimestamp;

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
            this.latestTimestamp = this.stanzas[this.stanzas.Count - 1].GetComponent<Stanza>().GetEndTimestamp();
        }
        for (int i = 0; i < this.stanzas.Count; i++) {
            Stanza stanza = this.stanzas[i].GetComponent<Stanza>();
            stanza.SetIndexInSentence(i);
            stanza.SetSentenceIndex(indexInSentences);
            if (i == 0) {
                stanza.SetSentenceTimestamps(this.earliestTimestamp, this.latestTimestamp);
            } else {
                stanza.SetSwipeable(false);
            }
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

    // Highlights the entire sentence. Used for turn taking reading interaction.
    public void Highlight(Color color) {
        foreach (GameObject stanzaObject in this.stanzas) {
            stanzaObject.GetComponent<Stanza>().Highlight(color);
        }
    }

    public void Unhighlight() {
        foreach (GameObject stanzaObject in this.stanzas) {
            stanzaObject.GetComponent<Stanza>().UnHighlight();
        }
    }

}