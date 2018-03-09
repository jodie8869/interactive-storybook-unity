using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection.Emit;
using UnityEngine.UI;

// PopupLabel is a script attached to the popup panel game object, which appears
// when the user clicks on a sceneObject whose label is not in the text.
using UnityEngine.Experimental.UIElements;


public class PopupLabel : MonoBehaviour {

    public GameObject text;
    private const int DESIRED_BORDER = 20; // In pixels, for space around the label.
    public static float POPUP_LABEL_HEIGHT = 150;

    // Called by StoryManager to position the popup and populate the text.
    public void Configure(string label, Position objectPosition) {
        this.text.GetComponent<Text>().text = label;

        // Configure the size.
        float preferredWidth = this.text.GetComponent<Text>()
            .GetComponent<RectTransform>().sizeDelta.x;
        this.text.GetComponent<RectTransform>().sizeDelta = 
            new Vector2(preferredWidth, POPUP_LABEL_HEIGHT);
        this.GetComponent<RectTransform>().sizeDelta =
            new Vector2(preferredWidth + DESIRED_BORDER,
                POPUP_LABEL_HEIGHT + DESIRED_BORDER);
        
        // TODO: Configure the position.
        // Either put it above, to the right, or to the left of the object,
        // depending on where the object is. Preference is above, below, right, left.

    }

	// Use this for initialization
	void Start() {
        Logger.Log("popup panel starting");
	}
	
	// Update is called once per frame
	void Update() {
		
	}
}
