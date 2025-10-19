using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace MMOClient.Skills
{
    /// <summary>
    /// UI da barra de skills (slots 1-9)
    /// </summary>
    public class SkillbarUI : MonoBehaviour
    {
        public static SkillbarUI Instance { get; private set; }

        [Header("Skill Slots")]
        public SkillSlotUI[] skillSlots = new SkillSlotUI[9];

        [Header("Cast Bar")]
        public GameObject castBarPanel;
        public Image castBarFill;
        public TextMeshProUGUI castBarText;

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
        }

        private void Start()
        {
            // Oculta cast bar
            if (castBarPanel != null)
            {
                castBarPanel.SetActive(false);
            }

            // Inicializa slots
            for (int i = 0; i < skillSlots.Length; i++)
            {
                if (skillSlots[i] != null)
                {
                    skillSlots[i].Initialize(i + 1);
                }
            }
        }

        private void Update()
        {
            UpdateCooldowns();
        }

        /// <summary>
        /// Atualiza skillbar com skills do personagem
        /// </summary>
        public void RefreshSkillbar(Dictionary<int, LearnedSkill> skills)
        {
            for (int i = 0; i < skillSlots.Length; i++)
            {
                int slotNumber = i + 1;
                
                if (skillSlots[i] != null)
                {
                    if (skills.TryGetValue(slotNumber, out var skill))
                    {
                        skillSlots[i].SetSkill(skill);
                    }
                    else
                    {
                        skillSlots[i].Clear();
                    }
                }
            }

            Debug.Log($"🎮 Skillbar refreshed with {skills.Count} skills");
        }

        /// <summary>
        /// Atualiza cooldowns visuais
        /// </summary>
        private void UpdateCooldowns()
        {
            float currentTime = Time.time;

            foreach (var slot in skillSlots)
            {
                if (slot != null && slot.skill != null)
                {
                    float remaining = slot.skill.GetCooldownRemaining(currentTime);
                    
                    if (remaining > 0f)
                    {
                        float total = slot.skill.template?.cooldown ?? 1f;
                        slot.UpdateCooldown(remaining, total);
                    }
                    else
                    {
                        slot.ClearCooldown();
                    }
                }
            }
        }

        /// <summary>
        /// Inicia cooldown de um slot
        /// </summary>
        public void UpdateCooldown(int slotNumber, float cooldownTime)
        {
            if (slotNumber >= 1 && slotNumber <= 9)
            {
                var slot = skillSlots[slotNumber - 1];
                
                if (slot != null && slot.skill != null)
                {
                    slot.StartCooldown(cooldownTime);
                }
            }
        }

        // ==================== CAST BAR ====================

        public void ShowCastBar(string skillName, float duration)
        {
            if (castBarPanel != null)
            {
                castBarPanel.SetActive(true);
            }

            if (castBarText != null)
            {
                castBarText.text = skillName;
            }

            if (castBarFill != null)
            {
                castBarFill.fillAmount = 0f;
            }
        }

        public void UpdateCastBar(float progress)
        {
            if (castBarFill != null)
            {
                castBarFill.fillAmount = Mathf.Clamp01(progress);
            }
        }

        public void HideCastBar()
        {
            if (castBarPanel != null)
            {
                castBarPanel.SetActive(false);
            }
        }
    }
}