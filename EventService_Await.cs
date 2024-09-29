using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class EventService_Await : MonoBehaviour
{
    [SerializeField]
    private string serverUrl = "https://example-url.com";
    [SerializeField]
    private float cooldownBeforeSend = 3f;

    // currently processing events
    private WrapperEvents sendingEvents;

    // events waiting for sending
    private WrapperEvents waitedEvents;

    private bool isSending = false;
    private bool isCooldown = false;

    private bool CanSend => !isSending && !isCooldown;

    void Start()
    {
        sendingEvents = new("sendingEvents");
        waitedEvents = new("waitedEvents");

        if (!sendingEvents.IsEmpty || !waitedEvents.IsEmpty)
            SendEvents();
    }

    [Button]
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
        Send();
    }

    private async void Send()
    {
        bool successful;
        isSending = true;
        StartCooldown();

        using (UnityWebRequest request = UnityWebRequest.Post(serverUrl, sendingEvents.ToJson(), "application/json"))
        {
            request.SendWebRequest();
            while (!request.isDone)
                await Task.Yield();
            successful = request.result == UnityWebRequest.Result.Success && request.responseCode == 200;
        }

        Debug.Log("end waiting");

        if (successful)
        {
            isSending = false;
            sendingEvents.Clear();
            // if sending take more time than cooldown
            if (!isCooldown && !waitedEvents.IsEmpty)
                SendEvents();
        }
        else
            // retry to send
            SendEvents();
    }

    private void StartCooldown()
    {
        if (!isCooldown)
            Cooldown();
    }

    private async void Cooldown()
    {
        isCooldown = true;
        await Task.Delay((int)(cooldownBeforeSend * 1000));
        isCooldown = false;
        if (!waitedEvents.IsEmpty && !isSending)
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
