// This file contains constants for the interactive storybook.

using UnityEngine;
using Amazon;

public static class Constants {

    // Flags.
    public static bool LOAD_ASSETS_LOCALLY = false;
    public static bool USE_ROS = true;

    // For float comparison.
    public static float EPSILON = 1e-5f;

    // Participant information.
    public static string CHILD_NAME = "child_test";


    // Desired ratio of graphics panel width to entire landscape panel width
    // in landscape display mode.
    public static float LANDSCAPE_GRAPHICS_WIDTH_FRACTION = 850f / (850f + 1048f);
    // Desired ratio of graphics panel height to entire landscape panel height
    // in portrait display mode. Note: always running out of space, so making this smaller.
    public static float PORTRAIT_GRAPHICS_HEIGHT_FRACTION = 650f / (650f + 900f);
    // Desired ratio of graphics panel height to entire landscape panel height
    // in landscape wide display mode.
    public static float LANDSCAPE_WIDE_GRAPHICS_HEIGHT_FRACTION = 620f / (620f + 500f);

    // Extra height the book should have so that graphics panel and text panel don't cover
    // the book curved pages.
    public static float BOOK_EXTRA_WIDTH = 80f;
    public static float BOOK_EXTRA_HEIGHT = 270f;

    // UI things.
    public static Color SCENE_OBJECT_HIGHLIGHT_COLOR = new Color(0, 1, 1, 60f / 255f);
    public static float SCENE_OBJECT_DISPLAY_TIME = 2.0f;
    public static Color TINKERTEXT_CLICK_HIGHLIGHT_COLOR = Color.blue;
    public static Color TINKERTEXT_AUDIO_HIGHLIGHT_COLOR = Color.magenta;

    public static Color CHILD_READ_TEXT_COLOR = new Color(0, .5f, 0, 1);
    public static Color JIBO_READ_TEXT_COLOR = new Color(.5f, 0, .5f, 1);
    public static Color GREY_TEXT_COLOR = new Color(.5f, .5f, .5f, 1);

    // Library Panel. For now, just hardcode stuff.
    public static int NUM_LIBRARY_COLS = 4;
    public static int SHELF_X_VALUE = -80;
    public static int FIRST_SHELF_Y_VALUE = 1205;
    public static int SHELF_Y_DIFF = 473;
        

    // ROS connection information.
    public static string DEFAULT_ROSBRIDGE_IP = "192.168.1.156";
    public static string DEFAULT_ROSBRIDGE_PORT = "9090";

    // ROS topics.
    // Storybook to Roscore
    public static string STORYBOOK_EVENT_TOPIC = "/storybook_event";
    public static string STORYBOOK_EVENT_MESSAGE_TYPE = "/unity_game_msgs/StorybookEvent";
    public static string STORYBOOK_PAGE_INFO_TOPIC = "/storybook_page_info";
    public static string STORYBOOK_PAGE_INFO_MESSAGE_TYPE = "/unity_game_msgs/StorybookPageInfo";
    public static string STORYBOOK_STATE_TOPIC = "/storybook_state";
    public static string STORYBOOK_STATE_MESSAGE_TYPE = "/unity_game_msgs/StorybookState";
    // Roscore to Storybook
    public static string STORYBOOK_COMMAND_TOPIC = "/storybook_command";
    public static string STORYBOOK_COMMAND_MESSAGE_TYPE = "/unity_game_msgs/StorybookCommand";

    // Publishing rate for StorybookState messages.
    public static float STORYBOOK_STATE_PUBLISH_HZ = 3.0f;
    public static float STORYBOOK_STATE_PUBLISH_DELAY_MS = 1000.0f / STORYBOOK_STATE_PUBLISH_HZ;

    // Download URLs for story assets.
    public static string IMAGE_BASE_URL = "https://s3.amazonaws.com/storycorpus-images-without-text/images/";
    public static string AUDIO_BASE_URL = "https://s3.amazonaws.com/storycorpus-audio/";
    public static string JSON_BASE_URL = "https://s3.amazonaws.com/storycorpus-interactive-storybook-json/";

    // Amazon S3 Information
    public static string COGNITO_IDENTITY_REGION = RegionEndpoint.USEast1.SystemName;
    public static string S3_REGION = RegionEndpoint.USEast1.SystemName;
    public static string IDENTITY_POOL_ID = "us-east-1:f54fdb54-0d47-4b18-b3e3-59d9d19e3fe3";
    public static string S3_JSON_BUCKET = "storycorpus-interactive-storybook-json";
    public static string S3_IMAGES_BUCKET = "storycorpus-images-without-text";
    public static string S3_AUDIO_BUCKET = "storycorpus-audio";
    public static string S3_CHILD_AUDIO_BUCKET = "storybook-collected-child-audio";
    public static string S3_STORY_METADATA_BUCKET = "storybook-story-metadata";
}

// Display Modes.
// Related to ScreenOrientation but also deals with layout of the scene.
public enum DisplayMode {
    LandscapeWide,
    Landscape,
    Portrait
};

// Game Modes.
// Determines whether or not the tablet should autoplay the audio, if we should be evaluating
// the child's speech, if graphics/words are touchable, etc.
public enum StorybookMode {
    NotReading, // A storybook has not been selected yet.
    Explore, // No evaluation, just ask what is this, what is that?
    Evaluate, // Robot prompts child to read, does evaluation, asks questions.
}
