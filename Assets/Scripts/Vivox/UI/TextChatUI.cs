using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VivoxUnity;
using System.Collections.Generic;
using System.Collections;

// TODO: file from Vivox sample; not implemented yet
/// <summary>
/// Text Chat UI in game scene
/// </summary>
public class TextChatUI : MonoBehaviour
{
    private VivoxVoiceManager vivoxVoiceManager;
    private const string LobbyChannelName = "lobbyChannel";
    private ChannelId lobbyChannelId;
    private List<GameObject> messageObjPool = new List<GameObject>();
    private ScrollRect textChatScrollRect;

    public GameObject ChatContentObj;
    public GameObject MessageObject;
    public Button EnterButton;
    public Button SendTtsMessageButton;
    public InputField MessageInputField;
    public Toggle ToggleTts;


    private void Awake()
    {
        textChatScrollRect = GetComponent<ScrollRect>();
        vivoxVoiceManager = VivoxVoiceManager.Instance;
        if (messageObjPool.Count > 0)
        {
            ClearMessageObjectPool();
        }

        ClearOutTextField();

        vivoxVoiceManager.OnParticipantAddedEvent += OnParticipantAdded;
        vivoxVoiceManager.OnTextMessageLogReceivedEvent += OnTextMessageLogReceivedEvent;

#if !(UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID || UNITY_STADIA)
        MessageInputField.gameObject.SetActive(false);
        EnterButton.gameObject.SetActive(false);
        SendTTSMessageButton.gameObject.SetActive(false);
#else
        EnterButton.onClick.AddListener(SubmitTextToVivox);
        MessageInputField.onEndEdit.AddListener(text => { EnterKeyOnTextField(); });
        SendTtsMessageButton.onClick.AddListener(SubmitTtsMessageToVivox);
        ToggleTts.onValueChanged.AddListener(TtsToggleValueChanged);

#endif
        if (vivoxVoiceManager.ActiveChannels.Count > 0)
        {
            lobbyChannelId = vivoxVoiceManager.ActiveChannels
                .FirstOrDefault(ac => ac.Channel.Name == LobbyChannelName)?.Key;
        }
    }


    private void OnDestroy()
    {
        vivoxVoiceManager.OnParticipantAddedEvent -= OnParticipantAdded;
        vivoxVoiceManager.OnTextMessageLogReceivedEvent -= OnTextMessageLogReceivedEvent;

#if UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID || UNITY_STADIA
        EnterButton.onClick.RemoveAllListeners();
        MessageInputField.onEndEdit.RemoveAllListeners();
        SendTtsMessageButton.onClick.RemoveAllListeners();
        ToggleTts.onValueChanged.RemoveAllListeners();
#endif
    }


    private void TtsToggleValueChanged(bool toggleTts)
    {
        if (!ToggleTts.isOn)
        {
            vivoxVoiceManager.loginSession.TTS.CancelDestination(TTSDestination
                .QueuedLocalPlayback);
        }
    }


    private void ClearMessageObjectPool()
    {
        for (int i = 0; i < messageObjPool.Count; i++)
        {
            Destroy(messageObjPool[i]);
        }

        messageObjPool.Clear();
    }

    private void ClearOutTextField()
    {
        MessageInputField.text = string.Empty;
        MessageInputField.Select();
        MessageInputField.ActivateInputField();
    }


    private void EnterKeyOnTextField()
    {
        if (!Input.GetKeyDown(KeyCode.Return))
        {
            return;
        }

        SubmitTextToVivox();
    }

    private void SubmitTextToVivox()
    {
        if (string.IsNullOrEmpty(MessageInputField.text))
        {
            return;
        }

        vivoxVoiceManager.SendTextMessage(MessageInputField.text, lobbyChannelId);
        ClearOutTextField();
    }

    public static string TruncateAtWord(string value, int length)
    {
        if (value == null || value.Length < length ||
            value.IndexOf(" ", length, StringComparison.Ordinal) == -1)
            return value;

        return value.Substring(0, value.IndexOf(" ", length, StringComparison.Ordinal));
    }


    //public string[] TruncateWithPreservation(string s, int len)
    //{
    //    string[] words = s.Split(' ');
    //    string[] sections;

    //    StringBuilder sb = new StringBuilder();

    //    string currentString;

