/* 
 * This file defines helper structs and enums for building and interpreting ROS messages.
 *
 * IMPORTANT:
 *
 * They need to be manually kept consistent with the unity_game_controllers/msgs/*.msg files,
 * otherwise messages will be malformatted and ROS will not send them (and since we are using
 * rosbridge, these failures will be silent, which, to quote our President, is BAD).
 *
 */

// Messages from the storybook to the controller.
public enum StorybookEventType {
    HELLO_WORLD = 0,
    SPEECH_ACE_RESULT = 1, // Message is json string representing entire SpeechACE result.
    WORD_TAPPED = 2, // Message is {index: int, word: string, phrase: string} of the tinkertext.
    SCENE_OBJECT_TAPPED = 3, // Message is {id: int, label: string} of the scene object.
    SENTENCE_SWIPED = 4, // Message is {index: int, text: string} of the stanza.
    RECORD_AUDIO_COMPLETE = 5, // Message is index of sentence. 
    STORY_SELECTED = 6, // Message is {needs_download: bool}.
    STORY_LOADED = 7, // Message is {continue_midway: bool}.
    CHANGE_MODE = 8, // Message is {mode: int}
    REPEAT_END_PAGE_QUESTION = 9, // Message is empty.
    END_STORY = 10, // Message is empty. Happens in explore mode when we reach "The End" page.
    RETURN_TO_LIBRARY_EARLY= 11, // Message is empty.
}

// Messages coming from the controller to the storybook.
// We will need to deal with each one by registering a handler.
public enum StorybookCommand {
    HELLO_WORLD_ACK = 0, // No params.
    HIGHLIGHT_WORD = 1, // Params is {indexes: [int]}, words to highlight.
    HIGHLIGHT_SCENE_OBJECT = 2, // Params is {ids: [int]}, scene object to highlight.
    SHOW_NEXT_SENTENCE = 3, // Params is {index: int, child_turn: bool, record: bool}.
    BEGIN_RECORD = 4, // Params is empty. Start the recording without reshowing sentence.
    CANCEL_RECORD = 5, // Stop and discard the recording. Params is empty.
    GO_TO_PAGE = 6, // Params is {page_number: int}. Used for starting story midway through.
    NEXT_PAGE = 7, // Params is empty.
    GO_TO_END_PAGE = 8, // Params is empty.
    SHOW_LIBRARY_PANEL = 9, // Params is empty.
    HIGHLIGHT_ALL_SENTENCES = 10, // Params is empty.
}

// Message type representing the high level state of the storybook, to be published at 10Hz.
public struct StorybookState {
    public bool audioPlaying; // Is an audio file playing?
    public string audioFile; // Name of the audio file that's playing, if there is one.

    public StorybookMode storybookMode; // See Constants.StorybookMode.

    public string currentStory;
    public int numPages;
     
    public int evaluatingSentenceIndex; // If in Evaluate mode, this will be which sentence we're on.
}

// Message type representing which page of the storybook is currently active.
public struct StorybookPageInfo {
    public string storyName;
    public int pageNumber; // 0-indexed, where 0 is the title page.
    public string[] sentences;
    public StorybookSceneObject[] sceneObjects;
    public StorybookTinkerText[] tinkerTexts;
}

// To be nested inside of StorybookPageInfo.
// Represents info about a scene object on the page.
public struct StorybookSceneObject {
    public int id;
    public string label;
    public bool inText;
}

// To be nested inside of StorybookPageInfo.
// Represents info about a tinkertext on the page.
public struct StorybookTinkerText {
    public bool hasSceneObject;
    public int sceneObjectId;
    public string word;
}
    