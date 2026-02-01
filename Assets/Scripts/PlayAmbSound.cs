using UnityEngine;

public class PlayAmbSound : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
   private void Start()
    {
        AudioManager.Instance.PlaySound("Amb");
    }

}
