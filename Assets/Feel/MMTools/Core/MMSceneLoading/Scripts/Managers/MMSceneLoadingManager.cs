using UnityEngine;
#if MM_UI
using UnityEngine.UI;
using TMPro;
#endif
using System.Collections;
using System.Threading;
using UnityEngine.SceneManagement;

namespace MoreMountains.Tools
{
    /// <summary>
    /// A class to load scenes using a loading screen instead of just the default API
    /// This class used to be known as LoadingSceneManager, and has now been renamed to MMSceneLoadingManager for consistency
    /// </summary>
    public class MMSceneLoadingManager : MonoBehaviour
    {
        public enum LoadingStatus
        {
            LoadStarted, BeforeEntryFade, EntryFade, AfterEntryFade, UnloadOriginScene, LoadDestinationScene,
            LoadProgressComplete, InterpolatedLoadProgressComplete, BeforeSceneActivation, ExitFade, DestinationSceneActivation,
            UnloadSceneLoader, LoadTransitionComplete, AfterSceneActivation
        }

        public struct LoadingSceneEvent
        {
            public LoadingStatus Status;
            public string SceneName;

            public LoadingSceneEvent(string sceneName, LoadingStatus status)
            {
                Status = status;
                SceneName = sceneName;
            }

            static LoadingSceneEvent e;

            public static void Trigger(string sceneName, LoadingStatus status)
            {
                e.Status = status;
                e.SceneName = sceneName;
                MMEventManager.TriggerEvent(e);
            }
        }

        [Header("Binding")]
        /// The name of the scene to load while the actual target scene is loading (usually a loading screen)
        public static string LoadingScreenSceneName = "LoadingScreen";

        [Header("GameObjects")]
#if MM_UI
        /// the text object where you want the loading message to be displayed
        public TextMeshProUGUI LoadingText;
        // CanvasGroup to fade the loading text (auto-added at runtime if left null)
        [SerializeField] private CanvasGroup LoadingTextGroup;
#endif
        /// the canvas group containing the progress bar
        public CanvasGroup LoadingProgressBar;
        /// the canvas group containing the animation
        public CanvasGroup LoadingAnimation;

        [Header("Time")]
        /// the duration (in seconds) of the initial fade in
        public float StartFadeDuration = 0.2f;
        /// the speed of the progress bar
        public float ProgressBarSpeed = 2f;
        /// the duration (in seconds) of the load complete fade out
        public float ExitFadeDuration = 0.2f;
        /// the delay (in seconds) before leaving the scene when complete
        public float LoadCompleteDelay = 0.5f;

        protected AsyncOperation _asyncOperation;
        protected static string _sceneToLoad = "";
        protected float _fadeDuration = 0.5f;
        protected float _fillTarget = 0f;
        protected string _loadingTextValue;

#if MM_UI
        protected Image _progressBarImage;
        [SerializeField] private Slider _progressSlider; // assign your "Slider" here (optional)
#endif

        protected static MMTweenType _tween;

        /// <summary>
        /// Call this static method to load a scene from anywhere
        /// </summary>
        public static void LoadScene(string sceneToLoad)
        {
            _sceneToLoad = sceneToLoad;
            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.High;

            if (!string.IsNullOrEmpty(LoadingScreenSceneName))
            {
                LoadingSceneEvent.Trigger(sceneToLoad, LoadingStatus.LoadStarted);
                SceneManager.LoadScene(LoadingScreenSceneName);
            }
        }

        /// <summary>
        /// Call this static method to load a scene from anywhere
        /// </summary>
        public static void LoadScene(string sceneToLoad, string loadingSceneName)
        {
            _sceneToLoad = sceneToLoad;
            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.High;
            SceneManager.LoadScene(loadingSceneName);
        }

