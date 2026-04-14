using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private AudioSource _uiAudioSource; // Один общий для всех кнопок
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip pressSound;
    [SerializeField] private AudioClip clickSound;

    private void OnEnable() => GetComponent<Button>().onClick.AddListener(PlayClick);

    public void OnPointerEnter(PointerEventData eventData) => Play(hoverSound);
    public void OnPointerExit(PointerEventData eventData) { } // обычно hover-звук не выключают

    // Для срабатывания при физическом нажатии (до отпускания)
    private void OnMouseDown() => Play(pressSound);

    private void PlayClick() => Play(clickSound);

    private void Play(AudioClip clip)
    {
        if (clip != null && _uiAudioSource != null)
            _uiAudioSource.PlayOneShot(clip);
    }
}