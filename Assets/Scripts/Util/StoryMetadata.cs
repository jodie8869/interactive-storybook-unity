// This file contains a simple class that represents the metadata of a story.
// GameController has a list of StoryMetadata objects, and uses them to populate the dropdown menu.

using UnityEngine;
using System.Collections;

public class StoryMetadata {
	
    public string name;
    public string humanReadableName;
    public int numPages;
    public ScreenOrientation orientation;
    public string orientationString;

    public StoryMetadata(string name, int numPages, string orientationString, string humanReadableName = null) {
        this.name = name;
        this.numPages = numPages;
        this.orientationString = orientationString;
        this.humanReadableName = humanReadableName;
        this.initFields();
    }
        
    public StoryMetadata(string jsonData) {
        JsonUtility.FromJsonOverwrite(jsonData, this);
        this.initFields();
    }

    private void initFields() {
        if (this.orientationString == "landscape") {
            this.orientation = ScreenOrientation.Landscape;
        } else if (this.orientationString == "portrait") {
            this.orientation = ScreenOrientation.Portrait;
        } else {
            Logger.LogError("Unknown orientation " + this.orientationString);
        }

        if (this.humanReadableName == null) {
            this.humanReadableName = Util.HumanReadableStoryName(this.name);
        }
    }

    public string GetName() {
        return this.name;
    }

    public string GetHumanReadableName() {
        return this.humanReadableName;
    }

    public int GetNumPages() {
        return this.numPages;
    }

    public ScreenOrientation GetOrientation() {
        return this.orientation;
    }
	
}
