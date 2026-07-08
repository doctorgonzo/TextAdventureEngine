namespace TextEngine
{
    using UnityEngine;

    public class SoundManager : MonoBehaviour
    {
        [Header("Audio Sources")]
        [Tooltip("The AudioSource for looping background music.")]
        public AudioSource musicSource;

        [Tooltip("The AudioSource for one-off sound effects like picking up items.")]
        public AudioSource sfxSource;

        // This public method will be called by our Event System.
        public void OnLocationChanged(Location newLocation)
        {
            // If the new location has background music assigned...
            if (newLocation.backgroundMusic != null)
            {
                // ...and it's not the same music that's already playing...
                if (musicSource.clip != newLocation.backgroundMusic)
                {
                    // ...assign the new clip and play it.
                    musicSource.clip = newLocation.backgroundMusic;
                    musicSource.Play();
                }
            }
            else
            {
                // If the new location has no music, stop the current music.
                musicSource.Stop();
                musicSource.clip = null;
            }
        }

        // This public method will also be called by our Event System.
        public void OnItemTaken(Item item)
        {
            // If the item has a pickup sound assigned...
            if (item.pickupSound != null)
            {
                // ...play it as a one-off sound effect.
                sfxSource.PlayOneShot(item.pickupSound);
            }
        }
    }
}
