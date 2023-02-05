using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile_Script : MonoBehaviour
{
    public int damage;
    public string team;
    public bool menu;
    public GameObject sender;

    void Start()
    {
        if (!menu)
        {
            InvokeRepeating("Movement", 0.1f, 0.05f);
        }
    }
    
    private void Movement()
    {
        //movement direction
        float direciton = 0.2f;
        if (team == "enemy")
        {
            direciton *= -1;
        }
        //movment of the object
        gameObject.transform.SetPositionAndRotation(
            new Vector3(
                transform.position.x+ direciton,
                transform.position.y,
                transform.position.z
            ), 
            transform.rotation
        );
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        try
        {
            if (collision.collider.GetComponent<Item_Script>().team != team)
            {
                collision.collider.GetComponent<Item_Script>().health -= damage;
                sender.GetComponent<Item_Script>().health -= collision.collider.GetComponent<Item_Script>().knockback;
                Data.points += collision.collider.GetComponent<Item_Script>().health * 100;
                Data.points += collision.collider.GetComponent<Item_Script>().cost * 10;
                Destroy(gameObject);
            }
        } catch { }
        
    }
}