    //    foreach (string word in words)
    //    {
    //        if (sb.Length + word.Length > len)

    //            currentString = Strin;
    //            break;
    //        currentString += " ";
    //        currentString += word;
    //    }

    //    return sb.ToString();
    //}

    private void SubmitTtsMessageToVivox()
    {
        if (string.IsNullOrEmpty(MessageInputField.text))
        {
            return;
        }

        var ttsMessage = new TTSMessage(MessageInputField.text,
            TTSDestination.QueuedRemoteTransmissionWithLocalPlayback);
        vivoxVoiceManager.loginSession.TTS.Speak(ttsMessage);
        ClearOutTextField();
    }

    private IEnumerator SendScrollRectToBottom()
    {
        yield return new WaitForEndOfFrame();

        // We need to wait for the end of the frame for this to be updated, otherwise it happens too quickly.
        textChatScrollRect.normalizedPosition = new Vector2(0, 0);

        yield return null;
    }

    public void DisplayHostingMessage(IChannelTextMessage channelTextMessage)
    {
        var newMessageObj = Instantiate(MessageObject, ChatContentObj.transform);
        messageObjPool.Add(newMessageObj);
        Text newMessageText = newMessageObj.GetComponent<Text>();

        if (channelTextMessage.ApplicationStanzaNamespace.EndsWith(VivoxNetworkManager.MatchStatus
            .Open.ToString()))
        {
            newMessageText.alignment = TextAnchor.MiddleLeft;
            newMessageText.text = string.Format(
                $"<color=blue>{channelTextMessage.Sender.DisplayName} has begun hosting a match.</color>\n<color=#5A5A5A><size=8>{channelTextMessage.ReceivedTime}</size></color>");
        }
        else if (channelTextMessage.ApplicationStanzaNamespace.EndsWith(VivoxNetworkManager
            .MatchStatus.Closed.ToString()))
        {
            newMessageText.alignment = TextAnchor.MiddleLeft;
            newMessageText.text =
                string.Format(
                    $"<color=blue>{channelTextMessage.Sender.DisplayName}'s match has ended.</color>\n<color=#5A5A5A><size=8>{channelTextMessage.ReceivedTime}</size></color>");
        }
    }

    #region Vivox Callbacks

    void OnParticipantAdded(string username, ChannelId channel, IParticipant participant)
    {
        if (vivoxVoiceManager.ActiveChannels.Count > 0)
        {
            lobbyChannelId = vivoxVoiceManager.ActiveChannels.FirstOrDefault()?.Channel;
        }
    }

    private void OnTextMessageLogReceivedEvent(string sender,
        IChannelTextMessage channelTextMessage)
    {
        if (!String.IsNullOrEmpty(channelTextMessage.ApplicationStanzaNamespace))
        {
            // If we find a message with an ApplicationStanzaNamespace we don't push that to the chat box.
            // Such messages denote opening/closing or requesting the open status of multiplayer matches.
            return;
        }

        var newMessageObj = Instantiate(MessageObject, ChatContentObj.transform);
        messageObjPool.Add(newMessageObj);
        Text newMessageText = newMessageObj.GetComponent<Text>();

        if (channelTextMessage.FromSelf)
        {
            newMessageText.alignment = TextAnchor.MiddleRight;
            newMessageText.text =
                string.Format(
                    $"{channelTextMessage.Message} :<color=blue>{sender} </color>\n<color=#5A5A5A><size=8>{channelTextMessage.ReceivedTime}</size></color>");
            StartCoroutine(SendScrollRectToBottom());
        }
        else
        {
            newMessageText.alignment = TextAnchor.MiddleLeft;
            newMessageText.text =
                string.Format(
                    $"<color=green>{sender} </color>: {channelTextMessage.Message}\n<color=#5A5A5A><size=8>{channelTextMessage.ReceivedTime}</size></color>");
            if (ToggleTts.isOn)
            {
                // Speak local tts message with incoming text message
                new TTSMessage($"{sender} said,", TTSDestination.QueuedLocalPlayback).Speak(
                    vivoxVoiceManager.loginSession);
                new TTSMessage($"{channelTextMessage.Message}", TTSDestination.QueuedLocalPlayback)
                    .Speak(vivoxVoiceManager.loginSession);
            }
        }
    }

    #endregion
}