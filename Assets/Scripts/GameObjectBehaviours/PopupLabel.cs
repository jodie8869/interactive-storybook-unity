using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// PopupLabel is a script attached to the popup panel game object, which appears
// when the user clicks on a sceneObject whose label is not in the text.
public class PopupLabel : MonoBehaviour {

    public GameObject text;
    private Position objectPosition;

    public static float DESIRED_MARGIN = 20f;
    private const int DESIRED_PADDING = 40; // In pixels, for space around the label.
    private const float POPUP_LABEL_HEIGHT = 80;

    // Called by StoryManager to position the popup and populate the text.
    public void Configure(string label, Position objectPosition) {
        this.text.GetComponent<Text>().text = label;
        Canvas.ForceUpdateCanvases();
        // Configure the size.
        this.objectPosition = objectPosition;
        this.setSize();
    }

    private void setSize() {
        float preferredWidth = this.text.GetComponent<RectTransform>().rect.width;
//        Logger.Log("popup preferredWidth: " + preferredWidth);
        this.text.GetComponent<RectTransform>().sizeDelta = 
            new Vector2(preferredWidth, POPUP_LABEL_HEIGHT);
        this.GetComponent<RectTransform>().sizeDelta =
            new Vector2(preferredWidth + DESIRED_PADDING,
                POPUP_LABEL_HEIGHT + DESIRED_PADDING);

        // TODO: Configure the position smarter somehow? Use this.objectPosition
        // for reference on where the object is.
        // Either put it above, to the right, or to the left of the object,
        // depending on where the object is. Preference is above, below, right, left.

    }

    // Use this for initialization
    void Start() {
        Logger.Log("popup panel start");
    }

    void Update() {

    }

}
