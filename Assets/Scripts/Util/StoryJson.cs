// Simple class that just stores the json for a particular story page.
// After AssetManager downloads json files, it stores them instances of this class.
// We need the name field so we can sort them in order to be sure we load them correctly.
// This class is basically the same as TextAsset, but TextAsset.text isn't writeable so I made this.

using UnityEngine;
using System.Collections;

public class StoryJson {
    private string name;
    private string text;

    public StoryJson(string name, string text) {
        this.name = name;
        this.text = text;
    }

    public string GetName() {
        return this.name;
    }

    public string GetText() {
        return this.text;
    }
}
