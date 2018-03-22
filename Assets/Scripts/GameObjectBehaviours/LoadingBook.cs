using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingBook : MonoBehaviour {

    public static int NUM_LOADING_BOOK_SPRITES = 34;
    public Image loadingBookImage;
    public RectTransform rectTransform;
    public static float SWITCH_TIME_SECONDS = .05f;
    private Sprite[] sprites;
    private Vector2[] sizes;
    private int currentSpriteIndex = 0;
    private bool keepAnimating = false;

	// Use this for initialization
	void Awake () {
        this.sprites = new Sprite[NUM_LOADING_BOOK_SPRITES];
        this.sizes = new Vector2[NUM_LOADING_BOOK_SPRITES];
        for (int i = 0; i < NUM_LOADING_BOOK_SPRITES; i++) {
            string path = "UI/loading_book/loadingBook-" + i;
            this.sprites[i] = Resources.Load<Sprite>(path);
            Texture t = this.sprites[i].texture;
            this.sizes[i] = new Vector2(t.width, t.height);
        }
	}
	
	// Update is called once per frame
	void Update () {
		
    }

    public void StartAnimating() {
        Logger.Log("Loading book start animation");
        this.keepAnimating = true;
        StartCoroutine(this.switchSprite());
    }

    public void StopAnimatingAndReset() {
        Logger.Log("Loading book stop animation and reset");
        this.keepAnimating = false;
        this.currentSpriteIndex = 0;
    }

    private IEnumerator switchSprite() {
        this.currentSpriteIndex = (this.currentSpriteIndex + 1) % this.sprites.Length;
        this.loadingBookImage.sprite = this.sprites[this.currentSpriteIndex];
        this.rectTransform.sizeDelta = this.sizes[this.currentSpriteIndex];
        yield return new WaitForSeconds(SWITCH_TIME_SECONDS);
        if (this.keepAnimating) {
            StartCoroutine(this.switchSprite());
        }
    }
}
