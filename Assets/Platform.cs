using System.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class Platform : MonoBehaviour
{
    public float maxTime = 2.0f;

    private void Awake()
    {
        this.StartCoroutine(this.MoveUp());
    }

    private IEnumerator MoveUp()
    {
        var time = 0.0f;

        var position = this.transform.position;
        var endPosition = position + new Vector3(0.0f, 5.0f, 0.0f);

        while (time < this.maxTime)
        {
            this.transform.position = Vector3.Lerp(position, endPosition, time / this.maxTime);

            time += Time.deltaTime;

            yield return null;
        }

        
        this.StartCoroutine(this.PauseUp());
    }

    private IEnumerator MoveDown()
    {
        var time = 0.0f;

        var position = this.transform.position;
        var endPosition = position - new Vector3(0.0f, 5.0f, 0.0f);

        while (time < this.maxTime)
        {
            this.transform.position = Vector3.Lerp(position, endPosition, time / this.maxTime);

            time += Time.deltaTime;
            yield return null;
        }
        
        this.StartCoroutine(this.PauseDown());
    }

    private IEnumerator PauseDown()
    {
        yield return new WaitForSeconds(1.0f);
        this.StartCoroutine(this.MoveUp());
    }
    
    private IEnumerator PauseUp()
    {
        yield return new WaitForSeconds(1.0f);
        this.StartCoroutine(this.MoveDown());
    }
}
