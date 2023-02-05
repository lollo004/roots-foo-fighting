using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class Detect_Tilemap_Click : MonoBehaviour
{    
    //mouse pos
    public Vector3 mouse_pos;

    //item
    public GameObject item;
    [SerializeField] public GameObject[] items = new GameObject[7];

    //player
    public Player_Script player;
    private void Start()
    {
        player = FindObjectOfType<Player_Script>();
    }

    private void OnMouseDown()
    {
        if (player.selected_item != "") 
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].GetComponent<Item_Script>().selected_item == player.selected_item)
                {
                    item = items[i];
                    break;
                }
                
            }

            mouse_pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            mouse_pos.x = (float)Math.Floor(mouse_pos.x) + 0.5f;
            mouse_pos.y = (float)Math.Floor(mouse_pos.y) + 0.5f;
            mouse_pos.z = 30;

            if (player.clorofilla >= item.GetComponent<Item_Script>().cost)
            {
                try
                {
                    item.GetComponent<Item_Script>().menu = false;
                    GameObject go = Instantiate(item);

                    go.transform.SetPositionAndRotation(mouse_pos, new Quaternion(0, 0, 0, 0));
                    if (LobbyRelayUTP.hostDriver.IsCreated && LobbyRelayUTP.hostDriver.Bound)
                    {
                        LobbyRelayUTP.OnHostSendMessage(
                            "PLAY" + "|"
                            + go.GetComponent<Item_Script>().selected_item + "|"
                            + go.transform.position.x.ToString() + "|"
                            + go.transform.position.y.ToString()
                            );
                    }
                    else if (LobbyRelayUTP.playerDriver.IsCreated && LobbyRelayUTP.playerDriver.Bound)
                    {
                        LobbyRelayUTP.OnPlayerSendMessage(
                            "PLAY" + "|"
                            + go.GetComponent<Item_Script>().selected_item + "|"
                            + go.transform.position.x.ToString() + "|"
                            + go.transform.position.y.ToString()
                            );
                    }
                    player.clorofilla -= item.GetComponent<Item_Script>().cost;
                    Data.points += item.GetComponent<Item_Script>().cost * 10;
                    player.selected_item = "";
                }
                catch (UnassignedReferenceException) { }
            }
        }
    }
}
