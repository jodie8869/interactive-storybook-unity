// This class is a high level abstraction of dealing with Ros, to separate ROS
// logic from GameController.
//
// RosManager heavily relies on RosbridgeUtilities and RosbridgeWebSocketClient.

using UnityEngine;
using System;
using System.Threading;
using System.Collections.Generic;
using MiniJSON;

public class RosManager {

    public GameObject testObject; // For UI feedback when things happen.

    private GameController gameController; // Keep a reference to the game controller.
    private RosbridgeWebSocketClient rosClient;
    // TODO: note that for now only one handler can be registered per command.
    private Dictionary<StorybookCommand, Action<Dictionary<string, object>>> commandHandlers;
    private bool connected;

    System.Timers.Timer publishStateTimer =
        new System.Timers.Timer(Constants.STORYBOOK_STATE_PUBLISH_DELAY_MS);

    // Constructor.
    public RosManager(string rosIP, string portNum, GameController gameController) {
        Logger.Log("RosManager constructor");
        this.gameController = gameController;

        this.rosClient = new RosbridgeWebSocketClient(rosIP, portNum);
        this.rosClient.receivedMsgEvent += this.onMessageReceived;
        this.commandHandlers = new Dictionary<StorybookCommand, Action<Dictionary<string, object>>>();
    }

    public bool Connect() {
        this.rosClient.OnReconnectSuccess(this.setupPubSub);

        if (!this.rosClient.SetupSocket()) {
            Logger.Log("Failed to set up socket");
            return false;
        }

        // Advertise ROS topic subscription/publication and set connected=true on success.
        this.setupPubSub();

        // If connection successful, begin sending state messages.
        if (this.connected) {
            Logger.Log("Starting to send state messages");
            this.publishStateTimer.Elapsed += this.sendStorybookState;
            this.publishStateTimer.Start();
        }

        return this.connected;
    }

    public bool isConnected() {
        return this.connected;
    }

    public void CloseConnection() {
        this.rosClient.CloseSocket();
        this.connected = false;
    }

    public void StopSendingStorybookState() {
        this.publishStateTimer.Stop();
    }

    // Registers a message handler for a particular command the app might receive from the controller. 
    public void RegisterHandler(StorybookCommand command, Action<Dictionary<string, object>> handler) {
        this.commandHandlers.Add(command, handler);
    }

    private void setupPubSub() {
        Logger.Log("-- Setup Pub/Sub for Ros Manager --");
        string eventPubMessage = RosbridgeUtilities.GetROSJsonAdvertiseMsg(
            Constants.STORYBOOK_EVENT_TOPIC, Constants.STORYBOOK_EVENT_MESSAGE_TYPE);
        string pageInfoPubMessage = RosbridgeUtilities.GetROSJsonAdvertiseMsg(
            Constants.STORYBOOK_PAGE_INFO_TOPIC, Constants.STORYBOOK_PAGE_INFO_MESSAGE_TYPE);
        string statePubMessage = RosbridgeUtilities.GetROSJsonAdvertiseMsg(
            Constants.STORYBOOK_STATE_TOPIC, Constants.STORYBOOK_STATE_MESSAGE_TYPE);   
        string subMessage = RosbridgeUtilities.GetROSJsonSubscribeMsg(
            Constants.STORYBOOK_COMMAND_TOPIC, Constants.STORYBOOK_COMMAND_MESSAGE_TYPE);

        // Send all advertisements to publish and subscribe to appropriate channels.
        this.connected = this.rosClient.SendMessage(eventPubMessage) &&
            this.rosClient.SendMessage(pageInfoPubMessage) &&
            this.rosClient.SendMessage(statePubMessage) &&
            this.rosClient.SendMessage(subMessage);
    }

    private void onMessageReceived(object sender, int cmd, object properties) {
        Logger.Log("ROS Manager received and will handle message for command " + cmd);

        StorybookCommand command = (StorybookCommand)Enum.Parse(typeof(StorybookCommand), cmd.ToString());

        // First need to decode, then do something with it. 
        if (this.commandHandlers.ContainsKey(command)) {
            if (properties == null) {
                this.commandHandlers[command].Invoke(null); 
            } else {
                this.commandHandlers[command].Invoke((Dictionary<string, object>)properties);
            }
        } else {
            // Fail fast! Failure here means StorybookCommand struct is not up to date.
            throw new Exception("Don't know how to handle this command: " + command);
        }
    }

