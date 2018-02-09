// This file contains a simple class that represents the metadata of a story.
// GameController has a list of Story objects, and uses them to populate the dropdown menu.

using UnityEngine;
using System.Collections;

public class StoryMetadata {
	
    private string name;
    private string humanReadableName;
    private int numPages;
    private ScreenOrientation orientation;

    public StoryMetadata(string name, int numPages, string orientation, string humanReadableName = null) {
        this.name = name;
        this.numPages = numPages;

        if (orientation == "landscape") {
            this.orientation = ScreenOrientation.Landscape;
        } else if (orientation == "portrait") {
            this.orientation = ScreenOrientation.Portrait;
        } else {
            Logger.LogError("Unknown orientation " + orientation);
        }

        if (humanReadableName == null) {
            this.humanReadableName = Util.HumanReadableStoryName(name);
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