        /// <summary>
        /// On Start(), we start loading the new level asynchronously
        /// </summary>
        protected virtual void Start()
        {
            _tween = new MMTweenType(MMTween.MMTweenCurve.EaseOutCubic);

#if MM_UI
            // Auto-find Slider if not assigned
            if (_progressSlider == null && LoadingProgressBar != null)
            {
                _progressSlider = LoadingProgressBar.GetComponentInChildren<Slider>(true);
            }

            // Auto-find a fill Image as fallback (if you're not using a Slider)
            if (_progressBarImage == null && LoadingProgressBar != null)
            {
                _progressBarImage = LoadingProgressBar.GetComponentInChildren<Image>(true);
            }

            // Initialize display values
            if (_progressSlider != null)
            {
                _progressSlider.minValue = 0f;
                _progressSlider.maxValue = 1f;
                _progressSlider.value = 0f;
            }
            else if (_progressBarImage != null)
            {
                _progressBarImage.fillAmount = 0f;
            }

            _loadingTextValue = (LoadingText != null) ? LoadingText.text : "";

            if (LoadingText != null && LoadingTextGroup == null)
            {
                LoadingTextGroup = LoadingText.GetComponent<CanvasGroup>();
                if (LoadingTextGroup == null)
                {
                    LoadingTextGroup = LoadingText.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (LoadingTextGroup != null)
            {
                LoadingTextGroup.alpha = 1f;
            }
#endif

            if (!string.IsNullOrEmpty(_sceneToLoad))
            {
                StartCoroutine(LoadAsynchronously());
            }
        }

        /// <summary>
        /// Every frame, we fill the bar smoothly according to loading progress
        /// </summary>
        protected virtual void Update()
        {
            Time.timeScale = 1f;

#if MM_UI
            float current =
                (_progressSlider != null) ? _progressSlider.value :
                (_progressBarImage != null) ? _progressBarImage.fillAmount :
                0f;

            float next = MMMaths.Approach(current, _fillTarget, Time.deltaTime * ProgressBarSpeed);

            if (_progressSlider != null)
            {
                _progressSlider.value = next;
            }
            else if (_progressBarImage != null)
            {
                _progressBarImage.fillAmount = next;
            }
#endif
        }

        /// <summary>
        /// Loads the scene to load asynchronously.
        /// </summary>
        protected virtual IEnumerator LoadAsynchronously()
        {
            // setup visuals
            LoadingSetup();

            // fade from black
            MMFadeOutEvent.Trigger(StartFadeDuration, _tween);
            yield return MMCoroutine.WaitFor(StartFadeDuration);

            // start loading
            _asyncOperation = SceneManager.LoadSceneAsync(_sceneToLoad, LoadSceneMode.Single);
            _asyncOperation.allowSceneActivation = false;

            // progress (0..0.9)
            while (_asyncOperation.progress < 0.9f)
            {
                _fillTarget = _asyncOperation.progress;
                yield return null;
            }

            // force full
            _fillTarget = 1f;

            // wait until bar visually reaches target
#if MM_UI
            while (true)
            {
                bool reached;

                if (_progressSlider != null)
                {
                    reached = Mathf.Approximately(_progressSlider.value, _fillTarget);
                }
                else if (_progressBarImage != null)
                {
                    reached = Mathf.Approximately(_progressBarImage.fillAmount, _fillTarget);
                }
                else
                {
                    reached = true; // nothing to animate
                }

                if (reached) { break; }
                yield return null;
            }
#endif

            // complete visuals
            LoadingComplete();
            yield return MMCoroutine.WaitFor(LoadCompleteDelay);

            // fade to black
            MMFadeInEvent.Trigger(ExitFadeDuration, _tween);
            yield return MMCoroutine.WaitFor(ExitFadeDuration);

            // activate scene
            _asyncOperation.allowSceneActivation = true;
            LoadingSceneEvent.Trigger(_sceneToLoad, LoadingStatus.LoadTransitionComplete);
        }

        /// <summary>
        /// Sets up all visual elements, fades from black at the start
        /// </summary>
        protected virtual void LoadingSetup()
        {
#if MM_UI
            if (_progressSlider != null) { _progressSlider.value = 0f; }
            if (_progressBarImage != null) { _progressBarImage.fillAmount = 0f; }

            if (LoadingText != null) { LoadingText.text = _loadingTextValue; }
            if (LoadingTextGroup != null) { LoadingTextGroup.alpha = 1f; }
#endif
        }

        /// <summary>
        /// Triggered when the actual loading is done, replaces the progress bar with the complete animation
        /// </summary>
        protected virtual void LoadingComplete()
        {
            LoadingSceneEvent.Trigger(_sceneToLoad, LoadingStatus.InterpolatedLoadProgressComplete);

#if MM_UI
            if (LoadingProgressBar != null)
            {
                StartCoroutine(MMFade.FadeCanvasGroup(LoadingProgressBar, 0.1f, 0f));
            }

            if (LoadingAnimation != null)
            {
                StartCoroutine(MMFade.FadeCanvasGroup(LoadingAnimation, 0.1f, 0f));
            }

            if (LoadingTextGroup != null)
            {
                StartCoroutine(MMFade.FadeCanvasGroup(LoadingTextGroup, 0.1f, 0f));
            }
#endif
        }
    }
}
