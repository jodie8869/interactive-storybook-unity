# interactive-storybook-unity

This repository contains code for the Unity/Android tablet application that presents interactive storybooks to children as part of a reading experience with a robot agent. Using Unity 2017.2.0f3, targetting .NET framework v4.6 in player settings.

The code has a few key classes, and many supporting classes. The important classes are:

1. `GameController` - controls high level logic, task queue
2. `StoryManager` - responsible for loading each story page, including all interactable elements
3. `RosManager` - manages communication over ROS to other system components
4. `AssetManager` - assists in the transfer of assets to and from Amazon S3 cloud storage

The supporting classes are also mentioned below. In general, there is only one instance of each class in this codebase. The exceptions to this are for classes that are meant to be used as templates for GameObjects or as UI building blocks, or to represent different types of data. These exceptions are:

- `SceneDescription` - class representing the JSON format for the data needed for each story page
- `StoryJson` - class to wrap a SceneDescription file name and its JSON text
- `StoryMetadata` - class representing the JSON format for the metadata of a story
- `TinkerText` - class for each word on the page, supports text highlighting and handling when the text is clicked
- `SceneObjectManipulator` - class for each scene object that allows highlighting and could support movement/animation
- `LibraryBook` - class for controlling a book in the library screen
- `PopupLabel` - class for controlling a label that pops up when the user clikcs a scene object whose label doesn't appear in the text and we want to display the text somehow

## 1. class: GameController
#### App Setup and Initialization
When the app starts, a few things happen:

- All button handlers are initialized.
- The app is resized via `resizePanelsOnStartup()`.
- The preset stories that should be shown in the library are set up via `initStories()`.
- If the app is configured to function as a standalone app with no robot (this is set via the `USE_ROS` flag in the Constants class), then the app directly loads and displays the library screen on startup. If the app is configured to function as part of an interaction with the robot, then the app displays a ROS connection screen on startup. The user will then be prompted to enter an IP address for the roscore node, and then to press "Start" to make the initial connnection. More on ROS in Section 3.

#### Story Library
Whether the app is functioning with a robot or not, after setup the user will end up in the library panel. Each library book shows the title page of a story that is available. The title pages for these stories are downloaded in the `downloadStoryTitlesAndShowLibrary()` function, which uses `AssetManager`'s methods to access assets via the AmazonS3 API. When a library book is selected, a "Read" button appears (`onLibraryBookClick()`). When the "Read" button is clicked, the assets for the selected book are downloaded via `AssetManager` and saved (`onReadButtonClick()` calls `startStory()`).

`startStory()` checks if the assets have already been downloaded, and if they have not, it makes the calls to download it. When assets are downloaded, it calls `startStoryHelper()`, which initializes the information for each page of the story in the `storyPages` array. While the assets are downloading, a loading screen appears and animates (code for loading page is in `LoadingBook` class), and when the assets are done loading, the loading screen is replaced with a title page screen, which appears via the function `loadFirstPage()`.

Before selecting a book, the user also has the option to change the reading mode, or download new stories. There are two modes: explore and evaluate. Explore can be done with or without a robot, while evaluate can only be done with a robot. In explore mode, the child is free to interact with the tablet and read at her own pace, and navigate the pages freely. In evaluate mode, the child is presented sentences one by one and must read them and answer questions from the robot at the end of each page. Downloading new stories is a feature that is used when someone has created a new story via the authoring interface web app, a separate component of the project with its own set of docs, and the tablet app user now wants to read that story on the tablet. This feature uses `AssetManager` to download the necessary assets to put a new book title page in the library screen.

#### Task Queue and Ros Message Handlers
Many classes in the Unity ecosystem extend the class "MonoBehaviour," which provides an `Update()` function that is called on every frame update. For `GameController`, the `Update()` function simply processes and executes tasks from a task queue. Other threads, particularly the ROS message handling threads, push tasks onto the queue. This is done because several of the ROS messages command the app to modify the UI in some way (for example, highlighting a particular word in the text), and Unity does not allow any thread besides the main thread to modify the GameObjects in the scene. (A GameObject is a basic Unity object. It can be an image, text, button, panel, etc.)

