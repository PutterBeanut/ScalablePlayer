using System.Collections;
using UnityEngine;

public class Platform : MonoBehaviour
{
    private void Awake()
    {
        this.StartCoroutine(this.MoveUp());
    }

    private IEnumerator MoveUp()
    {
        var time = 0.0f;

        var position = this.transform.position;
        var endPosition = position + new Vector3(0.0f, 5.0f, 0.0f);

        while (time < 4.0f)
        {
            this.transform.position = Vector3.Lerp(position, endPosition, time / 4.0f);

            time += Time.deltaTime;

            yield return null;
        }

        
        this.StartCoroutine(this.MoveDown());
    }

    private IEnumerator MoveDown()
    {
        var time = 0.0f;

        var position = this.transform.position;
        var endPosition = position - new Vector3(0.0f, 5.0f, 0.0f);

        while (time < 4.0f)
        {
            this.transform.position = Vector3.Lerp(position, endPosition, time / 4.0f);

            time += Time.deltaTime;
            yield return null;
        }
        
        this.StartCoroutine(this.MoveUp());
    }
}
