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

    private Thread storybookStateThread;

    private StorybookStateManager storybookStateManager;

    private DateTime lastStorybookStateSentTime = DateTime.Now;

    // Constructor.
    public RosManager(string rosIP, string portNum, GameController gameController) {
        Logger.Log("RosManager constructor");
        this.gameController = gameController;
        this.storybookStateManager = gameController.GetStorybookStateManager();

        this.rosClient = new RosbridgeWebSocketClient(rosIP, portNum);
        this.rosClient.receivedMsgEvent += this.onMessageReceived;
        this.commandHandlers = new Dictionary<StorybookCommand, Action<Dictionary<string, object>>>();
    }

    public bool Connect() {
        if (!this.rosClient.SetupSocket()) {
            Logger.Log("Failed to set up socket");
            return false;
        }
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

        // If connection successful, begin sending state messages.
        if (this.connected) {
            this.storybookStateThread = new Thread(() => {
                while (true) {
                    // Send StorybookState message over ROS at the desired frequency, set in Constants.
                    if (DateTime.Now.Subtract(this.lastStorybookStateSentTime).TotalSeconds
                        > Constants.STORYBOOK_STATE_PUBLISH_DELAY) {
                        if (this.connected) {
                            this.SendStorybookStateAction().Invoke();
                            this.lastStorybookStateSentTime = DateTime.Now;
                        }
                    }
                }
            });
            this.storybookStateThread.Start();
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
        this.storybookStateThread.Abort();
    }

    // Registers a message handler for a particular command the app might receive from the controller. 
    public void RegisterHandler(StorybookCommand command, Action<Dictionary<string, object>> handler) {
        this.commandHandlers.Add(command, handler);
    }

    private void onMessageReceived(object sender, int cmd, object properties) {
        Logger.Log("ROS Manager received message handler " + sender + " " + cmd);

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
    public Action SendSpeechAceResultAction(string jsonResults) {
        return () => {
            this.sendEventMessageToController(StorybookEventType.SPEECH_ACE_RESULT, jsonResults);
        };
    }

    // Send when TinkerText has been tapped.
    public Action SendTinkerTextTappedAction(int tinkerTextIndex, string word) {
        return () => {
            Logger.Log("Sending tinkertext tapped event message");
            Dictionary<string, object> message = new Dictionary<string, object>();
            message.Add("index", tinkerTextIndex);
            message.Add("word", word);
            this.sendEventMessageToController(StorybookEventType.WORD_TAPPED,
                Json.Serialize(message));
        };
    }
        
    // Send when SceneObject has been tapped.
    public Action SendSceneObjectTappedAction(int sceneObjectId, string label) {
        return () => {
            Logger.Log("Sending scene object tapped event message");
            Dictionary<string, object> message = new Dictionary<string, object>();
            message.Add("sceneObjectId", sceneObjectId);
            message.Add("label", label);
            this.sendEventMessageToController(StorybookEventType.SCENE_OBJECT_TAPPED,
                Json.Serialize(message));  
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

    // Send a message representing storybook state to the controller, in a new thread.
    public Action SendStorybookStateAction() {
        return () => {
            Thread t =  new Thread(() => {
                Dictionary<string, object> publish = new Dictionary<string, object>();
                publish.Add("topic", Constants.STORYBOOK_STATE_TOPIC);
                publish.Add("op", "publish");

                Dictionary<string, object> data = new Dictionary<string, object>();

                // Note that this is protected by a lock, so although ROS messages could
                // send out of order, the information within them will be consistent.
                // And if the sending rate isn't too high, the likelihood of out of order messages
                // is low, and inconsequential for the controller anyway.
                // TODO: should devise a better scheme to make sure states are sent in order.
                // Can also use the sequence numbers provided in the header.
                // Or use a lock in this class so that only one state message can be sent at a time.
                StorybookState currentStorybookState = this.storybookStateManager.getCurrentState();
                data.Add("header", RosbridgeUtilities.GetROSHeader());
                data.Add("audio_playing", currentStorybookState.audioPlaying);
                data.Add("audio_file", currentStorybookState.audioFile);
                data.Add("storybook_mode", (int)currentStorybookState.storybookMode);
                data.Add("current_story", currentStorybookState.currentStory);
                data.Add("num_pages", currentStorybookState.numPages);
                data.Add("evaluating_stanza_index", currentStorybookState.evaluatingStanzaIndex);

                publish.Add("msg", data);

                bool success = this.rosClient.SendMessage(Json.Serialize(publish));
                if (!success) {
                    Logger.Log("Failed to send StorybookState message: " + Json.Serialize((publish)));
                } 
            });
            t.Start();
        };
    }

    // Send a message representing new page info to the controller.
    // Typically will be called when the user presses previous or next.
    // Sends until success, in a new thread.
    public Action SendStorybookPageInfoAction(StorybookPageInfo pageInfo) {
        return () => {
            Thread thread = new Thread(() => {
                Dictionary<string, object> publish = new Dictionary<string, object>();
                publish.Add("topic", Constants.STORYBOOK_PAGE_INFO_TOPIC);
                publish.Add("op", "publish");

                Dictionary<string, object> data = new Dictionary<string, object>();
                data.Add("header", RosbridgeUtilities.GetROSHeader());
                data.Add("story_name", pageInfo.storyName);
                data.Add("page_number", pageInfo.pageNumber);
                data.Add("stanzas", pageInfo.stanzas);

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
        };
    }
}
