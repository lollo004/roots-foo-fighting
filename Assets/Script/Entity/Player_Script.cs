using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Unity.Services.Lobbies.Models;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player_Script : MonoBehaviour
{
    //GUI
    [SerializeField] public TextMeshProUGUI text_clorofilla;
    [SerializeField] public TextMeshProUGUI text_player_life;
    [SerializeField] public TextMeshProUGUI text_enemy_life;
    [SerializeField] public TextMeshProUGUI text_time;

    //time
    public int min = 5;
    public int sec = 0;

    //tilemaps
    [SerializeField] GameObject[] tilemaps_player = new GameObject[5];
    [SerializeField] GameObject[] tilemaps_enemy = new GameObject[5];

    //player health
    public int health = 150;

    //amount of coin
    [SerializeField] public int clorofilla = 100;
    //revenue of coin
    [SerializeField] public int revenue = 10;

    [SerializeField] public Projectile_Script projectile_player;
    [SerializeField] public Projectile_Script projectile_enemy;

    //selcted item
    public string selected_item = "";

    public int kill = 0;

    private void Start()
    {
        InvokeRepeating("Gain_Clorofilla", 1.0f, 1.5f);
        InvokeRepeating("TimerFunction", 0.1f, 1.0f);
    }

    private void Update()
    {
        // Skip update logic if the Host is not yet bound.
        if (LobbyRelayUTP.hostDriver.IsCreated && LobbyRelayUTP.hostDriver.Bound)
        {
            if (LobbyRelayUTP.hostMessagesBuffer.TryDequeue(out var cmd))
            {
                var command = cmd.Split("|");
                if (command[0] == "PLAY")
                {
                    GameObject[] obj = GameObject.FindGameObjectsWithTag("plants_menu");
                    for (int i = 0; i < obj.Length; i++)
                    {
                        var o = obj[i].GetComponent<Item_Script>();
                        if (command[1].ToString() == o.selected_item)
                        {
                            obj[i].GetComponent<Item_Script>().menu = false;
                            obj[i].GetComponent<Item_Script>().team = "enemy";
                            GameObject go = Instantiate(obj[i]);
                            obj[i].GetComponent<Item_Script>().team = "player";
                            obj[i].GetComponent<Item_Script>().menu = true;
                            float x = float.Parse(command[2], CultureInfo.InvariantCulture.NumberFormat);
                            float y = float.Parse(command[3], CultureInfo.InvariantCulture.NumberFormat);
                            if (SystemInfo.operatingSystem[0] == 'M') { x /= 10; y /= 10; }
                            Vector3 vec = new Vector3(-x, y, 30);
                            go.transform.SetPositionAndRotation(vec, go.transform.rotation);
                            break;
                        }
                    }
                }
                else if (command[0] == "START")
                {
                    Data.selected_tilemap = int.Parse(command[1]);
                }
            }
        }
        else if (LobbyRelayUTP.playerDriver.IsCreated && LobbyRelayUTP.playerDriver.Bound)
        {
            if (LobbyRelayUTP.playerMessagesBuffer.TryDequeue(out var cmd))
            {
                Debug.Log("Message arrived: " + cmd);
                var command = cmd.Split("|");
                if (command[0] == "PLAY")
                {
                    GameObject[] obj = GameObject.FindGameObjectsWithTag("plants_menu");
                    for (int i = 0; i < obj.Length; i++)
                    {
                        var o = obj[i].GetComponent<Item_Script>();
                        if (command[1].ToString() == o.selected_item)
                        {
                            obj[i].GetComponent<Item_Script>().menu = false;
                            obj[i].GetComponent<Item_Script>().team = "enemy";
                            GameObject go = Instantiate(obj[i]);
                            obj[i].GetComponent<Item_Script>().team = "player";
                            obj[i].GetComponent<Item_Script>().menu = true;
                            float x = float.Parse(command[2], CultureInfo.InvariantCulture.NumberFormat);
                            float y = float.Parse(command[3], CultureInfo.InvariantCulture.NumberFormat);
                            if (SystemInfo.operatingSystem[0] == 'M') { x /= 10; y /= 10; }
                            Vector3 vec = new Vector3(-x, y, 30);
                            go.transform.SetPositionAndRotation(vec, go.transform.rotation);
                            break;
                        }
                    }
                }
                else if (command[0] == "START")
                {
                    Data.selected_tilemap = int.Parse(command[1]);
                    tilemaps_player[int.Parse(command[1])].SetActive(true);
                    tilemaps_enemy[int.Parse(command[1])].SetActive(true);
                }
            }
        }
        text_clorofilla.SetText("Chlorophyll level: " + clorofilla);
        text_player_life.SetText("Player Health: " + health);
        text_enemy_life.SetText("Enemy Health: " + Data.enemy_life);

        if (health <= 0)
        {
            Data.message = "You died, try again... maybe better next time!";
            SceneManager.LoadScene("Lose");
        }
        if (Data.enemy_life <= 0)
        {
            Data.message = "Good work, you won!";
            SceneManager.LoadScene("Win");
        }
    }

    private void TimerFunction()
    {
        //time update
        sec--;
        if (sec < 0)
        {
            sec = 59;
            min--;
        }
        if (min <= 0 && sec <= 0)
        {
            Data.message = "Time has ended, no one wons, bye!";
            SceneManager.LoadScene("Lose");
        }
        //GUI update
        if (sec >= 10)
        {
            text_time.SetText("Time Remaning: " + min + ":" + sec);
        }
        else
        {
            text_time.SetText("Time Remaning: " + min + ":0" + sec);
        }
    }

    private void Gain_Clorofilla()
    {
        clorofilla += revenue;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        health -= collision.collider.GetComponent<Projectile_Script>().damage;
    }
}