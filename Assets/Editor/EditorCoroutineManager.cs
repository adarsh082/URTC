using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[InitializeOnLoad]
public static class EditorCoroutineUtility
{
    private class EditorCoroutine
    {
        public IEnumerator routine;
        public bool isDone;
        public float waitUntilTime = 0f;
        public UnityWebRequestAsyncOperation webRequestOp = null;
    }

    private static List<EditorCoroutine> coroutines = new List<EditorCoroutine>();
    private static bool isRunning;

    static EditorCoroutineUtility()
    {
        StartUpdateLoop();
    }

    private static void StartUpdateLoop()
    {
        if (!isRunning)
        {
            EditorApplication.update += Update;
            isRunning = true;
        }
    }

    private static void StopUpdateLoop()
    {
        if (isRunning)
        {
            EditorApplication.update -= Update;
            isRunning = false;
        }
    }

    public static void StartCoroutine(IEnumerator routine)
    {
        if (routine != null)
        {
            coroutines.Add(new EditorCoroutine { routine = routine });
            StartUpdateLoop();
        }
    }

    public static void StopCoroutine(IEnumerator routine)
    {
        coroutines.RemoveAll(c => c.routine == routine);
    }

    public static void StopAllCoroutines()
    {
        coroutines.Clear();
        StopUpdateLoop();
    }

    private static void Update()
    {
        float currentTime = (float)EditorApplication.timeSinceStartup;

        for (int i = coroutines.Count - 1; i >= 0; i--)
        {
            EditorCoroutine coroutine = coroutines[i];

            if (coroutine.isDone)
            {
                coroutines.RemoveAt(i);
                continue;
            }

            // Wait for time delay
            if (coroutine.waitUntilTime > currentTime)
            {
                continue;
            }

            // Wait for UnityWebRequest
            if (coroutine.webRequestOp != null && !coroutine.webRequestOp.isDone)
            {
                continue;
            }

            try
            {
                bool movedNext = coroutine.routine.MoveNext();

                if (!movedNext)
                {
                    coroutine.isDone = true;
                    coroutines.RemoveAt(i);
                    continue;
                }

                object yielded = coroutine.routine.Current;

                // Handle known yield types
                if (yielded == null)
                {
                    coroutine.waitUntilTime = currentTime; // resume next update
                    coroutine.webRequestOp = null;
                }
                else if (yielded is WaitForSeconds wait)
                {
                    coroutine.waitUntilTime = currentTime + wait.waitTime;
                    coroutine.webRequestOp = null;
                }
                else if (yielded is UnityWebRequestAsyncOperation op)
                {
                    coroutine.webRequestOp = op;
                    coroutine.waitUntilTime = 0f;
                }
                else
                {
                    Debug.LogWarning("Unsupported yield type: " + yielded.GetType().Name);
                    coroutine.waitUntilTime = currentTime;
                    coroutine.webRequestOp = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EditorCoroutine] Exception: {ex.Message}\n{ex.StackTrace}");
                coroutine.isDone = true;
                coroutines.RemoveAt(i);
            }
        }

        if (coroutines.Count == 0)
        {
            StopUpdateLoop();
        }
    }
}



public class WaitForSeconds
{
    public float waitTime;

    public WaitForSeconds(float time)
    {
        waitTime = time;
    }
}