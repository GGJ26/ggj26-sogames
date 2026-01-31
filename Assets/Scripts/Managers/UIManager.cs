using UnityEngine;

// Singleton manager
public class UIManager : MonoBehaviour
{

    private static UIManager _instance;
    public static UIManager Instance
    {
        // Create the singleton when a script calls UIManager.Instance
        get
        {
            if (_instance == null)
            {
                GameObject gameObject = new GameObject("UIManager");
                _instance = gameObject.AddComponent<UIManager>();
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
}
