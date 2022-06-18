using System.Collections;
using UnityEngine;

public class Platform : MonoBehaviour
{
    public float maxTime = 2.0f;
    public Vector3 velocity;

    private bool waiting;
    private bool movingUp = true;

    private float time;

    private bool hasAssignedPosition = false;
    private Vector3 position;

    private void FixedUpdate()
    {
        if (this.waiting)
        {
            this.time += Time.fixedDeltaTime;

            if (this.time > 1.0f)
                this.waiting = false;
        }

        if (!this.hasAssignedPosition)
        {
            position = this.transform.position;
            this.hasAssignedPosition = true;
        }

        var offset = this.movingUp ? 5.0f : -5.0f;
        var endPosition = position + new Vector3(0.0f, offset, 0.0f);

        if (time < this.maxTime)
        {
            var oldPosition = this.transform.position;
            this.GetComponent<Rigidbody>().MovePosition(Vector3.Lerp(position, endPosition, time / this.maxTime));
            this.velocity = transform.position - oldPosition;

            this.time += Time.fixedDeltaTime;
            return;
        }

        this.hasAssignedPosition = false;
        this.movingUp = !this.movingUp;
        this.time = 0.0f;
    }
}
