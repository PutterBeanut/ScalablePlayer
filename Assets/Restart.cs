using System.Collections;
using System.Collections.Generic;
using CriticalAngle.ExpandablePlayer;
using UnityEngine;

public class Restart : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            this.transform.position = Vector3.one;
            this.GetComponent<Player>().Velocity = Vector3.zero;
        }
    }
}
