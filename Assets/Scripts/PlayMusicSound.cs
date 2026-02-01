using UnityEngine;

public class PlayMusicSound : MonoBehaviour
{
    private void Start()
    {
        AudioManager.Instance.PlaySound("Music");
    }
 
}
