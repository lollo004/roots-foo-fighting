using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class Item_Script : MonoBehaviour
{
    [SerializeField] public string selected_item;

    public Player_Script player;

    [SerializeField] public int health;

    [SerializeField] public int revenue;

    [SerializeField] public int knockback;

    [SerializeField] public int damage;

    [SerializeField] public int cost;

    [SerializeField] public bool menu = false;

    [SerializeField] public string team = "player";

    private void Start()
    {
        if (team == "player")
        {
            player = FindObjectOfType<Player_Script>();
            player.revenue += revenue;
        }

        if (damage > 0 && !menu)
        {
            InvokeRepeating("Attacco", 0.1f, 3.0f);
        }
    }

    private void Update()
    {
        if (health <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnMouseDown()
    {   
        if (gameObject.tag == "fuoco")
        {
            if (player.clorofilla >= 500)
            {
                Data.message = "Fire is too much powerfull for you miserable man, you can't control it.";
                SceneManager.LoadScene("Lose");
                return;
            }
        }
        else
        {
            player.selected_item = selected_item;
        }
    }

    private void Attacco()
    {
        if (team == "player")
        {
            Projectile_Script pr = GameObject.Instantiate(player.projectile_player);
            pr.damage = damage;
            pr.sender = gameObject;
            pr.transform.SetPositionAndRotation(
                new Vector3(
                    gameObject.transform.position.x,
                    gameObject.transform.position.y,
                    gameObject.transform.position.z
                ),
                pr.transform.rotation
            );
            pr.menu = false;
        }
        else
        {
            Projectile_Script pr = GameObject.Instantiate(player.projectile_enemy);
            pr.damage = damage;
            pr.sender = gameObject;
            pr.transform.SetPositionAndRotation(
                new Vector3(
                    gameObject.transform.position.x,
                    gameObject.transform.position.y,
                    gameObject.transform.position.z
                ),
                pr.transform.rotation
            );
            pr.menu = false;
        }
        
    }
}
