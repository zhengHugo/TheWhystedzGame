using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using UnityEngine;
using VivoxUnity;


public class VivoxVoiceManager : MonoBehaviour
{
    #region Enums

    /// <summary>
    /// Defines properties that can change.  Used by the functions that subscribe to the OnAfterTYPEValueUpdated functions.
    /// </summary>
    public enum ChangedProperty
    {
        None,
        Speaking,
        Typing,
        Muted
    }

    public enum ChatCapability
    {
        TextOnly,
        AudioOnly,
        TextAndAudio
    };

    #endregion

    #region Delegates/Events

    public delegate void ParticipantValueChangedHandler(string username, ChannelId channel,
        bool value);

    public event ParticipantValueChangedHandler OnSpeechDetectedEvent;

    public delegate void ParticipantValueUpdatedHandler(string username, ChannelId channel,
        double value);

    public event ParticipantValueUpdatedHandler OnAudioEnergyChangedEvent;


    public delegate void ParticipantStatusChangedHandler(string username, ChannelId channel,
        IParticipant participant);

    public event ParticipantStatusChangedHandler OnParticipantAddedEvent;
    public event ParticipantStatusChangedHandler OnParticipantRemovedEvent;

    public delegate void ChannelTextMessageChangedHandler(string sender,
        IChannelTextMessage channelTextMessage);

    public event ChannelTextMessageChangedHandler OnTextMessageLogReceivedEvent;

    public delegate void LoginStatusChangedHandler();

    public event LoginStatusChangedHandler OnUserLoggedInEvent;
    public event LoginStatusChangedHandler OnUserLoggedOutEvent;

    #endregion

    #region Member Variables

    private Uri ServerUri
    {
        get => new Uri(server);

        set => server = value.ToString();
    }

    private string server = "https://mt1s.www.vivox.com/api2";
    private const string Domain = "mt1s.vivox.com";
    private const string TokenIssuer = "hugozh5545-vi18-dev";
    private const string TokenKey = "dump966";
    private TimeSpan tokenExpiration = TimeSpan.FromSeconds(90);

    private Client client = new Client();
    private AccountId accountId;

    // Check to see if we're about to be destroyed.
    private static object lockObj = new object();
    private static VivoxVoiceManager instance;

    /// <summary>
    /// Access singleton instance through this propriety.
    /// </summary>
    public static VivoxVoiceManager Instance
    {
        get
        {
            lock (lockObj)
            {
                if (instance == null)
                {
                    // Search for existing instance.
                    instance = (VivoxVoiceManager) FindObjectOfType(typeof(VivoxVoiceManager));

                    // Create new instance if one doesn't already exist.
                    if (instance == null)
                    {
                        // Need to create a new GameObject to attach the singleton to.
                        var singletonObject = new GameObject();
                        instance = singletonObject.AddComponent<VivoxVoiceManager>();
                        singletonObject.name =
                            typeof(VivoxVoiceManager) + " (Singleton)";
                    }
                }

                // Make instance persistent even if its already in the scene
                DontDestroyOnLoad(instance.gameObject);
                return instance;
            }
        }
    }


    public LoginState LoginState { get; private set; }
    public ILoginSession loginSession;

    public VivoxUnity.IReadOnlyDictionary<ChannelId, IChannelSession> ActiveChannels =>
        loginSession?.ChannelSessions;

    public IAudioDevices AudioInputDevices => client.AudioInputDevices;
    public IAudioDevices AudioOutputDevices => client.AudioOutputDevices;

    #endregion

    #region Properties

    /// <summary>
    /// Retrieves the first instance of a session that is transmitting. 
    /// </summary>
    public IChannelSession TransmittingSession
    {
        get
        {
            if (client == null)
                throw new NullReferenceException("client");
            return client.GetLoginSession(accountId).ChannelSessions
                .FirstOrDefault(x => x.IsTransmitting);
        }
        set
        {
            if (value != null)
            {
                client.GetLoginSession(accountId)
                    .SetTransmissionMode(TransmissionMode.Single, value.Channel);
            }
        }
    }

