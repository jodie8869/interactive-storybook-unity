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
    WORD_TAPPED = 2, // Message is {index: int, word: string} of the tinkertext.
    SCENE_OBJECT_TAPPED = 3, // Message is {id: int, label: string} of the scene object.
    SENTENCE_SWIPED = 4 // Message is {index: int, text: string} of the stanza.
}

// Messages coming from the controller to the storybook.
// We will need to deal with each one by registering a handler.
public enum StorybookCommand {
    PING_TEST = 0, // No params.
    HIGHLIGHT_WORD = 1, // Params is which index word to highlight.
    HIGHLIGHT_SCENE_OBJECT = 2, // Params is which id scene object to highlight.
    HIGHLIGHT_NEXT_SENTENCE = 3 // Params is which index sentence to highlight.
}

// Message type representing the high level state of the storybook, to be published at 10Hz.
public struct StorybookState {
    public bool audioPlaying; // Is an audio file playing?
    public string audioFile; // Name of the audio file that's playing, if there is one.

    public StorybookMode storybookMode; // Three modes are NotReading, Explore, Evaluate.

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
    