Each ROS message handler is registered with the RosManager class. This registration happens in `GameController` with a call to `registerRosMessageHandlers()` as soon as the ROS connection is established. Each handler function is a private void function, and it pushes a task onto the queue. In order to separate out the logic of performing some of the desired actions such that they can also be called from outside of ROS message handlers, the logic is often in its own function, that returns an `Action`, which the ROS message handler can enqueue, or another caller can directly invoke. An example of this paradigm is the `onHighlightSceneObjectMessage` ROS message handler function, which looks like:

```C#
    private void onHighlightSceneObjectMessage(Dictionary<string, object> args)
    {
        Logger.Log("onHighlightSceneObjectMessage");
        int[] ids = Util.ParseIntArrayFromRosMessageParams(args);
        Logger.Log("This many scene objects to highlight: " + ids.Length);
        this.taskQueue.Enqueue(this.highlightSceneObject(ids));
    }
    
    private Action highlightSceneObject(int[] ids)
    {
        return () => {
            foreach (int id in ids) {
                if (this.storyManager.sceneObjects.ContainsKey(id)) {
                    this.storyManager.sceneObjects[id]
                        .GetComponent<SceneObjectManipulator>()
                        .Highlight(Constants.SCENE_OBJECT_HIGHLIGHT_COLOR)
                        .Invoke();   
                } else {
                    Logger.Log("No scene object with id: " + id);
                }
            }
        };
    }
```

Notice that the handler function does not actually perform any actions, it simply enqueues the task returned by `highlightSceneObject(ids)`, which is the funcion that actually modifies the color of the specified text objects. Also note that `highlightSceneObject(int[] ids)` returns an `Action`, which is C#'s version of an anonymous function. 

#### Other Functions - Audio Recording, App Navigation
Besides ROS handlers and button handlers, the GameController does not have many responsibilities. However, within the ROS handlers and button handlers are a few more complicated functions that are described here.

The first is page navigation. When the story wants to move to the next page (either as a result of a button press or a ROS message command), all code paths will end up calling `loadPageAndSendRosMessage()`, which is the only function that actually handles the logic of changing pages. Do not introduce another function that does the same thing. 

The second is audio recording for the child. This is only necessary in evaluate mode. When a sentence is shown, the audio recording begins automatically. When the "next sentence" button is pressed, it doubles as a "stop recording" button, and the button handler `ondoneRecordingButtonClick()` is called. This button handler calls `stopRecordingAndDoSpeechace()`, which will access the audio clip from `StoryAudioManager` and send it to SpeechACE for evaluation via `SpeechAceManager`.

There are also some messy but necessary functions that are used for layout, showing/hiding various screens of the app, and changing the orientation of the app if necessary. These are all located at the bottom of the class. 

## 2. class: StoryManager
The `StoryManager` class is responsible for one major task: Given a `SceneDescription` object, materialize a story page on the app. This includes several features:

- Image is loaded and does not overflow its container.
- Text is broken into sentences and displayed in logically determined stanzas and sentences.
- Each word is tappable, and tapping it causes both the word and any scene objects that match the word to be highlighted. For example, if there is an object labeled "toad" and the word "toad" appears in the story text (e.g. "The toad was always hungry."), then tapping the word toad will light up both the word and the object.
- All bounding boxes for scene objects are loaded but initially invisible.
- Each scene object is tappable, and the effect is the same as for tapping a word. Namely, the scene object should be highlighted, and if there is a word matching that scene object, the word should also be highlighted. If there is no word matching the label of the scene object, then a popup label should appear.
- Each stanza that is the first stanza in its sentence is swipeable, and swiping the stanza plays the audio for that sentence.
- When audio plays, the words should highlight in time with the audio.

There only exists one instance of `StoryManager`, and it belongs to `GameController`. When it is time to load a page, `GameController` calls `StoryManager`'s `LoadPage()` function, which is the entrance point to most of `StoryManager`'s code. `LoadPage()` takes as argument a `SceneDescription` object. The details of `SceneDescription` are given later in Section 4.2, but essentially the object tells `StoryManager` what text, images, scene objects, and timestamps exist for the desired story page. The `LoadPage()` function calls helper functions to handle most of the heavy lifting:

