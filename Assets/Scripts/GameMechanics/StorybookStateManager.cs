using System;

// This class manages the StorybookState that is published to the controller.
public class StorybookStateManager {
    
    private StorybookState currentState;
    // Lock should be readonly to prevent corruption,
    // for example this prevents it from pointing to another object.
    private readonly Object stateLock;

    public StorybookStateManager () {
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
    }

    // Used by RosManager to retrieve the current state and publish it.
    public StorybookState getCurrentState() {
        // It's a struct, so it should return by value.
        return currentState;
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