    #endregion

    private void Awake()
    {
        if (instance != this)
        {
            Debug.LogWarning(
                "Multiple VivoxVoiceManager detected in the scene. Only one VivoxVoiceManager can exist at a time. The duplicate VivoxVoiceManager will be destroyed.");
            Destroy(this);
        }
    }

    private void Start()
    {
        if (ServerUri.ToString() == "https://GETFROMPORTAL.www.vivox.com/api2" ||
            Domain == "GET VALUE FROM VIVOX DEVELOPER PORTAL" ||
            TokenKey == "GET VALUE FROM VIVOX DEVELOPER PORTAL" ||
            TokenIssuer == "GET VALUE FROM VIVOX DEVELOPER PORTAL")
        {
            Debug.LogError(
                "The default VivoxVoiceServer values (Server, Domain, TokenIssuer, and TokenKey) must be replaced with application specific issuer and key values from your developer account.");
        }

        client.Uninitialize();

        client.Initialize();
    }

    private void OnApplicationQuit()
    {
        // Needed to add this to prevent some unsuccessful uninit, we can revisit to do better -carlo
        Client.Cleanup();
        if (client != null)
        {
            VivoxLog("Uninitializing client.");
            client.Uninitialize();
            client = null;
        }
    }

    public void Login(string displayName = null)
    {
        string uniqueId = Guid.NewGuid().ToString();
        //for proto purposes only, need to get a real token from server eventually
        accountId = new AccountId(TokenIssuer, uniqueId, Domain, displayName);
        loginSession = client.GetLoginSession(accountId);
        loginSession.PropertyChanged += OnLoginSessionPropertyChanged;
        loginSession.BeginLogin(ServerUri, loginSession.GetLoginToken(TokenKey, tokenExpiration),
            SubscriptionMode.Accept, null, null, null, ar =>
            {
                try
                {
                    loginSession.EndLogin(ar);
                }
                catch (Exception e)
                {
                    // Handle error 
                    VivoxLogError(nameof(e));
                    // Unbind if we failed to login.
                    loginSession.PropertyChanged -= OnLoginSessionPropertyChanged;
                }
            });
    }

    public void Logout()
    {
        if (loginSession != null && LoginState != LoginState.LoggedOut &&
            LoginState != LoginState.LoggingOut)
        {
            OnUserLoggedOutEvent?.Invoke();
            loginSession.PropertyChanged -= OnLoginSessionPropertyChanged;
            loginSession.Logout();
        }
    }

    public void JoinChannel(string channelName, ChannelType channelType,
        ChatCapability chatCapability,
        bool switchTransmission = true, Channel3DProperties properties = null)
    {
        if (LoginState == LoginState.LoggedIn)
        {
            ChannelId channelId =
                new ChannelId(TokenIssuer, channelName, Domain, channelType, properties);
            IChannelSession channelSession = loginSession.GetChannelSession(channelId);
            channelSession.PropertyChanged += OnChannelPropertyChanged;
            channelSession.Participants.AfterKeyAdded += OnParticipantAdded;
            channelSession.Participants.BeforeKeyRemoved += OnParticipantRemoved;
            channelSession.Participants.AfterValueUpdated += OnParticipantValueUpdated;
            channelSession.MessageLog.AfterItemAdded += OnMessageLogReceived;
            channelSession.BeginConnect(chatCapability != ChatCapability.TextOnly,
                chatCapability != ChatCapability.AudioOnly, switchTransmission,
                channelSession.GetConnectToken(TokenKey, tokenExpiration), ar =>
                {
                    try
                    {
                        channelSession.EndConnect(ar);
                    }
                    catch (Exception e)
                    {
                        // Handle error 
                        VivoxLogError($"Could not connect to voice channel: {e.Message}");
                    }
                });
        }
        else
        {
            VivoxLogError("Cannot join a channel when not logged in.");
        }
    }

