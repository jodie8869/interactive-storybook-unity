// This class is a high level abstraction of dealing with Ros, to separate ROS
// logic from GameController.
//
// RosManager heavily relies on RosbridgeUtilities and RosbridgeWebSocketClient.

public class RosManager {

    GameController gameController; // Keep a reference to the game controller.
    RosbridgeWebSocketClient rosClient;

    // Constructor.
    public RosManager(string rosIP, string portNum) {
        rosClient = new RosbridgeWebSocketClient(rosIP, portNum);
        rosClient.receivedMsgEvent += this.onMessageReceived;
    }

    private void onMessageReceived(object sender, int command, object properties) {
        Logger.Log("ROS Manager received message " + sender + " " + command.ToString() + " " + properties);
        // Depending on the message type, take some actions.
    }

    public bool SendMessage(string message) {
        // TODO: do some stuff maybe, and then send the message.
        // Or, have different functions for different messages, and they all end up calling this.
        return this.rosClient.SendMessage(message);
    }


}
