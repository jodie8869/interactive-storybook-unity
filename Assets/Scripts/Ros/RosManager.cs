// This class is a high level abstraction of dealing with Ros, to separate ROS
// logic from GameController.
//
// RosManager heavily relies on RosbridgeUtilities and RosbridgeWebSocketClient.

using UnityEngine;
using System.Collections.Generic;
using MiniJSON;

public class RosManager {

    private GameController gameController; // Keep a reference to the game controller.
    private RosbridgeWebSocketClient rosClient;

    // Constructor.
    public RosManager(string rosIP, string portNum, GameController gameController) {
        Logger.Log("RosManager constructor");
        this.gameController = gameController;

        this.rosClient = new RosbridgeWebSocketClient(rosIP, portNum);
        this.rosClient.receivedMsgEvent += this.onMessageReceived;
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

    private void onMessageReceived(object sender, int command, object properties) {
        Logger.Log("ROS Manager received message " + sender + " " + 
                   command.ToString() + " " + properties);

        // First need to decode, then do something with it. 

        // TODO: Depending on the message type, take some actions.
    }

    public bool SendHelloWorld() {
        Logger.Log("Sending hello world");
        Dictionary<string, object> message = new Dictionary<string, object>();
        message.Add("topic", Constants.STORYBOOK_TO_ROSCORE_TOPIC);
        // Or, have different functions for different messages, and they all end up calling this.
        return this.rosClient.SendMessage(Json.Serialize(message));
    }

}
