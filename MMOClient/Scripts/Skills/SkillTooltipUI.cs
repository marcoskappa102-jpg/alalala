using UnityEngine;
using TMPro;

namespace MMOClient.Skills
{
    /// <summary>
    /// Tooltip que aparece ao passar mouse sobre skill
    /// </summary>
    public class SkillTooltipUI : MonoBehaviour
    {
        public static SkillTooltipUI Instance { get; private set; }

        [Header("UI Elements")]
        public GameObject tooltipPanel;
        public TextMeshProUGUI tooltipText;
        public RectTransform tooltipRect;

        [Header("Settings")]
        public Vector2 offset = new Vector2(10f, 10f);
        public float padding = 20f;

        private Canvas parentCanvas;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            parentCanvas = GetComponentInParent<Canvas>();
            Hide();
        }

        /// <summary>
        /// Mostra tooltip
        /// </summary>
        public void Show(string text, Vector3 worldPosition)
        {
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(true);
            }

            if (tooltipText != null)
            {
                tooltipText.text = text;
            }

            // Força atualização do layout
            Canvas.ForceUpdateCanvases();

            // Posiciona tooltip
            PositionTooltip(worldPosition);
        }

        /// <summary>
        /// Oculta tooltip
        /// </summary>
        public void Hide()
        {
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Posiciona tooltip próximo ao cursor
        /// </summary>
        private void PositionTooltip(Vector3 worldPosition)
        {
            if (tooltipRect == null || parentCanvas == null)
                return;

            // Converte posição world para screen
            Vector2 screenPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                worldPosition,
                parentCanvas.worldCamera,
                out screenPosition
            );

            // Aplica offset
            screenPosition += offset;

            // Garante que não saia da tela
            Vector2 canvasSize = (parentCanvas.transform as RectTransform).sizeDelta;
            Vector2 tooltipSize = tooltipRect.sizeDelta;

            // Ajusta X
            if (screenPosition.x + tooltipSize.x + padding > canvasSize.x / 2)
            {
                screenPosition.x -= tooltipSize.x + offset.x * 2;
            }

            // Ajusta Y
            if (screenPosition.y + tooltipSize.y + padding > canvasSize.y / 2)
            {
                screenPosition.y -= tooltipSize.y + offset.y * 2;
            }

            tooltipRect.anchoredPosition = screenPosition;
        }

        private void Update()
        {
            // Atualiza posição do tooltip para seguir o mouse (opcional)
            if (tooltipPanel != null && tooltipPanel.activeSelf)
            {
                PositionTooltip(Input.mousePosition);
            }
        }
    }
}