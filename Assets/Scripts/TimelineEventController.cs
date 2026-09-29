using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelineEventController : MonoBehaviour
{
    private enum State
    {
        Intro,
        Idle,
        Event
    }

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;

    [Header("Main Clips")]
    [SerializeField] private string introClipName = "Intro";
    [SerializeField] private string idleClipName = "Idle";

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private TimelineAsset timelineAsset;

    private TimelineClip introClip;
    private TimelineClip idleClip;
    private TimelineClip currentEventClip;

    private State currentState;

    private bool initialized;

    public bool IntroFinished => currentState != State.Intro;
    public bool EventPlaying => currentState == State.Event;


    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void Awake()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        if (director == null)
        {
            Debug.LogError(
                "[TimelineEventController] PlayableDirector não encontrado."
            );

            return;
        }

        timelineAsset = director.playableAsset as TimelineAsset;

        if (timelineAsset == null)
        {
            Debug.LogError(
                "[TimelineEventController] O PlayableDirector não possui uma TimelineAsset."
            );
        }
    }


    private void Start()
    {
        if (director == null || timelineAsset == null)
            return;

        introClip = FindClip(introClipName);
        idleClip = FindClip(idleClipName);

        if (introClip == null)
        {
            Debug.LogError(
                $"[TimelineEventController] Não encontrei o clip '{introClipName}'."
            );

            return;
        }

        if (idleClip == null)
        {
            Debug.LogError(
                $"[TimelineEventController] Não encontrei o clip '{idleClipName}'."
            );

            return;
        }

        initialized = true;

        StartIntro();
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (!initialized)
            return;

        switch (currentState)
        {
            case State.Intro:
                UpdateIntro();
                break;

            case State.Idle:
                UpdateIdle();
                break;

            case State.Event:
                UpdateEvent();
                break;
        }
    }


    // ============================================================
    // INTRO
    // ============================================================

    private void StartIntro()
    {
        currentState = State.Intro;

        director.time = introClip.start;
        director.Evaluate();
        director.Play();

        DebugLog(
            $"Intro começou: {introClip.start:F2}s → {introClip.end:F2}s"
        );
    }


    private void UpdateIntro()
    {
        // NÃO permitimos nenhum evento aqui.

        if (director.time >= introClip.end)
        {
            StartIdle();
        }
    }


    // ============================================================
    // IDLE
    // ============================================================

    private void StartIdle()
    {
        currentState = State.Idle;

        currentEventClip = null;

        // Vai para o início do Idle
        director.time = idleClip.start;
        director.Evaluate();

        // IMPORTANTE: continua tocando
        director.Play();

        DebugLog(
            $"Idle começou: {idleClip.start:F2}s → {idleClip.end:F2}s"
        );
    }


    private void UpdateIdle()
    {
        /*
         * Quando chegar ao final do Idle,
         * volta para o começo.
         */

        if (director.time >= idleClip.end)
        {
            director.time = idleClip.start;
            director.Evaluate();
            director.Play();

            DebugLog("Idle loop.");
        }
    }


    // ============================================================
    // TRIGGER EVENT
    // ============================================================

    public void Trigger(string eventName)
    {
        if (!initialized)
            return;

        /*
         * A INTRO É INTOCÁVEL.
         */
        if (currentState == State.Intro)
        {
            DebugLog(
                $"'{eventName}' ignorado porque a Intro ainda está tocando."
            );

            return;
        }

        TimelineClip targetClip = FindClip(eventName);

        if (targetClip == null)
        {
            Debug.LogWarning(
                $"[TimelineEventController] Clip '{eventName}' não encontrado."
            );

            return;
        }

        PlayEvent(targetClip);
    }


    // ============================================================
    // EVENT
    // ============================================================

    private void PlayEvent(TimelineClip targetClip)
    {
        currentState = State.Event;

        currentEventClip = targetClip;

        /*
         * Vai DIRETO para o evento.
         *
         * Não passa por Intro, Idle ou qualquer outro clip.
         */
        director.time = targetClip.start;
        director.Evaluate();
        director.Play();

        DebugLog(
            $"Evento começou: {targetClip.displayName} | " +
            $"{targetClip.start:F2}s → {targetClip.end:F2}s"
        );
    }


    private void UpdateEvent()
    {
        if (currentEventClip == null)
        {
            StartIdle();
            return;
        }

        // O evento terminou
        if (director.time >= currentEventClip.end)
        {
            DebugLog(
                $"Evento terminou: {currentEventClip.displayName}"
            );

            // Limpa o evento atual
            currentEventClip = null;

            // Volta imediatamente para o Idle
            StartIdle();
        }
    }


    // ============================================================
    // FIND CLIP
    // ============================================================

    private TimelineClip FindClip(string clipName)
    {
        if (timelineAsset == null)
            return null;

        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            foreach (TimelineClip clip in track.GetClips())
            {
                /*
                 * Primeiro procura pelo nome do clip
                 * dentro da Timeline.
                 */
                if (string.Equals(
                    clip.displayName,
                    clipName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }

                /*
                 * Depois procura pelo nome do Animation Clip.
                 */
                if (clip.asset is AnimationPlayableAsset animationAsset)
                {
                    if (animationAsset.clip != null &&
                        string.Equals(
                            animationAsset.clip.name,
                            clipName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return clip;
                    }
                }
            }
        }

        return null;
    }


    // ============================================================
    // PUBLIC CONTROLS
    // ============================================================

    public void Pause()
    {
        if (director != null)
            director.Pause();
    }


    public void Resume()
    {
        if (director != null)
            director.Play();
    }


    public void Stop()
    {
        if (director != null)
            director.Stop();

        initialized = false;
    }


    // ============================================================
    // DEBUG
    // ============================================================

    private void DebugLog(string message)
    {
        if (showDebug)
        {
            Debug.Log(
                $"[TimelineEventController] {message}"
            );
        }
    }
}