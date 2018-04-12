// General utility functions that are useful throughout the app.

using System;
using UnityEngine;
using MiniJSON;
using System.Collections.Generic;

public static class Util {
    // TODO: should include comma or not? Sometimes that makes it too vertical.
    public static string[] punctuation = {";", ".", "?", "\"", "!", ","};
    public static string[] sentenceEndingPunctuation = { ".", "?", "!" };
    // Globally visible to the app, and can be changed by a simple select mode button.
    public static StorybookMode CurrentGameMode = StorybookMode.Explore;

    public static void SetGameMode(StorybookMode newMode) {
        Util.CurrentGameMode = newMode;
    }

    public static string FileNameToStoryName(string fileName) {
        return fileName.Substring(0,
            fileName.LastIndexOf("_", StringComparison.CurrentCulture)
        );
    }

    // Returns true if the given word should be the last word of a stanza,
    // such as if that word ends a phrase or sentence.
    public static bool WordShouldEndStanza(string word) {
        foreach (string p in punctuation) {
            if (word.EndsWith(p, StringComparison.CurrentCulture)) {
                return true;
            }
        }
        return false;
    }

    public static bool WordEndsSentence(string word, bool quoteOpen) {

        // Can't end sentence if quote is open.
        if (quoteOpen && !word.EndsWith("\"", StringComparison.CurrentCulture)) {
            return false;
        }

        foreach (string p in sentenceEndingPunctuation) {
            if (word.EndsWith(p, StringComparison.CurrentCulture)) {
                return true;
            }
        }
        // If the word is in quotes, check for second to last character being a period.
        // Don't include question marks and exclamation points for this, just as a heuristic
        // because those tend to be followed by a "said Freda" or something.
        if (word.Length > 1 && word[word.Length -1] == '"') {
            foreach (string p in sentenceEndingPunctuation) {
                if (word.Substring(0, word.Length - 1)
                    .EndsWith(p, StringComparison.CurrentCulture)) {
                    return true;
                }   
            }
        }
        return false;
    }

    // Returns true if the word contains no alphanumeric characters.
    public static bool WordHasNoAlphanum(string word) {
        foreach (char c in word) {
            if (char.IsLetterOrDigit(c)) {
                return false;
            }
        }
        return true;
    }

    // Given a string that is a json serialization of an array of integers, return
    // an actual array of integers.
    public static int[] ParseIntArrayFromRosMessageParams(Dictionary<string, object> input) {
        string serialized = Json.Serialize(input); // Will be something like '{"ids":[0,1]}'
        string array = serialized.Split(':')[1];
        if (array.Length < 3) {
            return new int[]{ };
        }
        array = array.Trim().Substring(1, array.Length - 3);
        string[] strings = array.Split(',');
        int[] values = new int[strings.Length];
        for (int i = 0; i < strings.Length; i++) {
            values[i] = Convert.ToInt32(strings[i]);
        }
        return values;
    }

    // Returns a vector to use as the position of the library book at given (row, col).
    public static void UpdateShelfPosition(GameObject shelf, int row) {
        int y = Constants.FIRST_SHELF_Y_VALUE - row * Constants.SHELF_Y_DIFF;
        float x = Constants.SHELF_X_VALUE;
        shelf.GetComponent<RectTransform>().anchoredPosition = new Vector3(x, y);
    }


    // Turns a positive integer (0 <= x < 100) into a 2 digit string.
    public static string TwoDigitStringFromInt(int num) {
        if (num < 0 || num > 100) {
            return "";
        }
        if (num < 10) {
            return "0" + num.ToString();
        } else {
            return num.ToString();
        }
    }

    // Gets the sprite for a particular story image file.
    public static Sprite GetStorySprite(string imageFile) {
        string storyName = Util.FileNameToStoryName(imageFile);
        string fullImagePath = "StoryPages/" + storyName + "/" + imageFile;
        return Resources.Load<Sprite>(fullImagePath);
    }


    // Turns something like "the_hungry_toad" into "The Hungry Toad"
    public static string HumanReadableStoryName(string story) {
        string[] words = story.Split('_');
        string humanReadable = "";
        foreach (string word in words) {
            if (word.Length > 1) {
                humanReadable += char.ToUpper(word[0]) + word.Substring(1);
            } else {
                humanReadable += word.ToUpper();
            }
            humanReadable += " ";
        }
        return humanReadable.TrimEnd(' ');
    }

    // Return true if the two positions (rectangles) overlap enough that we
    // think they refer to the same object. Based on a heuristic, not exact.
    public static bool RefersToSameObject(Position first, Position second) {
        // Check if the area of the rectangle of overlap is larger than 50%
        // of the area of the smaller input rectangle.
        Position leftMost = first;
        Position rightMost = second;
        if (first.left > second.left) {
            leftMost = second;
            rightMost = first;
        }
        //Logger.Log(leftMost.left + " " + rightMost.left);
        float xOverlap = Math.Max(0, (leftMost.left + leftMost.width) - rightMost.left);
        if (rightMost.left + rightMost.width < leftMost.left + rightMost.width) {
            // Special case for complete overlap (rightMost is contained in leftMost).
            //Logger.Log("x contained");
            xOverlap = rightMost.width;
        }
        Position topMost = first;
        Position bottomMost = second;
        if (first.top - first.height < second.top - second.height) {
            topMost = second;
            bottomMost = first;
        }
        //Logger.Log(topMost.top + " " + bottomMost.top);
        //Logger.Log(bottomMost.top + " " + (topMost.top - topMost.height));
        float yOverlap = Math.Max(0, bottomMost.top - (topMost.top - topMost.height));
        if (bottomMost.top - bottomMost.height > topMost.top - topMost.height) {
            // Complete overlap.
            //Logger.Log("y contained");
            yOverlap = bottomMost.height;
        }
        float overlapArea = xOverlap * yOverlap;
        float minArea = Math.Min(first.width * first.height, second.width * second.height);
        //Logger.Log("overlap min " + overlapArea + " " + minArea);

        return overlapArea / minArea > 0.5;
    }

    // Returns absolute screen width (meaning width is the larger of the two
    // values, not necessarily the horizonal one).
    public static int GetScreenWidth() {
        return Math.Max(Screen.width, Screen.height);
    }

    public static int GetScreenHeight() {
        return Math.Min(Screen.width, Screen.height);
    }

    public static void SetSize(GameObject panel, Vector2 newSize) {
        panel.GetComponent<RectTransform>().sizeDelta = newSize;
    }
}
