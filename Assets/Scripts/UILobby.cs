using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UILobby : MonoBehaviour
{
    public static UILobby instance;

    [Header("Host Join")]
    [SerializeField] TMP_InputField joinMatchInput;
    [SerializeField] List<Selectable> lobbySelectables = new List<Selectable>();
    [SerializeField] Canvas lobbyCanvas;
    [SerializeField] Canvas searchCanvas;

    [Header("Lobby")]
    [SerializeField] Transform UIPlayerParent;
    [SerializeField] GameObject UIPlayerPrefab;
    [SerializeField] TMP_Text matchIDText;
    [SerializeField] GameObject startGameButton;

    GameObject playerLobbyUI;

    bool searching = false;

    void Start()
    {
        instance = this;
    }

    // Creates a match ID
    public void HostPrivate()
    {
        joinMatchInput.interactable = false;

        lobbySelectables.ForEach(x => x.interactable = false);

        LobbyPlayer.localPlayer.HostGame(false);
    }

    public void HostPublic()
    {
        joinMatchInput.interactable = false;

        lobbySelectables.ForEach(x => x.interactable = false);

        LobbyPlayer.localPlayer.HostGame(true);
    }

    public void HostSuccess(bool success, string matchID)
    {
        if (success)
        {
            lobbyCanvas.enabled = true;
            if (playerLobbyUI != null)
                Destroy(playerLobbyUI);
            playerLobbyUI = SpawnUIPlayerPrefab(LobbyPlayer.localPlayer);
            matchIDText.text = matchID;
            startGameButton.SetActive(true);
        }
        else
        {
            joinMatchInput.interactable = true;
            lobbySelectables.ForEach(x => x.interactable = true);
        }
    }

    public void Join()
    {
        joinMatchInput.interactable = false;
        lobbySelectables.ForEach(x => x.interactable = false);

        LobbyPlayer.localPlayer.JoinGame(joinMatchInput.text.ToUpper());
    }

    public void JoinSuccess(bool success, string matchID)
    {
        if (success)
        {
            lobbyCanvas.enabled = true;
            if (playerLobbyUI != null)
                Destroy(playerLobbyUI);
            playerLobbyUI = SpawnUIPlayerPrefab(LobbyPlayer.localPlayer);
            matchIDText.text = matchID;
        }
        else
        {
            joinMatchInput.interactable = true;
            lobbySelectables.ForEach(x => x.interactable = true);
        }
    }

    public GameObject SpawnUIPlayerPrefab(LobbyPlayer player)
    {
        GameObject newUIPlayer = Instantiate(UIPlayerPrefab, UIPlayerParent);
        newUIPlayer.GetComponent<UIPlayer>().SetPlayer(player);
        newUIPlayer.transform.SetSiblingIndex(player.playerIndex - 1);
        return newUIPlayer;
    }

    public void StartGame()
    {
        LobbyPlayer.localPlayer.StartGame();
    }

    public void SearchGame()
    {
        Debug.Log($"Searching for game...");
        StartCoroutine(SearchingForGame());
    }

    IEnumerator SearchingForGame()
    {
        searchCanvas.enabled = true;

        searching = true;
        float searchInterval = 1f;
        float currentTime = 1f;
        
        while(searching)
        {
            if (currentTime > 0)
            {
                currentTime -= Time.deltaTime;
            }
            else
            {
                currentTime = searchInterval;
                LobbyPlayer.localPlayer.SearchGame();
            }
            yield return null;
        }

        searchCanvas.enabled = false;
    }

    public void SearchSuccess(bool success, string matchID)
    {
        if (success)
        {
            searchCanvas.enabled = false;
            searching = false;
            JoinSuccess(success, matchID);
        }
    }

    public void SearchCancel()
    {
        searching = false;
    }

    public void DisconnectLobby()
    {
        if (playerLobbyUI != null)
            Destroy(playerLobbyUI);
        LobbyPlayer.localPlayer.DisconnectGame();

        lobbyCanvas.enabled = false;
        lobbySelectables.ForEach(x => x.interactable = true);
        startGameButton.SetActive(false);
    }
}
