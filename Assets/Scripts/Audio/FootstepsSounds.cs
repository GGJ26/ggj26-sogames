using UnityEngine;

public class FootstepsSounds : MonoBehaviour
{
    public AudioSource audioSource;
    private Vector3 lastPosition;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void PlayAudio()
    {
        audioSource.Play();
    }
    void StopAudio()
    {
        audioSource.Stop();
    }

    void Update()
    {
        if (transform.position != lastPosition)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
        lastPosition = transform.position;
    }
}