- `loadImage()` - load the image and adjust the size of the image container so that the image fits the container as much as possible without overflowing or distorting the aspect ratio
- `loadTinkerText()` - load each word of the text as a `TinkerText` object, set the click handlers, and handle layout and stanza swiping through `StanzaManager`, which is described in Section 5.4
- `loadAudioTriggers()` - use the timestamps to set up when to highlight words while audio plays, see `StoryAudioManager` in Section 2.5
- `loadSceneObject()` - creates a scene object
- `loadTrigger()` - creates associations between scene objects and tinker texts

`StoryManager` is also responsible for displaying the title page and "The End" page at the beginning and end of a story respectively, which it does via `ShowTheEndPage()` and `loadTitlePage()`. Note that `ShowTheEndPage()` is called by `GameController` while `loadTitlePage()` is called inside of `LoadPage()` as a special case when the page number is 0. This is an artifact of adding in the "The End" page later than when the rest of the `StoryManager` code was written.

### 2.1 class: TinkerText
The `TinkerText` class is a MonoBehaviour that manages a single word in the story text. IT is initialized with an index, string, timestamp (start and end), and a flag indicating whether it is the last word in the story text. This flag is a bit of a hack to set the end audio timestamp to prevent the audio from clipping and prevent the word highlighting from getting stuck on this word and never unhighlighting. More on this in Section 2.5.

Each `TinkerText` object has an index, which is its index in the overall text, if the text were represented as a string array. A tinker text can have its width set (depending on the length of the word, for example) with the `SetWidth(float newWidth)` function, which is called by `StanzaManager`. The tinker text has a single click handler, but other classes can add actions to the tinker text's click handler via the `AddClickHandler(Action action)` function. A tinker text also has an array called `sceneObjectIds` which stores the ids of scene objects that the tinker text is paired with. A tinker text also has functions that support color changing, and it has the functions `OnStartAudioTrigger()` and `OnEndAudioTrigger()` which are called when a word is beginning to be spoken in the audio and has just finished being spoken, respectively. 

The tinker text has three timetamps. There is an `audioStartTime` and `audioEndTime`, which are known from the originating `SceneDescription`. There is also a `triggerAudioEndTime`, which exists to handle an edge case where the last word of the text does not unhighlight automatically, because its timestamp is normally the end of the audio, and it is never triggered because `StoryAudioManager` will not play triggers past the end of the audio, so the `triggerAudioEndTime` for the last word of the text is modified to be slightly earlier than the real `audioEndTime`. Relatedly, to prevent audio from clipping, the `audioEndTime` of the last word of the text is set to the max value possible, so that the `StoryAudioManager` knows not to cut off the audio too early. These issues generally arise because of the fact that the timestamps are only to the 1st decimal place, and therefore are inexact.

### 2.2 class: SceneObjectManipulator
`SceneObjectManipulator` is a MonoBehaviour that is attached to every scene object. A scene object is a GameObject that is essentially a rectangle that is layered on top of a particular region in the story image, a region that represents some labeled object such as a "coat" or a "spatula." The `SceneObjectManipulator` supports functions for modifying its appearance and handling clicks.

It has a click handler that `StoryManager` can add actions to using `SceneObjectManipulator`'s `AddClickHandler()` function. It also has `MoveToPosition()` for moving the scene object to the right coordinates. The `SceneObjectManipulator` also has functions that support adding a sprite and changing the size of the sprite, which supports the functionality of having a sprite overlay the story image and then expand when clicked, which is a more lively effect that simply changing the color of a bounding box, which is what the click handler currently does.

### 2.3 class: Stanza
A `Stanza` is a MonoBehaviour that represents a single line of text. A stanza is either an entire sentence, or is a part of one sentence. A stanza is swipeable only if it is the first or only stanza of a sentence. The stanzas are laid out by `StanzaManager`. Stanzas know their audio start and end times, and know the indexes of tinker texts that they contain, and know their own index in the stanzas array.

