using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MenuManager : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject panelSettings;
    public GameObject panelInfo;
    public GameObject panelLoad;
    public GameObject mainButtons;

    [Header("Backgrounds")]
    public GameObject normalBackground;
    public GameObject darkBackground;

    [Header("Time Settings")]
    public float TimeDelay = 0.25f;

    private bool _isLoading = false;

    public void Start()
    {
        ResetMenuState();
    }

    public void ResetMenuState()
    {
        _isLoading = false;

        if (panelSettings != null)
            panelSettings.SetActive(false);
        if (panelInfo != null)
            panelInfo.SetActive(false);
        if (panelLoad != null)
            panelLoad.SetActive(false);
        if (mainButtons != null)
            mainButtons.SetActive(true);

        if (normalBackground != null)
            normalBackground.SetActive(true);
        if (darkBackground != null)
            darkBackground.SetActive(false);
    }

    public void Play()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        StartCoroutine(LoadWithDelay(1));
    }

    public void Back()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        StartCoroutine(LoadWithDelay(0));
    }

    public void Settings()
    {
        if (_isLoading)
            return;

        TogglePanel(panelSettings);
    }

    public void Load()
    {
        if (_isLoading)
            return;

        TogglePanel(panelLoad);
    }

    public void Info()
    {
        if (_isLoading)
            return;

        TogglePanel(panelInfo);
    }

    public void Exit()
        => Application.Quit();

    private void TogglePanel(GameObject panel)
    {
        if (panel == null)
            return;

        var willBeActive = !panel.activeSelf;
        panel.SetActive(willBeActive);

        if (mainButtons != null)
            mainButtons.SetActive(!willBeActive);

        if (willBeActive)
        {
            if (normalBackground != null)
                normalBackground.SetActive(false);
            if (darkBackground != null)
                darkBackground.SetActive(true);
        }
        else
        {
            if (normalBackground != null)
                normalBackground.SetActive(true);
            if (darkBackground != null)
                darkBackground.SetActive(false);
        }
    }

    private IEnumerator LoadWithDelay(int sceneIndex)
    {
        yield return new WaitForSecondsRealtime(TimeDelay);
        SceneManager.LoadScene(sceneIndex);
    }
}