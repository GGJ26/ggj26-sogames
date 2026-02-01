using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif
// Singleton manager
public class GameManager : MonoBehaviour
{

    private static GameManager _instance;

    public GameObject gameOverScreen;
    public static GameManager Instance
    {
        // Create the singleton when a script calls GameManager.Instance
        get
        {
            if (_instance == null)
            {
                GameObject gameObject = new GameObject("GameManager");
                _instance = gameObject.AddComponent<GameManager>();
                DontDestroyOnLoad(gameObject);
            }
            return _instance;
        }
    }

    public void GameOver()
    {
        gameOverScreen.SetActive(true);
        Time.timeScale = 0;
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

        public void QuitApplication()
    {
        #if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
        #else
                Application.Quit();
        #endif
    }
}
