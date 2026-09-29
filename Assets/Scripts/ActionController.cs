using UnityEngine;

public class ActionController : MonoBehaviour
{
    public TimelineEventController timeline;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
   void Start()
    {
        if (timeline == null)
        {
            timeline = GetComponent<TimelineEventController>();
        }
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            timeline.Trigger("Incentivo_01");
            Debug.Log("Triggering Torcida event");
        }
    }
}
