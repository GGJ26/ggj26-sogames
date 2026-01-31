using UnityEngine;
using UnityEngine.SceneManagement;

// Singleton manager
public class GameSceneManager : MonoBehaviour
{

    private static GameSceneManager _instance;
    public static GameSceneManager Instance
    {
        // Create the singleton when a script calls GameSceneManager.Instance
        get
        {
            if (_instance == null)
            {
                GameObject gameObject = new GameObject("GameSceneManager");
                _instance = gameObject.AddComponent<GameSceneManager>();
                DontDestroyOnLoad(gameObject);
            }
            return _instance;
        }
    }

    private string _currentScene;
    public string CurrentScene
    {
        get => _currentScene;
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

        _currentScene = SceneManager.GetActiveScene().name;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    // Apply scene logic
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _currentScene = scene.name;
    }

    public void LoadSceneByName(string sceneName)
    {
        if(sceneName == "")
        {
            Debug.Log("Empty scene name");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