    public void SendTextMessage(string messageToSend, ChannelId channel,
        string applicationStanzaNamespace = null, string applicationStanzaBody = null)
    {
        if (ChannelId.IsNullOrEmpty(channel))
        {
            throw new ArgumentException("Must provide a valid ChannelId");
        }

        if (string.IsNullOrEmpty(messageToSend))
        {
            throw new ArgumentException("Must provide a message to send");
        }

        var channelSession = loginSession.GetChannelSession(channel);
        channelSession.BeginSendText(null, messageToSend, applicationStanzaNamespace,
            applicationStanzaBody, ar =>
            {
                try
                {
                    channelSession.EndSendText(ar);
                }
                catch (Exception e)
                {
                    VivoxLog($"SendTextMessage failed with exception {e.Message}");
                }
            });
    }

    public void DisconnectAllChannels()
    {
        if (ActiveChannels?.Count > 0)
        {
            foreach (var channelSession in ActiveChannels)
            {
                channelSession?.Disconnect();
            }
        }
    }

    #region Vivox Callbacks

    private void OnMessageLogReceived(object sender,
        QueueItemAddedEventArgs<IChannelTextMessage> textMessage)
    {
        ValidateArgs(new[] {sender, textMessage});

        IChannelTextMessage channelTextMessage = textMessage.Value;
        VivoxLog(channelTextMessage.Message);
        OnTextMessageLogReceivedEvent?.Invoke(channelTextMessage.Sender.DisplayName,
            channelTextMessage);
    }

    private void OnLoginSessionPropertyChanged(object sender,
        PropertyChangedEventArgs propertyChangedEventArgs)
    {
        if (propertyChangedEventArgs.PropertyName != "State")
        {
            return;
        }

        var loginSession = (ILoginSession) sender;
        LoginState = loginSession.State;
        VivoxLog("Detecting login session change");
        switch (LoginState)
        {
            case LoginState.LoggingIn:
            {
                VivoxLog("Logging in");
                break;
            }
            case LoginState.LoggedIn:
            {
                VivoxLog("Connected to voice server and logged in.");
                OnUserLoggedInEvent?.Invoke();
                break;
            }
            case LoginState.LoggingOut:
            {
                VivoxLog("Logging out");
                break;
            }
            case LoginState.LoggedOut:
            {
                VivoxLog("Logged out");
                this.loginSession.PropertyChanged -= OnLoginSessionPropertyChanged;
                break;
            }
        }
    }

    private void OnParticipantAdded(object sender, KeyEventArg<string> keyEventArg)
    {
        ValidateArgs(new[] {sender, keyEventArg});

        // INFO: sender is the dictionary that changed and trigger the event.  Need to cast it back to access it.
        var source = (VivoxUnity.IReadOnlyDictionary<string, IParticipant>) sender;
        // Look up the participant via the key.
        var participant = source[keyEventArg.Key];
        var username = participant.Account.Name;
        var channel = participant.ParentChannelSession.Key;
        var channelSession = participant.ParentChannelSession;

        // Trigger callback
        OnParticipantAddedEvent?.Invoke(username, channel, participant);
    }

    private void OnParticipantRemoved(object sender, KeyEventArg<string> keyEventArg)
    {
        ValidateArgs(new object[] {sender, keyEventArg});

        // INFO: sender is the dictionary that changed and trigger the event.  Need to cast it back to access it.
        var source = (VivoxUnity.IReadOnlyDictionary<string, IParticipant>) sender;
        // Look up the participant via the key.
        var participant = source[keyEventArg.Key];
        var username = participant.Account.Name;
        var channel = participant.ParentChannelSession.Key;
        var channelSession = participant.ParentChannelSession;

        if (participant.IsSelf)
        {
            VivoxLog($"Unsubscribing from: {channelSession.Key.Name}");
            // Now that we are disconnected, unsubscribe.
            channelSession.PropertyChanged -= OnChannelPropertyChanged;
            channelSession.Participants.AfterKeyAdded -= OnParticipantAdded;
            channelSession.Participants.BeforeKeyRemoved -= OnParticipantRemoved;
            channelSession.Participants.AfterValueUpdated -= OnParticipantValueUpdated;
            channelSession.MessageLog.AfterItemAdded -= OnMessageLogReceived;

            // Remove session.
            var user = client.GetLoginSession(accountId);
            user.DeleteChannelSession(channelSession.Channel);
        }

        // Trigger callback
        OnParticipantRemovedEvent?.Invoke(username, channel, participant);
    }

