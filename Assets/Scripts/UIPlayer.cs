using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIPlayer : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    LobbyPlayer player;

    public void SetPlayer(LobbyPlayer player)
    {
        this.player = player;
        text.text = "Player " + player.playerIndex.ToString();
    }
}
