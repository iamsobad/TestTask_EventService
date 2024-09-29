using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class EventService_Coroutine : MonoBehaviour
{
    [SerializeField]
    private string serverUrl = "https://example-url.com";
    [SerializeField]
    private float cooldownBeforeSend = 3f;

    // currently processing events
    private WrapperEvents sendingEvents;

    // events waiting for sending
    private WrapperEvents waitedEvents;

    private Coroutine sendingCoroutine;
    private Coroutine cooldownCoroutine;

    private bool CanSend => cooldownCoroutine == null && sendingCoroutine == null;

    void Start()
    {
        sendingEvents = new WrapperEvents("sendingEvents");
        waitedEvents = new WrapperEvents("waitedEvents");

        if (!sendingEvents.IsEmpty || !waitedEvents.IsEmpty)
            SendEvents();
    }

    public void TrackEvent(string type, string data)
    {
        waitedEvents.Add(new Event { data = data, type = type });
        if (CanSend)
            SendEvents();
    }

    private void SendEvents()
    {
        // add new events for sending
        sendingEvents.Add(waitedEvents);
        waitedEvents.Clear();
        sendingCoroutine = StartCoroutine(SendingCoroutine());
    }

    private IEnumerator SendingCoroutine()
    {
        bool successful;
        StartCooldown();

        using (UnityWebRequest request = UnityWebRequest.Post(serverUrl, sendingEvents.ToJson(), "application/json"))
        {
            yield return request.SendWebRequest();
            successful = request.result == UnityWebRequest.Result.Success && request.responseCode == 200;
        }

        if (successful)
        {
            sendingCoroutine = null;
            sendingEvents.Clear();
            // if sending take more time than cooldown
            if (cooldownCoroutine == null && !waitedEvents.IsEmpty)
                SendEvents();
        }
        else
            // retry to send
            SendEvents();
    }

    private void StartCooldown()
    {
        if (cooldownCoroutine != null)
            StopCoroutine(cooldownCoroutine);
        cooldownCoroutine = StartCoroutine(CooldownCoroutine());
    }

    private IEnumerator CooldownCoroutine()
    {
        yield return new WaitForSeconds(cooldownBeforeSend);
        cooldownCoroutine = null;
        if (!waitedEvents.IsEmpty && sendingCoroutine == null)
            SendEvents();

    }

    // wrapper for JsonUtility (doesn't work for arrays and lists directly)
    // also has logic for save/load
    [System.Serializable]
    private class WrapperEvents
    {
        private string prefKey;
        public List<Event> events;
        public WrapperEvents(string prefKey)
        {
            this.prefKey = prefKey;
            Load();
        }

        public bool IsEmpty => events.Count == 0;

        public void Add(Event evt)
        {
            events.Add(evt);
            Save();
        }

        public void Add(WrapperEvents evt)
        {
            events.AddRange(evt.events);
            Save();
        }

        public void Clear()
        {
            events.Clear();
            Save();
        }

        public string ToJson() => JsonUtility.ToJson(this);

        private void Save() => PlayerPrefs.SetString(prefKey, ToJson());

        private void Load()
        {
            string saveString = PlayerPrefs.GetString(prefKey, string.Empty);
            if (!string.IsNullOrEmpty(saveString))
                events = JsonUtility.FromJson<WrapperEvents>(saveString).events;
            else
                events = new();
        }
    }

    [System.Serializable]
    private class Event
    {
        public string type;
        public string data;
    }
}
