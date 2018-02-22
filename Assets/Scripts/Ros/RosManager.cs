// This class is a high level abstraction of dealing with Ros, to separate ROS
// logic from GameController.
//
// RosManager heavily relies on RosbridgeUtilities and RosbridgeWebSocketClient.

using UnityEngine;
using System;
using System.Collections.Generic;
using MiniJSON;

public enum StoryInfoMessageType {
    HELLO_WORLD = 0,
    SPEECH_ACE_RESULT = 1,
    REQUEST_ROBOT_FEEDBACK = 2,
}

public enum StorybookCommand {
    PING_TEST = 0,
}

public class RosManager {

    private GameController gameController; // Keep a reference to the game controller.
    private RosbridgeWebSocketClient rosClient;
    // TODO: note that for now only one handler can be registered per command.
    private Dictionary<StorybookCommand, Action<Dictionary<string, object>>> commandHandlers;

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
        return this.rosClient.SendMessage(pubMessage) && this.rosClient.SendMessage(subMessage);
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

    // Simple message to verify connection when we initialize connection to ROS.
    public bool SendHelloWorld() {
        return this.sendMessageToController(StoryInfoMessageType.HELLO_WORLD, "hello world");
    }

    // Send the SpeechACE results.
    public bool SendSpeechAceResult(string jsonResults) {
        return this.sendMessageToController(StoryInfoMessageType.SPEECH_ACE_RESULT, jsonResults);
    }

    private bool sendMessageToController(StoryInfoMessageType messageType, string message) {
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
        return this.rosClient.SendMessage(Json.Serialize(publish));
    }

}
