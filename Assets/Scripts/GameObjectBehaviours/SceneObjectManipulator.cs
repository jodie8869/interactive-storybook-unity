using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections;

// ObjectManipulator is a script that should be added to all game objects that
// are created dynamically by the StoryManager and that will be manipulated
// during the story interaction. StoryManager can call methods on the
// ObjectManipulator to set click handlers.
//
// The public methods of SceneObjectManipulator return Actions that will serve
// as the callbacks for click handlers. These Actions can also be called
// immediately if we want to invoke the effects at the calling time.


public class SceneObjectManipulator : MonoBehaviour {

    public static bool ALLOW_CLICK = true;
    private bool allowClick = true;

    // Components that all SceneObjects should have.
    public Button button;
    public Image image;
    public RectTransform rectTransform;
    public int id { get; set; }
    public string label { get; set; }
    public Position position { get; set; }

    // Original color is clear.
    private Color unhighlightedColor = new Color(1, 1, 1, 0);

    // TODO: Add the concept of variables, so that variables can be
    // saved between scenes. This also implies that we should not Destroy
    // SceneObjects as we change pages, otherwise we lose that memory, and
    // we should have an active set per page, and activate or deactivate the
    // page, and only delete everything when we end the story.

    // UnityActions for various UI interactions (e.g. clicking).
    private UnityAction clickUnityAction;

    void Start() {
        // TODO: Add audio and animation to the prefab, then include them.

        this.clickUnityAction += () => {};

        // TODO: It looks like the after setting the listener, it's no longer good
        // to modify or add to the clickUnityAction. Not sure why.
        this.AddClickHandler(this.Highlight(Constants.SCENE_OBJECT_HIGHLIGHT_COLOR));
        this.button.onClick.AddListener(this.clickUnityAction);
    }

    void Update() {
        
    }

    public void AddClickHandler(Action action) {
        this.clickUnityAction += new UnityAction(action);
    }

    public Action Highlight(Color color) {
        return () =>
        {
            gameObject.GetComponent<Image>().color = color;
            // After some amount of time, remove highlighting.
            StartCoroutine(undoHighlight(Constants.SCENE_OBJECT_DISPLAY_TIME));
        };
    }

    private IEnumerator undoHighlight(float secondsDelay) {
        yield return new WaitForSeconds(secondsDelay);
        gameObject.GetComponent<Image>().color = this.unhighlightedColor;
    }

    public Action MoveToPosition(Vector3 localPosition) {
        return () =>
        {
            this.rectTransform.localPosition = localPosition;
            this.GetComponent<RectTransform>().SetAsLastSibling();
        };
    }

    public Action ChangeSize(Vector2 newSize) {
        return () =>
        {
            this.rectTransform.sizeDelta = newSize;
        };
    }

    public void Scale(Vector3 scaleFactor) {
        Vector3 newScale = Vector3.Scale(this.rectTransform.localScale, scaleFactor);
        this.rectTransform.localScale = newScale;
    }

    public void SetSprite(Sprite sprite) {
        this.image.sprite = sprite;
    }

    public void SetPivotToCenter() {
        this.setPivot(new Vector2(.5f, .5f));
    }

    private void setPivot(Vector2 pivot) {
        Vector2 size = this.rectTransform.rect.size;
        Vector2 deltaPivot = this.rectTransform.pivot - pivot;
        Vector3 deltaPosition = new Vector3(deltaPivot.x * size.x, deltaPivot.y * size.y);
        this.rectTransform.pivot = pivot;
        this.rectTransform.localPosition -= deltaPosition;
    }

    //public Action PlayAnimation() {
    //    return () =>
    //    {
    //        this.animation.play();
    //    };
    //}

}