    //
    // Note that these all return Action so that they can be set as click handlers.
    //

    // Simple message to verify connection when we initialize connection to ROS.
    public Action SendHelloWorldAction() {
        return () => {
            this.sendEventMessageToController(StorybookEventType.HELLO_WORLD, "hello world");
        };
    }

    // Send the SpeechACE results.
    public Action SendSpeechAceResultAction(int sentenceIndex, string text,
        float duration, string jsonResults) {
        return () => {
            Logger.Log("Sending speech ace result event message");
            Dictionary<string, object> message = new Dictionary<string, object>();
            message.Add("index", sentenceIndex);
            message.Add("text", text);
            message.Add("duration", duration);
            message.Add("speechace", jsonResults);
            this.sendEventMessageToController(StorybookEventType.SPEECH_ACE_RESULT,
                Json.Serialize(message));
        };
    }

    // Send when TinkerText has been tapped.
    public Action SendTinkerTextTappedAction(int tinkerTextIndex, string word, string phrase) {
        return () => {
            Logger.Log("Sending tinkertext tapped event message");
            Dictionary<string, object> message = new Dictionary<string, object>();
            message.Add("index", tinkerTextIndex);
            message.Add("word", word);
            message.Add("phrase", phrase);
            this.sendEventMessageToController(StorybookEventType.WORD_TAPPED,
                Json.Serialize(message));
        };
    }
        
    // Send when SceneObject has been tapped.
    public Action SendSceneObjectTappedAction(int sceneObjectId, string label) {
        return () => {
            Logger.Log("Sending scene object tapped event message");
            Dictionary<string, object> message = new Dictionary<string, object>();
            message.Add("id", sceneObjectId);
            message.Add("label", label);
            this.sendEventMessageToController(StorybookEventType.SCENE_OBJECT_TAPPED,
                Json.Serialize(message));  
        };
    }

    // Send when Stanza has been swiped.
    public Action SendSentenceSwipedAction(int sentenceIndex, string text) {
        return () => {
            Logger.Log("Sending sentence swiped event message");
            Dictionary<string, object> message = new Dictionary<string, object>();
            message.Add("index", sentenceIndex);
            message.Add("text", text);
            this.sendEventMessageToController(StorybookEventType.SENTENCE_SWIPED,
                Json.Serialize(message));
        };
    }

    // Send when recording is complete. (Can be before it's sent up to SpeechACE).
    public Action SendRecordAudioComplete(int sentenceIndex) {
        return () => {
            Logger.Log("Sending record audio complete event message");
            Dictionary<string, object> message = new Dictionary<string, object>();
            message.Add("index", sentenceIndex);
            this.sendEventMessageToController(StorybookEventType.RECORD_AUDIO_COMPLETE,
                Json.Serialize(message));
        };
    }

    // Send when story is selected from the library (and we're waiting for it to load).
    public Action SendStorybookSelected(bool needsDownload) {
        return () => {
            Logger.Log("Sending storybook selected event message");
            Dictionary<string, object> message = new Dictionary<string, object>();
            message.Add("needs_download", needsDownload);
            this.sendEventMessageToController(StorybookEventType.STORY_SELECTED,
                Json.Serialize(message));
        };
    }

    // Send when story has loaded.
    public Action SendStorybookLoaded() {
        return () => {
            Logger.Log("Sending storybook loaded event message");
            this.sendEventMessageToController(StorybookEventType.STORY_LOADED, "");
        };
    }

    // Send when the user taps Explore or Evaluate, to tell controller to
    // change modes.
    public Action SendChangeMode(StorybookMode newMode) {
        return () => {
            Logger.Log("Sending change mode event message");
            Dictionary<string, object> message = new Dictionary<string, object>();
            message.Add("mode", (int)newMode);
            this.sendEventMessageToController(StorybookEventType.CHANGE_MODE,
                Json.Serialize(message));
        };
    }

    // Send when user wants Jibo to repeat the question.
    public Action SendRepeatEndPageQuestion() {
        return () => {
            Logger.Log("Sending repeat end page question event message");
            this.sendEventMessageToController(StorybookEventType.REPEAT_END_PAGE_QUESTION, "");
        };
    }

