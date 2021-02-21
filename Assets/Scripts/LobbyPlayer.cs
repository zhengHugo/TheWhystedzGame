using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

public class LobbyPlayer : NetworkBehaviour
{
    public static LobbyPlayer localPlayer;
    [SyncVar] public string matchID;
    [SyncVar] public int playerIndex;
    NetworkMatchChecker networkMatchChecker;

    void Start()
    {
        networkMatchChecker = GetComponent<NetworkMatchChecker>();

        if (isLocalPlayer)
        {
            localPlayer = this;
        }
        else
        {
            UILobby.instance.SpawnUIPlayerPrefab(this);
        }
    }

    //---- HOST GAME LOGIC ----

    public void HostGame()
    {
        string matchID = MatchMaker.GetRandomMatchID();
        CmdHostGame(matchID);
    }

    [Command]
    void CmdHostGame(string _matchID)
    {
        if (MatchMaker.instance.HostGame(_matchID, gameObject, out playerIndex))
        {
            matchID = _matchID;
            Debug.Log($"<color = green>Game hosted successfully</color>");
            networkMatchChecker.matchId = _matchID.ToGuid();
            TargetHostGame(true, _matchID, playerIndex);
        }
        else
        {
            Debug.Log($"<color = red>Game host failed</color>");
            TargetHostGame(false, _matchID, playerIndex);
        }
    }

    [TargetRpc]
    void TargetHostGame(bool success, string _matchID, int _playerIndex)
    {
        playerIndex = _playerIndex;
        Debug.Log($"Match ID: {matchID} == {_matchID}");
        UILobby.instance.HostSuccess(success, _matchID);
    }

    //---- JOIN GAME LOGIC ----

    public void JoinGame(string _inputID)
    {
        CmdJoinGame(_inputID);
    }

    [Command]
    void CmdJoinGame(string _matchID)
    {
        if (MatchMaker.instance.JoinGame(_matchID, gameObject, out playerIndex))
        {
            matchID = _matchID;
            Debug.Log($"<color = green>Game joined successfully</color>");
            networkMatchChecker.matchId = _matchID.ToGuid();
            TargetJoinGame(true, _matchID, playerIndex);
        }
        else
        {
            Debug.Log($"<color = red>Game join failed</color>");
            TargetJoinGame(false, _matchID, playerIndex);
        }
    }

    [TargetRpc]
    void TargetJoinGame(bool success, string _matchID, int _playerIndex)
    {
        playerIndex = _playerIndex;
        matchID = _matchID;
        Debug.Log($"Match ID: {matchID} == {_matchID}");
        UILobby.instance.JoinSuccess(success, _matchID);
    }

    //---- START GAME LOGIC ----

    public void StartGame()
    {
        CmdStartGame();
    }

    [Command]
    void CmdStartGame()
    {
        MatchMaker.instance.StartGame(matchID);
        Debug.Log($"<color = red>Game starting</color>");
    }

    public void StartMatch()
    {
        TargetStartGame();
    }

    [TargetRpc]
    void TargetStartGame()
    {
        Debug.Log($"Match ID: {matchID} | Starting...");
        // Load game scene
        SceneManager.LoadScene(2);
    }
}
