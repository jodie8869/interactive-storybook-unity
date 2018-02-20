// This class is responsible for sending results to SpeechACE, processing the results, and
// updating any state in the app and sending relevant information back to the controller.

using System.Net;
using System.IO;
using System.Security.Authentication;
using UnityEngine;
using System.Collections;
using System;

public class SpeechAceManager : MonoBehaviour {

    private string boundary;
    private string CRLF = "\r\n";
    private WebClient webClient;

    // Use this for initialization
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    // Sends off an async request to SpeechACE and deals with the result.
    public void AnalyzeTextSample(string filepath, string text) {
        analyzeTextSample(filepath, text);
    }

    private void analyzeTextSample(string filepath, string text) {
        
        string path = Application.persistentDataPath + "/" + filepath;
        if (!File.Exists(path)) {
            Logger.Log("no such file");
        }
        byte[] audioBytes = File.ReadAllBytes(path);

        // Send async HTTP request, handles result.
        HttpWebRequest request = WebRequest.CreateHttp("http://api.speechace.co/api/scoring/text/v0.1/json?key=po%2Fc4gm%2Bp4KIrcoofC5QoiFHR2BTrgfUdkozmpzHFuP%2BEuoCI1sSoDFoYOCtaxj8N6Y%2BXxYpVqtvj1EeYqmXYSp%2BfgNfgoSr5urt6%2FPQzAQwieDDzlqZhWO2qFqYKslE&user_id=1234&dialect=en-us");
        request.Method = "POST";
        this.boundary = this.generateBoundary();
        request.ContentType = "multipart/form-data; boundary=" + this.boundary;
        Stream requestStream = request.GetRequestStream();
        this.AddStandardFormValue(requestStream, "text", text);
        this.AddFileFormValue(requestStream, "user_audio_file", filepath, audioBytes);
        string endForm = "--" + this.boundary + "--";
        byte[] endFormBytes = System.Text.Encoding.UTF8.GetBytes(endForm);
        requestStream.Write(endFormBytes, 0, endFormBytes.Length);
        requestStream.Close();

        Logger.Log(request.ToString());

        Logger.Log("about to send request");
        HttpWebResponse response = (HttpWebResponse) request.GetResponse();
        Logger.Log("got response");
        StreamReader reader = new StreamReader(response.GetResponseStream());
        Logger.Log(reader.ReadToEnd());
        reader.Close();
        response.Close();
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
