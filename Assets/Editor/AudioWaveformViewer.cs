using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AudioWaveformViewer : EditorWindow
{
    private AudioClip audioClip;

    private float[] waveform;
    private bool waveformLoaded;

    private AudioSource previewSource;
    private GameObject previewObject;

    private double previewStartEditorTime;
    private float previewStartAudioTime;
    private bool isPlaying;

    private float playheadTime;

    private Vector2 markerScroll;

    private float zoom = 1f;

    private const float WaveformHeight = 180f;
    private const float LeftMargin = 55f;
    private const float RightMargin = 20f;

    private readonly List<AudioMarker> markers = new List<AudioMarker>();

    [Serializable]
    private class AudioMarker
    {
        public float time;
        public MarkerType type;
    }

    private enum MarkerType
    {
        TalkStart,
        TalkEnd
    }

    [MenuItem("Tools/Audio Waveform Viewer")]
    public static void ShowWindow()
    {
        GetWindow<AudioWaveformViewer>("Audio Waveform");
    }

    private void OnEnable()
    {
        EditorApplication.update += EditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;

        StopPreview();

        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
        }
    }

    private void EditorUpdate()
    {
        if (isPlaying && previewSource != null)
        {
            float elapsed =
                (float)(EditorApplication.timeSinceStartup - previewStartEditorTime);

            playheadTime = previewStartAudioTime + elapsed;

            if (playheadTime >= audioClip.length)
            {
                playheadTime = audioClip.length;
                StopPreview();
            }

            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawHeader();

        EditorGUILayout.Space(5);

        DrawAudioSelection();

        if (audioClip == null)
        {
            EditorGUILayout.HelpBox(
                "Arraste um AudioClip para o campo acima para começar.",
                MessageType.Info);

            return;
        }

        EditorGUILayout.Space(5);

        DrawTransportControls();

        EditorGUILayout.Space(5);

        DrawTimeInformation();

        EditorGUILayout.Space(5);

        DrawWaveform();

        EditorGUILayout.Space(5);

        DrawMarkers();

        EditorGUILayout.Space(5);

        DrawHelp();
    }

    // ------------------------------------------------------------
    // HEADER
    // ------------------------------------------------------------

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label(
            "Audio Waveform Viewer",
            EditorStyles.boldLabel);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(
            "Clear Markers",
            EditorStyles.toolbarButton,
            GUILayout.Width(100)))
        {
            markers.Clear();
            Repaint();
        }

        EditorGUILayout.EndHorizontal();
    }

    // ------------------------------------------------------------
    // AUDIO
    // ------------------------------------------------------------

    private void DrawAudioSelection()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            "Audio Clip",
            GUILayout.Width(70));

        AudioClip newClip = (AudioClip)EditorGUILayout.ObjectField(
            audioClip,
            typeof(AudioClip),
            false);

        if (newClip != audioClip)
        {
            audioClip = newClip;

            StopPreview();

            playheadTime = 0f;

            markers.Clear();

            LoadWaveform();
        }

        EditorGUILayout.EndHorizontal();

        if (audioClip != null)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(
                $"Length: {FormatTime(audioClip.length)}",
                GUILayout.Width(150));

            EditorGUILayout.LabelField(
                $"Samples: {audioClip.samples:N0}",
                GUILayout.Width(150));

            EditorGUILayout.LabelField(
                $"Frequency: {audioClip.frequency:N0} Hz");

            EditorGUILayout.EndHorizontal();
        }
    }

    private void LoadWaveform()
    {
        waveformLoaded = false;
        waveform = null;

        if (audioClip == null)
            return;

        if (audioClip.loadState != AudioDataLoadState.Loaded)
        {
            audioClip.LoadAudioData();
        }

        int sampleCount = audioClip.samples;

        if (sampleCount <= 0)
            return;

        int targetResolution = 20000;

        int samplesPerPixel =
            Mathf.Max(1, sampleCount / targetResolution);

        int waveformResolution =
            Mathf.CeilToInt(
                (float)sampleCount / samplesPerPixel);

        waveform = new float[waveformResolution];

        int channels = audioClip.channels;

        float[] data = new float[
            samplesPerPixel * channels];

        for (int i = 0; i < waveformResolution; i++)
        {
            int samplePosition =
                i * samplesPerPixel;

            int samplesToRead =
                Mathf.Min(
                    samplesPerPixel,
                    sampleCount - samplePosition);

            if (samplesToRead <= 0)
                break;

            int requiredLength =
                samplesToRead * channels;

            if (data.Length != requiredLength)
            {
                data = new float[requiredLength];
            }

            bool success =
                audioClip.GetData(
                    data,
                    samplePosition);

            if (!success)
            {
                Debug.LogWarning(
                    "Não foi possível ler os dados do AudioClip. " +
                    "Tente usar Load Type = Decompress On Load.");

                waveform = null;
                return;
            }

            float max = 0f;

            for (int j = 0; j < data.Length; j += channels)
            {
                float sample = 0f;

                for (int c = 0; c < channels; c++)
                {
                    int index = j + c;

                    if (index < data.Length)
                        sample += Mathf.Abs(data[index]);
                }

                sample /= channels;

                if (sample > max)
                    max = sample;
            }

            waveform[i] = max;
        }

        waveformLoaded = true;

        Repaint();
    }

    // ------------------------------------------------------------
    // TRANSPORT
    // ------------------------------------------------------------

    private void DrawTransportControls()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = !isPlaying;

        if (GUILayout.Button("▶ Play", GUILayout.Height(30)))
        {
            PlayPreview();
        }

        GUI.enabled = isPlaying;

        if (GUILayout.Button("⏸ Pause", GUILayout.Height(30)))
        {
            PausePreview();
        }

        GUI.enabled = true;

        if (GUILayout.Button("■ Stop", GUILayout.Height(30)))
        {
            StopPreview();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("Zoom", GUILayout.Width(45));

        zoom = EditorGUILayout.Slider(
            zoom,
            1f,
            10f);

        EditorGUILayout.EndHorizontal();
    }

    private void PlayPreview()
    {
        if (audioClip == null)
            return;

        if (previewObject == null)
        {
            previewObject =
                new GameObject(
                    "AudioWaveformPreview");

            previewObject.hideFlags =
                HideFlags.HideAndDontSave;

            previewSource =
                previewObject.AddComponent<AudioSource>();

            previewSource.playOnAwake = false;
        }

        previewSource.clip = audioClip;

        previewSource.time =
            Mathf.Clamp(
                playheadTime,
                0f,
                audioClip.length);

        previewSource.Play();

        previewStartAudioTime =
            previewSource.time;

        previewStartEditorTime =
            EditorApplication.timeSinceStartup;

        isPlaying = true;

        Repaint();
    }

    private void PausePreview()
    {
        if (previewSource != null)
        {
            previewSource.Pause();
        }

        if (isPlaying)
        {
            float elapsed =
                (float)(
                    EditorApplication.timeSinceStartup -
                    previewStartEditorTime);

            playheadTime =
                Mathf.Clamp(
                    previewStartAudioTime + elapsed,
                    0f,
                    audioClip.length);
        }

        isPlaying = false;

        Repaint();
    }

    private void StopPreview()
    {
        if (previewSource != null)
        {
            previewSource.Stop();
        }

        isPlaying = false;

        Repaint();
    }

    // ------------------------------------------------------------
    // TIME
    // ------------------------------------------------------------

    private void DrawTimeInformation()
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label(
            $"Current: {FormatTime(playheadTime)}",
            EditorStyles.boldLabel);

        GUILayout.FlexibleSpace();

        GUILayout.Label(
            $"Duration: {FormatTime(audioClip.length)}",
            EditorStyles.boldLabel);

        EditorGUILayout.EndHorizontal();
    }

    // ------------------------------------------------------------
    // WAVEFORM
    // ------------------------------------------------------------

    private void DrawWaveform()
    {
        Rect rect =
            GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(WaveformHeight));

        EditorGUI.DrawRect(
            rect,
            new Color(0.08f, 0.08f, 0.08f));

        if (!waveformLoaded ||
            waveform == null ||
            waveform.Length == 0)
        {
            GUI.Label(
                rect,
                "Waveform não disponível.",
                EditorStyles.centeredGreyMiniLabel);

            return;
        }

        float visibleWidth =
            rect.width -
            LeftMargin -
            RightMargin;

        float scaledWidth =
            visibleWidth * zoom;

        float centerY =
            rect.y +
            rect.height * 0.5f;

        float halfHeight =
            rect.height * 0.42f;

        Handles.BeginGUI();

        // Center line
        Handles.color =
            new Color(1f, 1f, 1f, 0.15f);

        Handles.DrawLine(
            new Vector3(
                rect.x + LeftMargin,
                centerY),

            new Vector3(
                rect.x + LeftMargin + scaledWidth,
                centerY));

        // Waveform
        Handles.color =
            new Color(
                0.2f,
                0.8f,
                1f,
                1f);

        int waveformLength =
            waveform.Length;

        for (int x = 0; x < scaledWidth; x++)
        {
            float normalized =
                x / scaledWidth;

            int index =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        normalized * waveformLength),
                    0,
                    waveformLength - 1);

            float amplitude =
                waveform[index];

            float y =
                amplitude * halfHeight;

            float px =
                rect.x +
                LeftMargin +
                x;

            Handles.DrawLine(
                new Vector3(px, centerY - y),
                new Vector3(px, centerY + y));
        }

        // Time markers
        Handles.color =
            new Color(
                1f,
                1f,
                1f,
                0.2f);

        float secondsPerMajorLine = GetGridInterval();

        for (
            float t = 0;
            t <= audioClip.length;
            t += secondsPerMajorLine)
        {
            float x =
                TimeToX(
                    t,
                    rect,
                    scaledWidth);

            if (x < rect.x + LeftMargin ||
                x > rect.x + rect.width)
                continue;

            Handles.DrawLine(
                new Vector3(
                    x,
                    rect.y),
                new Vector3(
                    x,
                    rect.y + rect.height));

            GUI.Label(
                new Rect(
                    x - 25,
                    rect.y + 2,
                    50,
                    18),
                FormatTime(t),
                EditorStyles.miniLabel);
        }

        // Markers
        for (int i = 0; i < markers.Count; i++)
        {
            AudioMarker marker =
                markers[i];

            float x =
                TimeToX(
                    marker.time,
                    rect,
                    scaledWidth);

            if (x < rect.x + LeftMargin ||
                x > rect.x + rect.width)
                continue;

            Handles.color =
                marker.type ==
                MarkerType.TalkStart
                    ? new Color(0.2f, 1f, 0.3f)
                    : new Color(1f, 0.3f, 0.3f);

            Handles.DrawLine(
                new Vector3(
                    x,
                    rect.y),
                new Vector3(
                    x,
                    rect.y + rect.height));

            GUI.Label(
                new Rect(
                    x + 3,
                    rect.y + 20,
                    90,
                    20),
                marker.type ==
                MarkerType.TalkStart
                    ? "TALK START"
                    : "TALK END",
                EditorStyles.miniBoldLabel);
        }

        // Playhead
        float playheadX =
            TimeToX(
                playheadTime,
                rect,
                scaledWidth);

        Handles.color = Color.white;

        Handles.DrawLine(
            new Vector3(
                playheadX,
                rect.y),
            new Vector3(
                playheadX,
                rect.y + rect.height));

        Handles.DrawSolidDisc(
            new Vector3(
                playheadX,
                rect.y + 8),
            Vector3.forward,
            4f);

        Handles.EndGUI();

        HandleWaveformMouse(
            rect,
            scaledWidth);
    }

    private float TimeToX(
        float time,
        Rect rect,
        float scaledWidth)
    {
        if (audioClip == null ||
            audioClip.length <= 0)
            return rect.x + LeftMargin;

        float normalized =
            time / audioClip.length;

        return
            rect.x +
            LeftMargin +
            normalized * scaledWidth;
    }

    private float XToTime(
        float x,
        Rect rect,
        float scaledWidth)
    {
        float normalized =
            (x -
             rect.x -
             LeftMargin) /
            scaledWidth;

        normalized =
            Mathf.Clamp01(normalized);

        return normalized *
               audioClip.length;
    }

    private void HandleWaveformMouse(
        Rect rect,
        float scaledWidth)
    {
        Event e = Event.current;

        if (!rect.Contains(e.mousePosition))
            return;

        if (e.type == EventType.MouseDown &&
            e.button == 0)
        {
            playheadTime =
                XToTime(
                    e.mousePosition.x,
                    rect,
                    scaledWidth);

            if (isPlaying)
            {
                PlayPreview();
            }

            Repaint();

            e.Use();
        }

        if (e.type == EventType.MouseDrag &&
            e.button == 0)
        {
            playheadTime =
                XToTime(
                    e.mousePosition.x,
                    rect,
                    scaledWidth);

            if (isPlaying)
            {
                PlayPreview();
            }

            Repaint();

            e.Use();
        }

        // Right click = create marker
        if (e.type == EventType.MouseDown &&
            e.button == 1)
        {
            float time =
                XToTime(
                    e.mousePosition.x,
                    rect,
                    scaledWidth);

            ShowMarkerMenu(time);

            e.Use();
        }
    }

    // ------------------------------------------------------------
    // GRID
    // ------------------------------------------------------------

    private float GetGridInterval()
    {
        if (audioClip == null)
            return 1f;

        float duration =
            audioClip.length;

        if (duration <= 5f)
            return 0.25f;

        if (duration <= 15f)
            return 0.5f;

        if (duration <= 30f)
            return 1f;

        if (duration <= 60f)
            return 2f;

        return 5f;
    }

    // ------------------------------------------------------------
    // MARKERS
    // ------------------------------------------------------------

    private void ShowMarkerMenu(float time)
    {
        GenericMenu menu =
            new GenericMenu();

        menu.AddItem(
            new GUIContent("Talk Start"),
            false,
            () =>
            {
                AddMarker(
                    time,
                    MarkerType.TalkStart);
            });

        menu.AddItem(
            new GUIContent("Talk End"),
            false,
            () =>
            {
                AddMarker(
                    time,
                    MarkerType.TalkEnd);
            });

        menu.AddSeparator("");

        menu.AddItem(
            new GUIContent("Set Playhead Here"),
            false,
            () =>
            {
                playheadTime = time;
                Repaint();
            });

        menu.ShowAsContext();
    }

    private void AddMarker(
        float time,
        MarkerType type)
    {
        markers.Add(
            new AudioMarker
            {
                time = time,
                type = type
            });

        markers.Sort(
            (a, b) =>
                a.time.CompareTo(b.time));

        Repaint();
    }

    private void DrawMarkers()
    {
        EditorGUILayout.LabelField(
            "Markers",
            EditorStyles.boldLabel);

        if (markers.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Clique com o botão direito na waveform para criar um marcador.",
                MessageType.None);

            return;
        }

        markerScroll =
            EditorGUILayout.BeginScrollView(
                markerScroll,
                GUILayout.Height(120));

        for (int i = 0; i < markers.Count; i++)
        {
            AudioMarker marker =
                markers[i];

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(
                marker.type ==
                MarkerType.TalkStart
                    ? "▶ TALK START"
                    : "■ TALK END",
                GUILayout.Width(110));

            float newTime =
                EditorGUILayout.FloatField(
                    marker.time);

            newTime =
                Mathf.Clamp(
                    newTime,
                    0f,
                    audioClip.length);

            marker.time = newTime;

            if (GUILayout.Button(
                "Go",
                GUILayout.Width(40)))
            {
                playheadTime =
                    marker.time;

                Repaint();
            }

            if (GUILayout.Button(
                "X",
                GUILayout.Width(25)))
            {
                markers.RemoveAt(i);
                i--;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------
    // HELP
    // ------------------------------------------------------------

    private void DrawHelp()
    {
        EditorGUILayout.HelpBox(
            "Clique na waveform para posicionar o playhead.\n" +
            "Arraste para fazer scrubbing.\n" +
            "Botão direito cria TALK START / TALK END.\n" +
            "Use Zoom para enxergar detalhes da fala.",
            MessageType.None);
    }

    // ------------------------------------------------------------
    // UTILS
    // ------------------------------------------------------------

    private string FormatTime(float seconds)
    {
        int minutes =
            Mathf.FloorToInt(
                seconds / 60f);

        float remaining =
            seconds -
            minutes * 60f;

        return
            $"{minutes:00}:{remaining:00.000}";
    }
}