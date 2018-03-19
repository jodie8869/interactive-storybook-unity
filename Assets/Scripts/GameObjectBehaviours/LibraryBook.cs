using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Events;

public class LibraryBook : MonoBehaviour {

    public static Vector2 LIBRARY_BOOK_SIZE = new Vector2(282, 378);
    public static Vector2 LIBRARY_TITLE_IMAGE_SIZE = new Vector2(245, 325);
    public static float LIBRARY_BOOK_ENLARGE_SCALE = 1.25f;

    public GameObject libraryBook;
    public GameObject titleImage;
    private Button button;

    public StoryMetadata story {get; private set;}

    private UnityAction clickUnityAction;


	// Use this for initialization
	void Start () {
        this.button = GetComponent<Button>();

        this.clickUnityAction += () => { };
        this.button.onClick.AddListener(this.clickUnityAction);
	}
	
    public void SetStory(StoryMetadata story) {
        this.story = story;
    }

    public void SetSprite(Sprite sprite) {
        this.titleImage.GetComponent<Image>().sprite = sprite;
    }

    public void AddClickHandler(Action action) {
        this.clickUnityAction += new UnityAction(action);
    }

    // Called when the book is clicked.
    public void Enlarge() {
        this.libraryBook.GetComponent<RectTransform>().sizeDelta *= LIBRARY_BOOK_ENLARGE_SCALE;
        this.titleImage.GetComponent<RectTransform>().sizeDelta *= LIBRARY_BOOK_ENLARGE_SCALE;
    }

    public void ReturnToOriginalSize() {
        this.libraryBook.GetComponent<RectTransform>().sizeDelta = LIBRARY_BOOK_SIZE;
        this.titleImage.GetComponent<RectTransform>().sizeDelta = LIBRARY_TITLE_IMAGE_SIZE;
    }

	// Update is called once per frame
	void Update () {
		
	}
}
