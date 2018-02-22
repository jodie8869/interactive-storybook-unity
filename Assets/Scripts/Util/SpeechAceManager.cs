// This class is responsible for sending results to SpeechACE, processing the results, and
// updating any state in the app and sending relevant information back to the controller.

using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Security.Authentication;
using UnityEngine;
using System.Collections;
using System;

public class SpeechAceManager : MonoBehaviour {

    private string boundary;
    private string CRLF = "\r\n";

    // Use this for initialization
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    // Sends off a request to SpeechACE and deals with the result.
    // Same filename as we used to save recording in AudioRecorder.
    public IEnumerator AnalyzeTextSample(string filename, string text, Action<string> callback=null, bool block = false) {
        if (!block) {
            yield return null;
        }

        // Get the raw bytes of the audio file.
        string path = Application.persistentDataPath + "/" + filename;
        if (!File.Exists(path)) {
            Logger.Log("No such file " + path);
        }
        byte[] audioBytes = File.ReadAllBytes(path);
        StartCoroutine(this.analyzeTextSample(filename, audioBytes, text, callback));
    
        if (block) {
            yield return null;
        }
    }

    // Provide raw bytes of audio data. Filename can be anything but should correspond
    // to the name of a the audio file if we have saved it using AudioRecorder.
    public IEnumerator AnalyzeTextSample(string filename, byte[] audioData, string text, Action<string> callback=null, bool block=false) {
        // Note: need to yield return null before calling the code that does HTTP requests,
        // otherwise the calling thread will block, which is not good.
        if (!block) {
            yield return null;
        }
        StartCoroutine(this.analyzeTextSample(filename, audioData, text, callback));
        if (block) {
            yield return null;
        }
    }

    private IEnumerator analyzeTextSample(string filename, byte[] audioBytes, string text,  Action<string> callback=null) {
        // Immediately yield return null so that the calling thread doesn't block.

        // Send HTTP request.
        HttpWebRequest request = WebRequest.CreateHttp("http://api.speechace.co/api/scoring/text/v0.1/json?key=po%2Fc4gm%2Bp4KIrcoofC5QoiFHR2BTrgfUdkozmpzHFuP%2BEuoCI1sSoDFoYOCtaxj8N6Y%2BXxYpVqtvj1EeYqmXYSp%2BfgNfgoSr5urt6%2FPQzAQwieDDzlqZhWO2qFqYKslE&user_id=1234&dialect=en-us");
        request.Method = "POST";
        this.boundary = this.generateBoundary();
        request.ContentType = "multipart/form-data; boundary=" + this.boundary;

        // Write the form data. Two fields: text and user_audio_file.
        Stream requestStream = request.GetRequestStream();
        this.AddStandardFormValue(requestStream, "text", text);
        this.AddFileFormValue(requestStream, "user_audio_file", filename, audioBytes);
        // Form text ends with these special characters.
        string endForm = "--" + this.boundary + "--";
        byte[] endFormBytes = System.Text.Encoding.UTF8.GetBytes(endForm);
        requestStream.Write(endFormBytes, 0, endFormBytes.Length);
        requestStream.Close();

        Logger.Log("Sending request");
        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
        StreamReader reader = new StreamReader(response.GetResponseStream());
        Logger.Log("Got response!");
        string speechAceResult = reader.ReadToEnd();
        Logger.Log(speechAceResult);

        reader.Close();
        response.Close();

        callback?.Invoke(speechAceResult);
        yield return null;
    }

    private void AddStandardFormValue(Stream requestStream, string formFieldName, string value) {
        string formData = "--" + this.boundary + this.CRLF;
        formData += "Content-Disposition: form-data; name=\"" + formFieldName + "\"";
        formData += this.CRLF + this.CRLF;
        formData += value;
        formData += this.CRLF;

        byte[] formDataBytes = System.Text.Encoding.UTF8.GetBytes(formData);
        requestStream.Write(formDataBytes, 0, formDataBytes.Length);
    }

    private void AddFileFormValue(Stream requestStream, string formFieldName, string filename, byte[] fileData) {
        string formData = "--" + this.boundary + this.CRLF;
        formData += "Content-Disposition: form-data; name=\"" + formFieldName + "\"; ";
        formData += "filename=\"" + filename + "\"";
        formData += this.CRLF;
        formData += "Content-Type: audio/wav";
        formData += this.CRLF + this.CRLF;

        byte[] formDataBytes = System.Text.Encoding.UTF8.GetBytes(formData);
        requestStream.Write(formDataBytes, 0, formDataBytes.Length);

        // Add the audio data.
        requestStream.Write(fileData, 0, fileData.Length);

        // Add the last CRLF.
        byte[] lastCRLF = System.Text.Encoding.UTF8.GetBytes(this.CRLF);
        requestStream.Write(lastCRLF, 0, lastCRLF.Length);
    }

    private string generateBoundary() {
        return "---------------------------" + DateTime.Now.Ticks.ToString("x");
    }
}
