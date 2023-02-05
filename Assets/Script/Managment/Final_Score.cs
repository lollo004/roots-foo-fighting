using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Final_Score : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private TextMeshProUGUI message;
    void Start()
    {
        text.SetText("Points: " + Data.points);
        message.SetText(Data.message);
    }
}
