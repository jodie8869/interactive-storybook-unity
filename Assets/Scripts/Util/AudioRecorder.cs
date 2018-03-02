// A class for recording through the Android tablet microphone and saving the audio files.
// This primarily used for reading assessment. Realtime interactions such as the child asking
// for clarification on a word or something else like that will be done through Jibo ASR.

using UnityEngine;
using System;
using System.Collections;

public class AudioRecorder : MonoBehaviour {
	
    public static string BUILTIN_MICROPHONE = "Built-in Microphone";
        
    AudioClip audioClipMidRecord;

    // Use this for initialization
	void Start() {
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
        Logger.Log("start recording");
        this.audioClipMidRecord = Microphone.Start(BUILTIN_MICROPHONE, false, 30, 44100);
    }

    public void EndRecording(Action<AudioClip> callback) {
        Microphone.End(BUILTIN_MICROPHONE);
        Logger.Log("end recording with length " + this.audioClipMidRecord.length);
        callback(this.audioClipMidRecord);
    }

    public IEnumerator RecordForDuration(int seconds, Action<AudioClip> callback) {
        StartCoroutine(this.recordForDuration(seconds, callback));
        // This is so the caller can continue to execute with no delay.
        yield return null;
    }

    private IEnumerator recordForDuration(int seconds, Action<AudioClip> callback) {
        Logger.Log("begin recording for " + seconds + " seconds"); 
        this.audioClipMidRecord = Microphone.Start(BUILTIN_MICROPHONE, false, seconds, 44100);
        yield return new WaitForSeconds(seconds);
        Logger.Log("finished recording for " + seconds + " seconds");
        callback(this.audioClipMidRecord);
    }

    // The filepath argument is with respect to persistentDataPath, prefix not necessary.
    public static void SaveAudioAtPath(string filepath, AudioClip audio) {
        string path = Application.persistentDataPath + "/" + filepath;
        SavWav.Save(path, audio);
    }

    // Same filepath as passed to SaveAudioAtPath().
    public static AudioClip LoadAudioLocal(string filepath) {
        string path = Application.persistentDataPath + "/" + filepath;
        if (System.IO.File.Exists(path)) {
            byte[] audioBytes = System.IO.File.ReadAllBytes(path);
            int numBytes = audioBytes.Length - SavWav.HEADER_SIZE;
            if (numBytes % 2 != 0) {
                Logger.Log("odd number of bytes, something is wrong");
            }
            int numSamples = numBytes / 2;
            // Convert bytes to int.
            Int16[] audioInts = new Int16[numSamples];
            // Copy the bytes into the ints, excluding the first HEADER_SIZE bytes.
            Buffer.BlockCopy(audioBytes, SavWav.HEADER_SIZE, audioInts, 0, audioInts.Length);
            float[] audioFloats = new float[numSamples];
            for (int i = 0; i < numSamples; i++) {
                audioFloats[i] = (float)audioInts[i] / SavWav.RESCALE_FACTOR;
            }
            AudioClip newClip = AudioClip.Create(filepath, numSamples, 1, 44100, false);
            newClip.SetData(audioFloats, 0);
            return newClip;

        } else {
            Logger.Log("File doesn't exist, sad");
            return null;
        }
    }

}