In the `Update()` function, the stanza constantly checks for whether it has been swiped. It does this by detecting the mousedown and mouseup events. There is a single swipe handler, but other classes can add actions to this swipe handler via the `AddSwipeHandler()` function, which is analogous to the `AddClickHandler()` function in `TinkerText` and `SceneObjectManipulator`. There is a static flag, `ALLOW_STANZA_SWIPE_GLOBAL`, which is enabled and disabled by `GameController`, and is false when the reading mode is evaluate, because users are not allowed to swipe stanzas in that mode. There is another static flag `ALLOW_SIPE_BECAUSE_AUDIO` which is enabled and disabled by `StoryManager`, and is false when there is currently audio playing (as reported by `StoryAudioManager`), so that the user cannot swipe on a stanza until the previous audio has finished playing. And there is a nonstatic flag `specificStanaAllowSwipe` that is set for each stanza independently, depending on whether it is the first/only stanza in the sentence it belongs to. The swipe handler is only called when a swipe is detected and all three swipe controlling flags are true.

A stanza can be operated on as a unit to affect the appearance of all tinker texts in the stanza. Stanza supports functions like `FadeIn()`, `Show()` and `ChangeTextColor()`.  

### 2.4 class: StanzaManager
The purpose of `StanzaManager` is to abstract away the logic of breaking the text into sentences nicely and displaying them nicely. There is exactly one `StanzaManager` and it belongs to the `StoryManager`. The `StanzaManager` maintains an array of stanzas and of sentences, which can be accessed by `StoryManager` via getter methods if desired. The `StanzaManager`'s main function is `AddTinkerText(GameObject tinkerTextObject)` which takes a newly instantiated tinker text and places it onto the page (this happens in `StoryManager`'s `loadTinkerText()` function). The `AddTinkerText()` function is a little complicated and is documented almost line by line in the code itself. The end result is that after adding all of the tinker texts, the text should be displayed nicely and broken into stanzas and sentences. Each sentence can also be operated on as a unit, and will affect all stanzas tha belong to it, for visual effects such as highlighting.

After all text has been added, the `StoryManager` will call `StanzaManager`'s `SetSentenceSwipeHandlers()` function, which adds to each stanza a swipe handler action that sends a ROS message announcing that the stanza has been swiped. Of course, this only happens if the app is connected to ROS.

### 2.5 class: StoryAudioManager
The `StoryAudioManager` is responsible for playing and pausing audio, and triggering text highlighting or theoretically any other events as the audio is playing. It supports any type of trigger that is timestamp and an action. When the audio is playing, the `Update()` function of `StoryAudioManager` compares the current timestamp of the audio to timestamps of known triggers, and invokes the actions of any triggers that have been passed in this frame update (but not the previous one). `StoryAudioManager` needs an audio clip to play, and is provided one by `StoryManager` via `StoryAudioManager`'s `LoadAudio()` function.

Other classes can add a trigger using the `AddTrigger(float timestamp, Action action, bool disallowAfterStop)` function. The argument `disallowAfterStop` is to handle the case where we do not want the trigger's action to occur if the current audio timestamp is past the end of the interval the audio is playing. The reason it is necessary to separately handle this case is because the `Update()` function is called once per frame update, so it is possible that the current audio timestamp at the time `Update()` is called is past the triggering timestamp of the trigger, and is also past the stop timestamp of the desired interval (e.g. the interval for a particular sentence). In that case, if the desired interval represented a sentence that was not the last sentence, then there would be a trigger to highlight the next word (the first word of the next sentence) at the same time as a trigger to unhighlight the last word of the desired sentence. We want one of those actions to happen (the unhighlighting of the last word) but not the other (the highlighting of the next word). This is what `disallowAfterStop` does. It is true for words that are the first word in their stanza (which is actually more restrictive than the first word of a sentence, but since the sentence concept was developed later I just kept it as stanza instead of sentence for this flag), so that the first word of the next sentence in the situation I described is not highlighted.

## 3. class: RosManager
The `RosManager` is the rest of the application's gateway to the rest of the system (particularly the controller). `RosManager` is built on `RosbridgeWebSocketClient` and is responsible for initializing a connection to the roscore node over the rosbridge, publishing and subscribing to topics, publishing messages at the appropriate time and sending received messages to the correct handler in `GameController`. 

