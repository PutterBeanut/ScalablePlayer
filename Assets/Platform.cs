using System.Collections;
using CriticalAngle.ExpandablePlayer;
using UnityEngine;

public class Platform : MonoBehaviour
{
    public float maxTime = 2.0f;
    public float moveDistance = 5.0f;
    public float waitTime = 2.0f;

    private void Start()
    {
        this.StartCoroutine(this.WaitThenUp());
    }

    private IEnumerator MoveUp()
    {
        var time = 0.0f;
        var beginPosition = this.transform.position;
        var endPosition = beginPosition + this.moveDistance.V3Y();
        
        while (time < this.maxTime)
        {
            this.transform.position = Vector3.Lerp(beginPosition, endPosition, time / this.maxTime);
            time += Time.deltaTime;
            yield return null;
        }

        this.StartCoroutine(this.WaitThenDown());
    }

    private IEnumerator WaitThenDown()
    {
        yield return new WaitForSeconds(this.waitTime);
        this.StartCoroutine(this.MoveDown());
    }
    
    private IEnumerator MoveDown()
    {
        var time = 0.0f;
        var beginPosition = this.transform.position;
        var endPosition = beginPosition - this.moveDistance.V3Y();
        
        while (time < this.maxTime)
        {
            this.transform.position = Vector3.Lerp(beginPosition, endPosition, time / this.maxTime);
            time += Time.deltaTime;
            yield return null;
        }

        this.StartCoroutine(this.WaitThenUp());
    }
    
    private IEnumerator WaitThenUp()
    {
        yield return new WaitForSeconds(this.waitTime);
        this.StartCoroutine(this.MoveUp());
    }
}
