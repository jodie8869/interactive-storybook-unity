using System;

// This singleton class manages the StorybookState that is published to the controller.
using System.Collections.Generic;


public static class StorybookStateManager {


    private static StorybookState currentState;
    private static Dictionary<string, object> rosMessageData;

    public static void Init() {
            
        // Set default values for start of interaction.
        currentState = new StorybookState {
            audioPlaying = false,
            audioFile = "",
            storybookMode = StorybookMode.NotReading,
            currentStory = "",
            numPages = 0,
            evaluatingSentenceIndex = -1,
        };
        rosMessageData = new Dictionary<string, object>();
        rosMessageData.Add("audio_playing", currentState.audioPlaying);
        rosMessageData.Add("audio_file", currentState.audioFile);
        rosMessageData.Add("storybook_mode", (int)currentState.storybookMode);
        rosMessageData.Add("current_story", currentState.currentStory);
        rosMessageData.Add("num_pages", currentState.numPages);
        rosMessageData.Add("evaluating_sentence_index", currentState.evaluatingSentenceIndex);
    }

    public static StorybookState GetState() {
        // It's a struct, so it should return by value.
        return currentState;
    }

    public static Dictionary<string, object> GetRosMessageData() {
        return new Dictionary<string, object>(rosMessageData);
    }

    // Used by StoryAudioManager to set whether an audio file is currently playing.
    public static void SetAudioState(bool isPlaying, string audioFile) {
        // Just make sure I'm not bamboozling myself.
        if (isPlaying && audioFile == "") {
            throw new Exception("Invalid audio state, if isPlaying, then must provide an audio file");
        }
        currentState.audioPlaying = isPlaying;
        currentState.audioFile = audioFile;

        rosMessageData["audio_playing"] = isPlaying;
        // Avoid null values.
        if (audioFile == null) {
            rosMessageData["audio_file"] = "";
        } else {
            rosMessageData["audio_file"] = audioFile;
        }
    }

    // Used by GameController to update the state when a storybook has been selected,
    // when the user has returned to story selection page, and what game mode is there.
    public static void SetStorySelected(string storyName, int numPages) {
        currentState.currentStory = storyName;
        currentState.numPages = numPages;
        rosMessageData["current_story"] = storyName;
        rosMessageData["num_pages"] = numPages;
    }

    public static void SetStorybookMode(StorybookMode mode) {
        currentState.storybookMode = mode;
        rosMessageData["storybook_mode"] = (int)mode;
    }

    // Used by GameController when user returns back to story selection, i.e. finishes
    // or prematurely exists.
    public static void SetStoryExited() {
        currentState.currentStory = "";
        currentState.numPages = 0;
        currentState.storybookMode = StorybookMode.NotReading;
        rosMessageData["current_story"] = "";
        rosMessageData["num_pages"] = 0;
        rosMessageData["storybook_mode"] = (int)StorybookMode.NotReading;
    }

    // TODO: when in evaluate mode, update the current stanza as the reading task progresses.
    // Actually, might not need this, since the controller should be telling us, not vice versa.
    public static void SetEvaluatingSentence(int sentenceIndex) {
        currentState.evaluatingSentenceIndex = sentenceIndex;
        rosMessageData["evaluating_sentence_index"] = sentenceIndex;
    }
        
}


