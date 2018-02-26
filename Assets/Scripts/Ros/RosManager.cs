// This class is a high level abstraction of dealing with Ros, to separate ROS
// logic from GameController.
//
// RosManager heavily relies on RosbridgeUtilities and RosbridgeWebSocketClient.

using UnityEngine;
using System;
using System.Collections.Generic;
using MiniJSON;

// Messages from the storybook to the controller.
public enum StoryInfoMessageType {
    HELLO_WORLD = 0,
    SPEECH_ACE_RESULT = 1,
    REQUEST_ROBOT_FEEDBACK = 2,
    WORD_TAPPED = 3,
}

// Messages coming from the controller to the storybook.
// We will need to deal with each one by registering a handler.
public enum StorybookCommand {
    PING_TEST = 0,
}

public class RosManager {

    private GameController gameController; // Keep a reference to the game controller.
    private RosbridgeWebSocketClient rosClient;
    // TODO: note that for now only one handler can be registered per command.
    private Dictionary<StorybookCommand, Action<Dictionary<string, object>>> commandHandlers;
    private bool connected;

    // Constructor.
    public RosManager(string rosIP, string portNum, GameController gameController) {
        Logger.Log("RosManager constructor");
        this.gameController = gameController;

        this.rosClient = new RosbridgeWebSocketClient(rosIP, portNum);
        this.rosClient.receivedMsgEvent += this.onMessageReceived;
        this.commandHandlers = new Dictionary<StorybookCommand, Action<Dictionary<string, object>>>();
    }

    public bool Connect() {
        if (!this.rosClient.SetupSocket()) {
            Logger.Log("Failed to set up socket");
            return false;
        }
        string pubMessage = RosbridgeUtilities.GetROSJsonAdvertiseMsg(
            Constants.STORYBOOK_TO_ROSCORE_TOPIC, Constants.STORYBOOK_TO_ROSCORE_MESSAGE_TYPE);
        string subMessage = RosbridgeUtilities.GetROSJsonSubscribeMsg(
            Constants.ROSCORE_TO_STORYBOOK_TOPIC, Constants.ROSCORE_TO_STORYBOOK_MESSAGE_TYPE);
        this.connected = this.rosClient.SendMessage(pubMessage) && this.rosClient.SendMessage(subMessage);
        return this.connected;
    }

    public bool isConnected() {
        return this.connected;
    }

    // Registers a message handler for a particular command the app might receive from the controller. 
    public void RegisterHandler(StorybookCommand command, Action<Dictionary<string, object>> handler) {
        this.commandHandlers.Add(command, handler);
    }

    private void onMessageReceived(object sender, int cmd, object properties) {
        Logger.Log("ROS Manager received message handler " + sender + " " + 
                   cmd.ToString());

        StorybookCommand command = (StorybookCommand)Enum.Parse(typeof(StorybookCommand), cmd.ToString());

        // First need to decode, then do something with it. 
        if (this.commandHandlers.ContainsKey(command)) {
            if (properties == null) {
                this.commandHandlers[command].Invoke(null); 
            } else {
                this.commandHandlers[command].Invoke((Dictionary<string, object>)properties);
            }
        } else {
            Logger.LogError("Don't know how to handle this command: " + command);
        }
    }

    // Note that these all return Action so that they can be set as click handlers.

    // Simple message to verify connection when we initialize connection to ROS.
    public Action SendHelloWorld() {
        return () => {
            this.sendMessageToController(StoryInfoMessageType.HELLO_WORLD, "hello world");
        };
    }

    // Send the SpeechACE results.
    public Action SendSpeechAceResult(string jsonResults) {
        return () => {
            this.sendMessageToController(StoryInfoMessageType.SPEECH_ACE_RESULT, jsonResults);
        };
    }

    // Send when TinkerText has been tapped.
    public Action SendTinkerTextTapped(string text) {
        return () => {
            Logger.Log("sending tinkertext tapped");
            this.sendMessageToController(StoryInfoMessageType.WORD_TAPPED, text);
        };
    }

    // Send until received.
    private void sendMessageToController(StoryInfoMessageType messageType, string message) {
        Dictionary<string, object> publish = new Dictionary<string, object>();
        publish.Add("topic", Constants.STORYBOOK_TO_ROSCORE_TOPIC);
        publish.Add("op", "publish");
        // Build data to send.
        Dictionary<string, object> data = new Dictionary<string, object>();
        data.Add("message_type", (int)messageType);
        data.Add("header", RosbridgeUtilities.GetROSHeader());
        data.Add("message", message);
        publish.Add("msg", data);
        Logger.Log("Sending ROS message: " + Json.Serialize(publish));
        bool sent = false;
        while (!sent) {
            sent = this.rosClient.SendMessage(Json.Serialize(publish));
        }
    }



}
