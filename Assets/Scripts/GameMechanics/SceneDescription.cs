// SceneDescription is a serializable struct that describes a storybook scene.
//
// It contains definitions and descriptions of the objects in the scene,
// some metadata about the scene, and specifies triggers among objects.
// SceneManager takes a given SceneDescription and loads it, setting up
// necessarily colliders and handlers to enusre that the triggers occur.

// TODO: access control - adjust things so not everything is public.

using UnityEngine;
using System;
using System.IO;

// Describes the position of a scene object. Uses the same format as the
// output from our Mechanical Turk HITs.
[Serializable]
public struct Position {
    public int left;
    public int top;
    public int width;
    public int height;
}

// Describes a scene object.
// Contains the asset file to load the image from, the position to load
// the image, and an identifying name that is unique among other SceneObject
// objects in this scene.
[Serializable]
public struct SceneObject {
    public int id;
    public string label;
    // Can be empty. This means there's no sprite to load.
    public string asset;
    public Position position;
    public bool inText; // If this object corresponds to a word in the text.
}

[Serializable]
public struct AudioTimestamp {
    public float start;
    public float end;
}

[Serializable]
public enum TriggerType {
    CLICK_TINKERTEXT_SCENE_OBJECT,
}

[Serializable]
public struct TriggerArgs {
    public int textId;
    public int sceneObjectId;
    public float timestamp;
}

[Serializable]
public struct Trigger {
    public TriggerType type;
    public TriggerArgs args;
}

[Serializable]
public struct JiboPrompt {
    public string question;
    public string response;
    public string hint;
}

// SceneDescription can be serialized to and from JSON.
// This is necessary so that we can describe scenes in plaintext, so that they
// can be stored easily as JSON files and can be sent over the network.
[Serializable]
public class SceneDescription {
    public ScreenOrientation orientation; // Set when story is selected.
    public DisplayMode displayMode; // Set when a particular page's aspect ratio is inspected.

    public bool isTitle;

    // E.g. // "the_hungry_toad_01".
    public string storyImageFile;

    // All of the text. StoryManager will create TinkerText.
    public string text;

    // E.g. "the_hungry_toad_2" or "the_hungry_toad_title".
    public string audioFile;

    public AudioTimestamp[] timestamps;

    // List of scene objects to place.
    public SceneObject[] sceneObjects;

    // Triggers to coordinate connections among SceneObjects and TinkerTexts.
    public Trigger[] triggers;

    public JiboPrompt[] prompts;

    public SceneDescription() {
        // Empty constructor if no JSON file is passed.
    }

    // Constructor for SceneDescription. Takes either a file name or the raw
    // data. If a file name is given, it is just the name not the full path,
    // for example should give simply "the_hungry_toad_04".
    public SceneDescription(string jsonFileOrData, ScreenOrientation orientation, bool isData=true) {
        if (isData) {
            this.loadFromJSONData(jsonFileOrData);
        } else {
            this.loadFromJSONFile(jsonFileOrData);
        }
        this.orientation = orientation;
        // Make sure prompts is not null, let it be an empty array.
        if (this.prompts == null) {
            this.prompts = new JiboPrompt[]{};
        }
        Logger.Log("prompts are: " + this.prompts);
    }

    // Populate this SceneDescription with JSON data from the given file.
    private void loadFromJSONFile(string jsonFile) {
        string storyName = jsonFile.Substring(0,
            jsonFile.LastIndexOf("_", StringComparison.CurrentCulture)
        );
        string dataAsJson = File.ReadAllText(Application.streamingAssetsPath +
                                             "/SceneDescriptions/" + storyName +
                                             "/" + jsonFile);
		
        this.loadFromJSONData(dataAsJson);
    }

    // Populate this SceneDescription with the given JSON data.
    private void loadFromJSONData(string jsonData) {
        JsonUtility.FromJsonOverwrite(jsonData, this);
    }
}