    // Send StorybookEvent message until received, in a new thread.
    private void sendEventMessageToController(StorybookEventType messageType, string message) {
        Thread t = new Thread(() => {
            Dictionary<string, object> publish = new Dictionary<string, object>();
            publish.Add("topic", Constants.STORYBOOK_EVENT_TOPIC);
            publish.Add("op", "publish");
            // Build data to send.
            Dictionary<string, object> data = new Dictionary<string, object>();
            data.Add("event_type", (int)messageType);
            data.Add("header", RosbridgeUtilities.GetROSHeader());
            data.Add("message", message);
            publish.Add("msg", data);
            Logger.Log("Sending event ROS message: " + Json.Serialize(publish));
            bool sent = false;
            while (!sent) {
                sent = this.rosClient.SendMessage(Json.Serialize(publish));
            }    
        });
        t.Start();
    }

    // Public wrapper to send storybook state at a specific time, when a timely update
    // is necessary. For example, after next page, need to make sure controller has
    // seen an updated evaluating_sentence_index before trying to send the next sentence.
    public void SendStorybookState() {
        this.sendStorybookState(null, null);
    }

    // Send a message representing storybook state to the controller, in a new thread.
    // Doesn't need to return Action because it's only used as a timer elapsed handler.
    private void sendStorybookState(object _, System.Timers.ElapsedEventArgs __) {
        Dictionary<string, object> publish = new Dictionary<string, object>();
        publish.Add("topic", Constants.STORYBOOK_STATE_TOPIC);
        publish.Add("op", "publish");

        // TODO: could devise a better scheme to make sure states are sent in order.
        // Can also use the sequence numbers provided in the header. Probably overkill.
        Dictionary<string, object> data = StorybookStateManager.GetRosMessageData();
        data.Add("header", RosbridgeUtilities.GetROSHeader());
        // Don't allow audio_file to be null, ROS will get upset.
        if (data["audio_file"] == null) {
            data["audio_file"] = "";
        }
        publish.Add("msg", data);

        bool success = this.rosClient.SendMessage(Json.Serialize(publish));
        if (!success) {
            // Logger.Log("Failed to send StorybookState message: " + Json.Serialize((publish)));
        }       
    }

    // Send a message representing new page info to the controller.
    // Typically will be called when the user presses previous or next.
    // Sends until success, in a new thread.
    public void SendStorybookPageInfoAction(StorybookPageInfo pageInfo) {
        Thread thread = new Thread(() => {
            Dictionary<string, object> publish = new Dictionary<string, object>();
            publish.Add("topic", Constants.STORYBOOK_PAGE_INFO_TOPIC);
            publish.Add("op", "publish");

            Dictionary<string, object> data = new Dictionary<string, object>();
            data.Add("header", RosbridgeUtilities.GetROSHeader());
            data.Add("story_name", pageInfo.storyName);
            data.Add("page_number", pageInfo.pageNumber);
            data.Add("sentences", pageInfo.sentences);

            List<Dictionary<string, object>> tinkerTexts =
                new List<Dictionary<string, object>> ();
            foreach (StorybookTinkerText t in pageInfo.tinkerTexts) {
                Dictionary<string, object> tinkerText = new Dictionary<string, object>();
                tinkerText.Add("has_scene_object", t.hasSceneObject);
                tinkerText.Add("scene_object_id", t.sceneObjectId);
                tinkerText.Add("word", t.word);
                tinkerTexts.Add(tinkerText);
            }
            data.Add("tinkertexts", tinkerTexts);

            List<Dictionary<string, object>> sceneObjects =
                new List<Dictionary<string, object>>();
            foreach (StorybookSceneObject o in pageInfo.sceneObjects) {
                Dictionary<string, object> sceneObject = new Dictionary<string, object>();
                sceneObject.Add("id", o.id);
                sceneObject.Add("label", o.label);
                sceneObject.Add("in_text", o.inText);
                sceneObjects.Add(sceneObject);
            }
            data.Add("scene_objects", sceneObjects);

            publish.Add("msg", data);
            Logger.Log("Sending page info ROS message: " + Json.Serialize(publish));
            bool sent = false;
            while (!sent) {
                sent = this.rosClient.SendMessage(Json.Serialize(publish));
            }
            Logger.Log("Successfully sent page info ROS message.");
        });
        thread.Start();
    }
}
