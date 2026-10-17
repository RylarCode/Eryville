using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// What an HTTP request came back with
public class HttpResult
{
    public long status;          // the HTTP status (200 = fine), 0 if there was no answer
    public string body = "";
    public bool networkError;    // could not reach the server at all
    public string error = "";
}

// The one thing the Firebase backend needs from the network: send some text, get some text back.
// A small interface so the backend can be tested without a network.
public interface IHttpClient
{
    void Post(string url, string body, string contentType, string bearerToken, Action<HttpResult> done);
}

// The real thing, on UnityWebRequest
public class UnityHttpClient : IHttpClient
{
    private const int TimeoutSeconds = 20;

    private class Runner : MonoBehaviour { }
    private static Runner runner;

    private static Runner GetRunner()
    {
        if (runner != null) return runner;
        GameObject go = new GameObject("Http");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
        return runner;
    }

    public void Post(string url, string body, string contentType, string bearerToken, Action<HttpResult> done)
    {
        GetRunner().StartCoroutine(Send(url, body, contentType, bearerToken, done));
    }

    private static IEnumerator Send(string url, string body, string contentType, string bearerToken, Action<HttpResult> done)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            if (!string.IsNullOrEmpty(bearerToken)) request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
            request.timeout = TimeoutSeconds;

            yield return request.SendWebRequest();

            HttpResult result = new HttpResult
            {
                status = request.responseCode,
                body = request.downloadHandler != null ? request.downloadHandler.text : "",
                networkError = request.result == UnityWebRequest.Result.ConnectionError,
                error = request.error ?? "",
            };
            done?.Invoke(result);
        }
    }
}
