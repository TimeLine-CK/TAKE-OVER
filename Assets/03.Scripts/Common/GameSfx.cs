using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameSfx : MonoBehaviour
{
    public enum Cue
    {
        BatteryUp,
        GunShot,
        Lever,
        RobotAttack,
        Run,
        TimeTransition,
        UIButton,
        Valve,
        WaterUp
    }

    [Serializable]
    private struct Sound
    {
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume;
    }

    [SerializeField] private Sound[] sounds;
    private static GameSfx instance;
    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureInstance();
    }

    private void Start()
    {
        SceneManager.sceneLoaded += BindButtons;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            BindButtons(SceneManager.GetSceneAt(i), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= BindButtons;
    }

    private static void BindButtons(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveListener(PlayButton);
                button.onClick.AddListener(PlayButton);
            }
        }
    }

    private static void PlayButton()
    {
        Play(Cue.UIButton);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameSfx prefab = Resources.Load<GameSfx>("GameSfx");
        if (prefab == null)
            return;

        instance = Instantiate(prefab);
        DontDestroyOnLoad(instance.gameObject);
        instance.source = instance.gameObject.AddComponent<AudioSource>();
    }

    private static bool TryGetSound(Cue cue, out Sound sound)
    {
        EnsureInstance();
        sound = default;
        if (instance == null)
            return false;

        sound = instance.sounds[(int)cue];
        return sound.Clip != null;
    }

    public static void Play(Cue cue)
    {
        if (TryGetSound(cue, out Sound sound))
            instance.source.PlayOneShot(sound.Clip, sound.Volume);
    }

    public static void SetLoop(GameObject owner, ref AudioSource loop, Cue cue, bool playing)
    {
        if (!playing)
        {
            if (loop != null && loop.isPlaying)
                loop.Stop();
            return;
        }

        if (loop == null)
        {
            if (!TryGetSound(cue, out Sound sound))
                return;

            loop = owner.AddComponent<AudioSource>();
            loop.playOnAwake = false;
            loop.loop = true;
            loop.clip = sound.Clip;
            loop.volume = sound.Volume;
        }

        if (!loop.isPlaying)
            loop.Play();
    }
}
