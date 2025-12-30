using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

namespace SmallScaleInc.CharacterCreatorModern
{
    public class TooltipManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Tooltip Panel Setup")]
        public GameObject tooltipPanel;
        public TextMeshProUGUI tooltipHeader;
        public TextMeshProUGUI tooltipContent;

        [Header("Tooltip Text Settings")]
        public string headerText;

        [TextArea(3, 10)]
        public string contentText;

        [Header("Tooltip Timing Settings")]
        public float delayBeforeShow = 0.5f;
        public float fadeDuration = 0.2f;

        private CanvasGroup tooltipCanvasGroup;
        private Coroutine tooltipCoroutine;

        private void Awake()
        {
            if (tooltipPanel != null)
            {
                tooltipCanvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
                if (tooltipCanvasGroup == null)
                {
                    tooltipCanvasGroup = tooltipPanel.AddComponent<CanvasGroup>();
                }
                tooltipPanel.SetActive(false);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipCoroutine != null)
            {
                StopCoroutine(tooltipCoroutine);
            }
            tooltipCoroutine = StartCoroutine(ShowTooltipCoroutine());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipCoroutine != null)
            {
                StopCoroutine(tooltipCoroutine);
            }
            tooltipCoroutine = StartCoroutine(HideTooltipCoroutine());
        }

        private IEnumerator ShowTooltipCoroutine()
        {
            yield return new WaitForSeconds(delayBeforeShow);

            if (tooltipHeader != null)
                tooltipHeader.text = headerText;

            if (tooltipContent != null)
                tooltipContent.text = contentText;

            if (tooltipPanel != null)
                tooltipPanel.SetActive(true);

            if (tooltipCanvasGroup != null)
            {
                float timer = 0f;
                while (timer < fadeDuration)
                {
                    timer += Time.deltaTime;
                    tooltipCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                    yield return null;
                }
                tooltipCanvasGroup.alpha = 1f;
            }
        }

        private IEnumerator HideTooltipCoroutine()
        {
            if (tooltipCanvasGroup != null)
            {
                float timer = 0f;
                while (timer < fadeDuration)
                {
                    timer += Time.deltaTime;
                    tooltipCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                    yield return null;
                }
                tooltipCanvasGroup.alpha = 0f;
            }

            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }
    }
}
