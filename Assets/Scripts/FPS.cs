using System;
using UnityEngine;
using UnityEngine.UI;

public class FPS : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField, Range(0.1f, 1.0f)] private float interval = 0.5f;

    int frames = 0;
    float elapsed = 0f;


    private void Update()
    {
        frames++;
        elapsed += Time.unscaledDeltaTime;

        if (elapsed >= interval)
        {
            float fps = frames / elapsed;
            float ms = 1000f / Mathf.Max(fps, 0.0001f);

            if (label)
            {
                label.text = $"FPS {fps:0.#}  ({ms:0.0})";
            }

            frames = 0;
            elapsed = 0f;
        }
    }
}
