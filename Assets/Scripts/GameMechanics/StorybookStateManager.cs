using System;

// This singleton class manages the StorybookState that is published to the controller.
using System.Collections.Generic;


public class StorybookStateManager {

    public static StorybookStateManager instance;

    private StorybookState currentState;
    private Dictionary<string, object> rosMessageData;

    // for example this prevents it from pointing to another object.
    private readonly Object stateLock;

    public StorybookStateManager () {
        if (instance == null) {
            instance = this;
        } else {
            throw new Exception("Cannot attempt to create multiple StorybookStateManagers");
        }

        this.stateLock = new Object(); 

        // Set default values for start of interaction.
        this.currentState = new StorybookState {
            audioPlaying = false,
            audioFile = "",
            storybookMode = StorybookMode.NotReading,
            currentStory = "",
            numPages = 0,
            evaluatingStanzaIndex = -1,
        };

        this.rosMessageData = new Dictionary<string, object>();
        this.currentSate = currentStorybookState;
        this.rosMessageData.Add("audio_playing", this.currentSate.audioPlaying);
        // ROS freaks out if it gets a null value, so just fill it in with an empty string
        // if there is no provided audio file.
        string audioFile = this.currentSate.audioFile;
        if (audioFile == null) {
            audioFile = "";
        }
        this.rosMessageData.Add("audio_file", audioFile);
        this.rosMessageData.Add("storybook_mode", (int)this.currentSate.storybookMode);
        this.rosMessageData.Add("current_story", this.currentSate.currentStory);
        this.rosMessageData.Add("num_pages", this.currentSate.numPages);
        this.rosMessageData.Add("evaluating_stanza_index", this.currentSate.evaluatingStanzaIndex);
           
    }

    public StorybookState GetCurrentState() {
        // It's a struct, so it should return by value.
        return currentState;
    }

    public Dictionary<string, object> GetCurrentRosMessageData() {
        return this.rosMessageData;
    }

    // Used by StoryAudioManager to set whether an audio file is currently playing.
    public void SetAudioState(bool isPlaying, string audioFile="") {
        // Just make sure I'm not bamboozling myself.
        if (isPlaying && audioFile == "") {
            throw new Exception("Invalid audio state, if isPlaying, then must provide an audio file");
        }
        lock (stateLock) {
            this.currentState.audioPlaying = isPlaying;
            this.currentState.audioFile = audioFile;
            this.rosMessageData
        }
    }

    // Used by GameController to update the state when a storybook has been selected,
    // when the user has returned to story selection page, and what game mode is there.
    public void SetStorySelected(string storyName, int numPages, StorybookMode mode) {
        lock (stateLock) {
            this.currentState.currentStory = storyName;
            this.currentState.numPages = numPages;
            this.currentState.storybookMode = mode;
        }
    }

    // Used by GameController when user returns back to story selection, i.e. finishes
    // or prematurely exists.
    public void SetStoryExited() {
        lock (stateLock) {
            this.currentState.currentStory = "";
            this.currentState.numPages = 0;
            this.currentState.storybookMode = StorybookMode.NotReading;
        }
    }

    // TODO: when in evaluate mode, update the current stanza as the reading task progresses.
    public void SetEvaluatingStanza(int stanzaIndex) {
        this.currentState.evaluatingStanzaIndex = stanzaIndex;
    }
        
}