The one topic that the tablet app subscribes to is:
- `StorybookCommand` - published when the controller wants the tablet app to do something; these are the messages that are handld in the ROS message handlers in `GameController`.

The three topics that the tablet app publishes to are:
- `StorybookState` - published consistently at the rate `STORYBOOK_STATE_PUBLISH_HZ` in `Constants`
- `StorybookPageInfo` - published whenever there is a page change
- `StorybookEvent` - published for any other type of event, such as user interactions with the tablet, SpeechACE results, the story ending and returning to the library screen, etc.

After the user has typed in a ROS IP address and pressed "Start" on the initial connection screen on app startup, `GameController` calls `RosManager`'s `Connect()` function, which calls the underlying `RosbridgeWebSocketClient`'s `SetupSocket` function, and then also sets up publishers and subscribers via `setupPubSub()`.

`RosManager` supports a `RegisterHandler()` function that `GameController` can call to register a ROS message handler. Then `RosManager` registers its own `onMessageReceived()` function with the underlying `RosbridgeWebSocketClient`. The `onMessageReceived()` function simply looks at which type of `StorybookCommand` was sent, and then calls the appropriate ROS message handler. To add a new `StorybookCommand`, the process would be to add it to the ROS msg definition, update it in `RosStorybookMessage`, write a handler for it in `GameController`, then register that handler with the `RosManager`.

Each type of `StorybookEvent` message that the `RosManager` can send is sent via its own function. These functions return an `Action` so that they can be set as click handlers. For example the `SendSentenceSwipedAction()` function returns an `Action` that is then set as the swipe handler for the first stanza in that sentence. Each of the functions that sends a `StorybookEvent` message eventually calls the function `sendEventMessageToController()`, which starts a new thread to send the message and continues to retry until it is sent successfully. There is a separate function, `SendStorybookPageInfoAction()`, that just sends a `StorybookPageInfo` message to the controller. And there is a `SendStorybookState()` function that is called at a fixed interval by the timeout function of the `publishStateTimer`. The state itself is managed by `StorybookStateManager`, a static class.

### 3.1 class: RosStorybookMessages
This is a utility class that defines the different message types that the tablet app subscribes to and publishes to. See the class itself for all of the messages and their definitions.

### 3.2 class: StorybookStateManager
`StorybookStateManager` is a static class that maintains the most recent state of the storybook. The state includes the current page, the current sentence (if in evaluate mode), whether or not audio is playing and which file is playing, the current story, and the current storybook mode (evaluate vs. explore vs. not reading). Other classes modify this static state. For example, `StoryAudioManager` updates the `audio_playing` field of the current state. Other classes can also request to view the current state by calling `GetState()`. To save the effort of reconstructing a dictionary to serialize to send the state, `StorybookStateManager` also maintains a dictionary that represents the same information as the state. The reason to not only use a dictionary is because it is less desirable in code to reference fields by their key string instead of by an actual property name that can be statically checked. When `RosManager` wants to send state, it calls `GetRosMessageData()` which returns a copy of the current dictionary. (Because it is returning a copy, it may not be saving much time compared to just constructing the dictionary from the properties every time, but for some reason I just found it to be cleaner this way).

### 3.3 class: RosbridgeWebSocketClient
This is a utility class written by a previous grad student. I made a few modifications to it to prevent things like app freezing when a connection fails, and to throttle too many consecutive reconnect attempts. At its core it is a fairly straightforward class that implements a web socket to receive and send ROS messages. The things to know about it are: 

- `SetupSocket()` - called on initial connection attempt
- `CloseSocket()` - called in `GameController`'s `OnApplicationQuit()` function to cleanly close the connection when the application is exited
- `SendMessage()` - used to send all messages
- `HandleOnMessage()` - call this and pass it a function to handle all received messages, in `RosManager`'s case the function that is passed is `RosManager`'s `onMessageReceived()`.

One disadvantage of this class is that it currently can only handle messages that have the structure:

```
{
    command: int,
    params: string --OR-- Dictionary<string, object>
}
```

