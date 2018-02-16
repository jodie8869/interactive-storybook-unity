// A class for recording through the Android tablet microphone and saving the audio files.
// This primarily used for reading assessment. Realtime interactions such as the child asking
// for clarification on a word or something else like that will be done through Jibo ASR.

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class AudioRecorder : MonoBehaviour {
	
    public static string BUILTIN_MICROPHONE = "Built-in Microphone";
        
    AudioClip audioClipMidRecord;

    // Use this for initialization
	void Start() {
        Logger.Log(Microphone.devices);
        for (int i = 0; i < Microphone.devices.Length; i++) {
            Logger.Log(Microphone.devices[i]);
        }
	}

	// Update is called once per frame
	void Update() {
			
	}

    // Caller should call EndRecording() to stop the recording.
    public void StartRecording() {
        // Pass null as the device name to use the default microphone. 
        this.audioClipMidRecord = Microphone.Start(BUILTIN_MICROPHONE, false, 30, 44100);
    }

    public void EndRecording(Action<AudioClip> callback) {
        Microphone.End(BUILTIN_MICROPHONE);
        Logger.Log(this.audioClipMidRecord.length);
        callback(this.audioClipMidRecord);
    }

    public IEnumerator RecordForDuration(int seconds, Action<AudioClip> callback) {
        StartCoroutine(this.recordForDuration(seconds, callback));
        // This is so the caller can continue to execute with no delay.
        yield return null;
    }

    private IEnumerator recordForDuration(int seconds, Action<AudioClip> callback) {
        this.audioClipMidRecord = Microphone.Start(BUILTIN_MICROPHONE, false, seconds, 44100);
        yield return new WaitForSeconds(seconds);
        callback(this.audioClipMidRecord);
    }

    public void RecordAndSave(int seconds) {
        
    }

    public void EndRecordingAndSave() {
        
    }

    // The filepath argument is with respect to persistentDataPath, prefix not necessary.
    public void SaveAudioAtPath(string filepath, AudioClip audio) {
        SavWav.Save(Application.persistentDataPath + "/" + filepath, audio);


        // Try loading it back.
        this.LoadAudioLocal(filepath, (AudioClip loadedClip) => {
            Logger.Log("In callback");
            // TODO: pass to StoryAudioManager and make it play back to test.
        });

    }

    // Same filepath as passed to SaveAudioAtPath().
    IEnumerator LoadAudioLocal(string filepath, Action<AudioClip> callback) {
        string path = Application.persistentDataPath + "/" + filepath;
        if (System.IO.File.Exists(path)) {
            WWW www = new WWW(path);
            yield return www;
            AudioClip loadedAudio = www.GetAudioClip();
            Logger.Log("Possibly loaded audio?");
            callback(loadedAudio);
        } else {
            Logger.Log("File doesn't exist");
            yield return null;
        }
    }
}
