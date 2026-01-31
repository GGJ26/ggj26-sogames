using UnityEngine;

// Singleton manager
public class AudioManager : MonoBehaviour
{

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
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
}
