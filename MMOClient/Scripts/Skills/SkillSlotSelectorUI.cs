using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MMOClient.Skills
{
    /// <summary>
    /// UI para selecionar em qual slot (1-9) aprender uma skill
    /// VERSÃO CORRIGIDA - Com validações e logs de debug
    /// </summary>
    public class SkillSlotSelectorUI : MonoBehaviour
    {
        public static SkillSlotSelectorUI Instance { get; private set; }

        [Header("UI Elements")]
        public GameObject selectorPanel;
        public TextMeshProUGUI titleText;
        public Button[] slotButtons = new Button[9];
        public TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[9];
        public Button cancelButton;

        private SkillTemplate skillToLearn;
        private System.Action<int> onSlotSelectedCallback;

        private void Awake()
        {
            // ✅ Singleton com validação
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("✅ SkillSlotSelectorUI: Instance created");
            }
            else
            {
                Debug.LogWarning("⚠️ SkillSlotSelectorUI: Duplicate instance destroyed");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // ✅ Validação de componentes obrigatórios
            if (selectorPanel == null)
            {
                Debug.LogError("❌ SkillSlotSelectorUI: selectorPanel is NULL! Assign it in Inspector!");
                return;
            }

            if (cancelButton == null)
            {
                Debug.LogError("❌ SkillSlotSelectorUI: cancelButton is NULL! Assign it in Inspector!");
                return;
            }

            // Configura botões de slot
            bool hasErrors = false;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i] == null)
                {
                    Debug.LogError($"❌ SkillSlotSelectorUI: slotButtons[{i}] is NULL!");
                    hasErrors = true;
                    continue;
                }

                int slotNumber = i + 1;
                slotButtons[i].onClick.AddListener(() => OnSlotButtonClick(slotNumber));

                // Configura label se existir
                if (slotLabels[i] != null)
                {
                    slotLabels[i].text = slotNumber.ToString();
                }
            }

            if (hasErrors)
            {
                Debug.LogError("❌ SkillSlotSelectorUI: Missing slot buttons! Check Inspector!");
            }

            // Configura botão cancelar
            cancelButton.onClick.AddListener(Hide);

            // Esconde inicialmente
            Hide();

            Debug.Log("✅ SkillSlotSelectorUI: Initialized successfully");
        }

        /// <summary>
        /// Mostra seletor de slot
        /// </summary>
        public void Show(SkillTemplate skill, System.Action<int> callback)
        {
            if (selectorPanel == null)
            {
                Debug.LogError("❌ SkillSlotSelectorUI.Show: selectorPanel is NULL!");
                return;
            }

            if (skill == null)
            {
                Debug.LogError("❌ SkillSlotSelectorUI.Show: skill is NULL!");
                return;
            }

            if (callback == null)
            {
                Debug.LogError("❌ SkillSlotSelectorUI.Show: callback is NULL!");
                return;
            }

            skillToLearn = skill;
            onSlotSelectedCallback = callback;

            selectorPanel.SetActive(true);

            if (titleText != null)
            {
                titleText.text = $"Escolha o slot para:\n<color=yellow>{skill.name}</color>";
            }

            UpdateSlotButtons();

            Debug.Log($"📚 SkillSlotSelectorUI: Showing selector for skill '{skill.name}'");
        }

        /// <summary>
        /// Oculta seletor
        /// </summary>
        public void Hide()
        {
            if (selectorPanel != null)
            {
                selectorPanel.SetActive(false);
            }

            skillToLearn = null;
            onSlotSelectedCallback = null;

            Debug.Log("📚 SkillSlotSelectorUI: Hidden");
        }

        /// <summary>
        /// Atualiza estado dos botões de slot
        /// </summary>
        private void UpdateSlotButtons()
        {
            if (SkillManager.Instance == null)
            {
                Debug.LogWarning("⚠️ SkillSlotSelectorUI: SkillManager.Instance is NULL!");
                return;
            }

            var skillSlots = SkillManager.Instance.GetAllSkills();

            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i] == null)
                    continue;

                int slotNumber = i + 1;

                // Verifica se slot já tem skill
                var existingSkill = skillSlots?.Find(s => s.slotNumber == slotNumber);

                if (existingSkill != null && existingSkill.template != null)
                {
                    // Slot ocupado
                    if (slotLabels[i] != null)
                    {
                        slotLabels[i].text = $"{slotNumber}\n<size=14><color=yellow>{existingSkill.template.name}</color></size>";
                    }

                    // Permite substituir
                    slotButtons[i].interactable = true;
                }
                else
                {
                    // Slot vazio
                    if (slotLabels[i] != null)
                    {
                        slotLabels[i].text = $"{slotNumber}\n<size=14><color=gray>Vazio</color></size>";
                    }

                    slotButtons[i].interactable = true;
                }
            }
        }

        /// <summary>
        /// Callback de clique no botão de slot
        /// </summary>
        private void OnSlotButtonClick(int slotNumber)
        {
            if (onSlotSelectedCallback == null)
            {
                Debug.LogError("❌ SkillSlotSelectorUI: No callback set!");
                return;
            }

            if (SkillManager.Instance == null)
            {
                Debug.LogError("❌ SkillSlotSelectorUI: SkillManager.Instance is NULL!");
                return;
            }

            // Verifica se slot já tem skill
            var existingSkill = SkillManager.Instance.GetSkillInSlot(slotNumber);

            if (existingSkill != null && existingSkill.template != null)
            {
                // Confirma substituição
                if (ConfirmDialogUI.Instance != null)
                {
                    ConfirmDialogUI.Instance.Show(
                        "⚠️ Substituir Skill?",
                        $"O slot {slotNumber} já tem a skill:\n<color=yellow>{existingSkill.template.name}</color>\n\nSubstituir por:\n<color=lime>{skillToLearn.name}</color>?",
                        () => ConfirmSlotSelection(slotNumber),
                        null,
                        "Substituir",
                        "Cancelar"
                    );
                }
                else
                {
                    // Sem confirmação, substitui direto
                    Debug.LogWarning("⚠️ ConfirmDialogUI not found, confirming directly");
                    ConfirmSlotSelection(slotNumber);
                }
            }
            else
            {
                // Slot vazio, seleciona direto
                ConfirmSlotSelection(slotNumber);
            }
        }

        /// <summary>
        /// Confirma seleção do slot
        /// </summary>
        private void ConfirmSlotSelection(int slotNumber)
        {
            Debug.Log($"✅ SkillSlotSelectorUI: Slot {slotNumber} selected for skill '{skillToLearn?.name}'");

            onSlotSelectedCallback?.Invoke(slotNumber);
            Hide();
        }

        /// <summary>
        /// Validação no OnDestroy
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Debug.Log("⚠️ SkillSlotSelectorUI: Instance destroyed");
            }
        }

        /// <summary>
        /// ✅ MÉTODO DE DEBUG - Chame no Inspector ou console
        /// </summary>
        [ContextMenu("Debug - Validate Setup")]
        public void DebugValidateSetup()
        {
            Debug.Log("=== SkillSlotSelectorUI Debug ===");
            Debug.Log($"Instance exists: {Instance != null}");
            Debug.Log($"selectorPanel: {(selectorPanel != null ? "✅" : "❌")}");
            Debug.Log($"titleText: {(titleText != null ? "✅" : "❌")}");
            Debug.Log($"cancelButton: {(cancelButton != null ? "✅" : "❌")}");

            int validButtons = 0;
            int validLabels = 0;

            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i] != null) validButtons++;
                if (slotLabels[i] != null) validLabels++;
            }

            Debug.Log($"Valid slot buttons: {validButtons}/9");
            Debug.Log($"Valid slot labels: {validLabels}/9");
            Debug.Log("==============================");
        }
    }
}