    private static void ValidateArgs(object[] objs)
    {
        foreach (var obj in objs)
        {
            if (obj == null)
                throw new ArgumentNullException(obj.GetType().ToString(),
                    "Specify a non-null/non-empty argument.");
        }
    }

    private void OnParticipantValueUpdated(object sender,
        ValueEventArg<string, IParticipant> valueEventArg)
    {
        ValidateArgs(new[] {sender, valueEventArg});

        var source = (VivoxUnity.IReadOnlyDictionary<string, IParticipant>) sender;
        // Look up the participant via the key.
        var participant = source[valueEventArg.Key];

        string username = valueEventArg.Value.Account.Name;
        ChannelId channel = valueEventArg.Value.ParentChannelSession.Key;
        string property = valueEventArg.PropertyName;

        switch (property)
        {
            case "SpeechDetected":
            {
                VivoxLog($"OnSpeechDetectedEvent: {username} in {channel}.");
                OnSpeechDetectedEvent?.Invoke(username, channel,
                    valueEventArg.Value.SpeechDetected);
                break;
            }
            case "AudioEnergy":
            {
                OnAudioEnergyChangedEvent?.Invoke(username, channel,
                    valueEventArg.Value.AudioEnergy);
                break;
            }
        }
    }

    private void OnChannelPropertyChanged(object sender,
        PropertyChangedEventArgs propertyChangedEventArgs)
    {
        ValidateArgs(new[] {sender, propertyChangedEventArgs});

        //if (_client == null)
        //    throw new InvalidClient("Invalid client.");
        var channelSession = (IChannelSession) sender;

        // IF the channel has removed audio, make sure all the VAD indicators aren't showing speaking.
        if (propertyChangedEventArgs.PropertyName == "AudioState" &&
            channelSession.AudioState == ConnectionState.Disconnected)
        {
            VivoxLog($"Audio disconnected from: {channelSession.Key.Name}");

            foreach (var participant in channelSession.Participants)
            {
                OnSpeechDetectedEvent?.Invoke(participant.Account.Name, channelSession.Channel,
                    false);
            }
        }

        // IF the channel has fully disconnected, unsubscribe and remove.
        if ((propertyChangedEventArgs.PropertyName == "AudioState" ||
             propertyChangedEventArgs.PropertyName == "TextState") &&
            channelSession.AudioState == ConnectionState.Disconnected &&
            channelSession.TextState == ConnectionState.Disconnected)
        {
            VivoxLog($"Unsubscribing from: {channelSession.Key.Name}");
            // Now that we are disconnected, unsubscribe.
            channelSession.PropertyChanged -= OnChannelPropertyChanged;
            channelSession.Participants.AfterKeyAdded -= OnParticipantAdded;
            channelSession.Participants.BeforeKeyRemoved -= OnParticipantRemoved;
            channelSession.Participants.AfterValueUpdated -= OnParticipantValueUpdated;
            channelSession.MessageLog.AfterItemAdded -= OnMessageLogReceived;

            // Remove session.
            var user = client.GetLoginSession(accountId);
            user.DeleteChannelSession(channelSession.Channel);
        }
    }

    #endregion

    private void VivoxLog(string msg)
    {
        Debug.Log("<color=green>VivoxVoice: </color>: " + msg);
    }

    private void VivoxLogError(string msg)
    {
        Debug.LogError("<color=green>VivoxVoice: </color>: " + msg);
    }
}