where `params` is a serialized dictionary of string keys to object values. This restriction comes from the fact that the decoding of messages happens via `RosbridgeUtilities`, which only knows how to deserialize the arguments if they are in the `string` or `Dictionary<string, object>` form. Luckily, this covers pretty much everything we need to send in the context of a `StorybookCommand`, so it is not really a restriction on this app.

### 3.4 class: RosbridgeUtilities
A utility class is the contemporary of `RosbridgeWebSocketClient`. It provides some useful utility functions. The important ones are:

- `GetROSJsonSubscribeMsg()` - creates a message to subscribe to a topic
- `GetROSJsonAdvertizeMsg()` - creates a message to advertize publishing a topic
- `DeserializeROSJsonCommand()` - deserializes a message into an integer command and an object that is the params, which is either a `string` or a `Dictionary<string, object>

I construct the JSON messages I want to publish on a particular topic instead of using any utility functions, but one could use `GetROSJsonPublishStringMsg()` to build such a message.

## 4. class: AssetManager
The `AssetManager` handles downloading story assets from AmazonS3 and uploading recorded clips of child audio to AmazonS3. Some of the older code operates by using the `WWW` object and Coroutines, while the newer code takes advantage of the actual AmazonS3 API. It would be a good idea to port over the old `WWW` code to also use the AmazonS3 API, but I didn't get a chance to do that. The `AssetManager` supports functions `GetSprite()`, `GetAudioClip()` and `GetStoryJson()` for when other classes want to access assets that have already been downloaded. Currently, it is assumed that the assets have already been downloaded prior to any of these three functions being called, and the caller can check to be sure by checking the result of `JsonJasBeenDownloaded()` for the story in question.

### 4.1 class: SceneDescription
A `SceneDescription` is an object that represents the JSON file that describes a story page. The `GameController` has an array of `SceneDescription` objects that represent the pages of the current story. Each time the user moves to a different page, the `GameController` passes the appropriate `SceneDescription` object to `StoryManager`'s `LoadPage()` function. The `SceneDescription` can be initialized directly from the raw serialized JSON string in the constructor.

### 4.2 class: StoryMetadata
A `StoryMetadata` is an object that represents the JSON file that provides metadata about a particular story. This metadata includes the title, number of pages, human readable name, orientation, and targe words. When the `AssetManager` downloads new StoryMetadata JSON files, it can create a new `StoryMetadata` object for each one by just passing in the raw serialized JSON string to the `StoryMetadata` constructor.

## 5. Utility Classes
### 5.1 class: AudioRecorder
Responsible for recording audio clips of the child speaking, and then saving them locally.

- `StartRecording()` - starts the recording
- `EndRecording(Action<AudioClip> callback)` - ends the recording and calls the callback with the audio clip
- `SaveAudioAtPath(string filepath, AudioClip audio)` - save the audio clip at the provided path rooted at `Application.persistentDataPath` using `SavWav`
- `LoadAudioLocal(string filepath)` - returns the audio clip saved at file path (rooted at `Application.persistentDataPath`); uses my own implementation of decoding the wave file because the `SavWav` one didn't work properly

### 5.2 class: SavWav
An imported class from https://gist.github.com/JT5D/5974837 that saves wave files.

### 5.3 class: SpeechAceManager
This class handles uploading an audio file to the SpeechACE service, and allows the caller to provide a callback to handle the response. The only public function is:

- `AnalyzeTextSample(string filename, string text, Action<string> callback, bool block)` - analyzes the audio sample at the given filename (path) that corresponds to the provided text, a callback to handle the string response, and a boolean to tell the function whether to block the caller or not; note: this function should be called in a coroutine

### 5.4 class: Constants
Stores constants such as the default ROS IP address, whether or not to use ROS in the app, the publishing rate for `StorybookState`, colors and sizes for UI elements.

### 5.5 class: Util
A catch-all utility class to house any random functions that are useful but shouldn't clutter the other classes. Examples include code to search for punctuation in strings (for breaking strings into clauses and sentences), update the positioning of a shelf in the library view, check if two rectangles are likely to refer to the same object (for filtering scene objects with the same label).


### 5.6 class: Logger
A logging class supporting error, warning and debug logging. Not written by me; copied from a previous project.
