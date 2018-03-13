using UnityEngine;
using System.Collections.Generic;
using System;

// AudioManager is given a single audio file and timestamp file, and is able
// to support operations like playing the audio between timestamps, and
// has triggers that can be triggered at specific timestamps, i.e. cause a
// particular TinkerText to light up when a certain word is said.
//
// This AudioManager script is attached to an invisible GameObject that always
// exists in the scene.
public class StoryAudioManager : MonoBehaviour {

    public struct AudioTrigger {
        public bool disallowInvokePastStop;
        public Action action;
    }
        
    // Need an AudioSource and an AudioClip to play audio.
    private string audioFileName;
    public AudioSource audioSource;
    private AudioClip audioClip;

    // TODO: Keep track of timestamped triggers in a balanced BST, which can then
    // support range search queries in O(log(n) + k) time.
    // Or to simplify, can use a sorted array and keep track of index to
    // start searching at.
    Dictionary<float, List<AudioTrigger>> triggers;
    float lastTimestamp;
    float currentTimestamp;

    float startTimestamp;
    float stopTimestamp; // If we want to stop the audio early.

	// Use this for initialization
	private void Start() {
        Logger.Log("StoryAudioManager start");
        this.resetInternalTimestamps();
        this.triggers = new Dictionary<float, List<AudioTrigger>>();
	}

    // Use Update for handling when to trigger actions in other objects.
	private void Update() {
        if (this.audioSource.isPlaying) {
            // Check our current timestamp, and compare against timestamps of
            // the triggers we have, in order to cause specific actions to happen.
            this.currentTimestamp = this.audioSource.time;
            float maxCutoffTime = this.currentTimestamp;
            float minCutoffTime = Math.Max(this.lastTimestamp, this.startTimestamp);
            // Watch for special case where the audio has finished and we need to
            // make sure we call any outstanding triggers.
            if (this.currentTimestamp < this.lastTimestamp) {
                maxCutoffTime = float.MaxValue;
            }
            foreach (KeyValuePair<float, List<AudioTrigger>> trigger in this.triggers) {
                // TODO: need a special case for first one? but not for hungry toad
                if (trigger.Key >= minCutoffTime &&
                    trigger.Key <= maxCutoffTime) {
                    // Invoke this trigger's action.
                    foreach (AudioTrigger t in trigger.Value) {
                        if (!t.disallowInvokePastStop) {
                            t.action();
                        } else {
                            // Only invoke if current time has not past stop time.
                            if (this.currentTimestamp <= this.stopTimestamp) {
                                t.action();
                            } else {
                                Logger.Log("don't do trigger action because first in stanza");
                            }
                        }
                    }
                }
            }
            if (this.currentTimestamp > this.stopTimestamp) {
                Logger.Log("stopping because current is " + this.currentTimestamp + " and stop is " + this.stopTimestamp);
                this.StopAudio();
            }
        }
        this.lastTimestamp = this.currentTimestamp;

        // Update audio state so that StorybookState ROS messages are accurate.
        bool playing = this.audioSource.isPlaying;
        StorybookStateManager.SetAudioState(playing, this.audioFileName);
	}

    // Load an audio clip as the current clip.
    public void LoadAudio(string audioFileName, AudioClip audioClip) {
        this.audioFileName = audioFileName;
        this.audioClip = audioClip;
        this.audioSource.clip = this.audioClip;
    }

    // For StoryManager to call when it's setting up the scene.
    public void AddTrigger(float timestamp, Action action, bool disallowAfterStop = false) {
        // Logger.Log("trigger timestamp " + timestamp);
        if (!this.triggers.ContainsKey(timestamp)) {
            this.triggers[timestamp] = new List<AudioTrigger>();
        }
        AudioTrigger newTrigger = new AudioTrigger();
        newTrigger.disallowInvokePastStop = disallowAfterStop;
        newTrigger.action += action;
        this.triggers[timestamp].Add(newTrigger);
    }

    public bool IsPlaying() {
        return this.audioSource.isPlaying;
    }

    // Start playing the audio (in a separate thread?).
    public void PlayAudio() {
        Logger.Log("Play Audio");
        this.StopAudio();
        this.audioSource.Play();
    }

    public void UnpauseAudio() {
        Logger.Log("Unpause Audio");
        this.audioSource.Play();
    }

    // Pause audio in a way that allows for resume.
    public void PauseAudio() {
        Logger.Log("Pause Audio");
        this.audioSource.Pause();
    }

    // Stop audio from playing (and reset timestamp back to 0).
    public void StopAudio() {
        Logger.Log("Stop Audio");
        this.audioSource.Stop();
        this.resetInternalTimestamps();
    }

    public void ToggleAudio() {
        if (this.audioSource.isPlaying) {
            this.PauseAudio();
        } else {
            this.UnpauseAudio();
        }
    }

    // Plays the audio between start seconds and end seconds.
    public void PlayInterval(float start, float end) {
        Logger.Log("Play Interval");
        this.StopAudio();
        this.audioSource.time = start; // TODO: maybe backtrack a tiny bit?
        this.stopTimestamp = end;
        this.UnpauseAudio();
    }

    // Called when StoryManager wants to transition to a new page.
    public void ClearTriggersAndReset() {
        this.triggers.Clear();
        this.resetInternalTimestamps();
    }

    private void resetInternalTimestamps() {
        this.audioSource.time = 0;
        this.lastTimestamp = float.MinValue;
        this.currentTimestamp = 0.0f;
        this.startTimestamp = 0;
        this.stopTimestamp = float.MaxValue;
    }

}
