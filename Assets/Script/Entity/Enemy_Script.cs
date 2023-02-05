using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Script : MonoBehaviour
{ 
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Data.enemy_life -= collision.collider.GetComponent<Projectile_Script>().damage;
        Destroy(collision.collider.gameObject);
    }
}
