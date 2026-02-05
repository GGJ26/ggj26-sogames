using System.Collections.Generic;
using UnityEngine;


// Singleton manager
public class AudioManager : MonoBehaviour
{
    [System.Serializable]
    public class SoundEffect
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }
    [SerializeField] private SoundEffect[] soundEffects;
    [SerializeField] private int poolSize = 8;

    private List<AudioSource> audioSources = new List<AudioSource>();
    private Dictionary<string, AudioClip> soundDictionary = new Dictionary<string, AudioClip>();

    private static AudioManager _instance;
    public static AudioManager Instance
    {
        // Create the singleton when a script calls AudioManager.Instance
        get
        {
            if (_instance == null)
            {
                GameObject gameObject = new GameObject("AudioManager");
                _instance = gameObject.AddComponent<AudioManager>();
                DontDestroyOnLoad(gameObject);
            }
            return _instance;
        }
    }

    void Awake()
    {
        // Create the singleton when the manager is added to hierarchy
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            audioSources.Add(source);
        }
        foreach (SoundEffect sound in soundEffects)
        {
            if (sound.clip != null)
            {
                soundDictionary[sound.name] = sound.clip;
            }
        }
    }
    void Start()
    {
        MaskManager.instance.AddMaskListener(PlayMaskSound);
    }
    public void Log(GameObject gameObjectToLog, string scriptName = "null", string content = "")
    {
        string gameObjectName;

        if(gameObjectToLog != null)
        {
            gameObjectName = gameObjectToLog.name;
        } else
        {
            gameObjectName = "null";
        }

        Debug.Log($"<color=orange>AUDIO</color> | CS script = {scriptName}, GameObject = {gameObjectName} : {content}");
    }
    private AudioSource GetAvailableAudioSource()
    {
        foreach (AudioSource source in audioSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        return audioSources.Count > 0 ? audioSources[0] : null;
    }
    public void PlayMaskSound()
    {
        string soundName = MaskManager.instance.selectedMask.id;
        PlaySound(soundName);
    }
    public void PlaySound(string soundName)
    {
        if (!soundDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            Debug.LogWarning($"Sound '{soundName}' not found!");
            return;
        }
        AudioSource source = GetAvailableAudioSource();
        if (source != null)
        {
            float soundVolume = 1f;
            foreach (SoundEffect sound in soundEffects)
            {
                if (sound.name == soundName)
                {
                    soundVolume = sound.volume;
                    break;
                }
            }
            float finalVolume = soundVolume;
            source.clip = clip;
            source.volume = finalVolume;
            source.Play();
        } 
    }
}
