using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace MMOClient.Skills
{
    /// <summary>
    /// Componente para entrada individual na lista de skills
    /// ESTE é o componente que vai no PREFAB!
    /// </summary>
    public class SkillBookEntry : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI skillNameText;
        public TextMeshProUGUI skillLevelText;
        public Image iconImage;
        public Button selectButton;

        [Header("Colors (opcional)")]
        public Color normalColor = Color.white;
        public Color selectedColor = Color.yellow;

        private SkillTemplate skill;
        private Action<SkillTemplate> onSelectCallback;
        private bool isSelected = false;

        /// <summary>
        /// Configura a entrada com dados da skill
        /// </summary>
        public void Setup(SkillTemplate skillTemplate, Action<SkillTemplate> callback)
        {
            skill = skillTemplate;
            onSelectCallback = callback;

            if (skillNameText != null)
            {
                skillNameText.text = skill.name;
            }

            if (skillLevelText != null)
            {
                skillLevelText.text = $"Lv.{skill.requiredLevel}";
            }

            if (iconImage != null)
            {
                var sprite = Resources.Load<Sprite>(skill.iconPath);
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                    iconImage.enabled = true;
                }
                else
                {
                    // Fallback: ícone padrão
                    iconImage.enabled = false;
                    Debug.LogWarning($"Icon not found: {skill.iconPath}");
                }
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnSelectClick);
            }

            UpdateVisuals();
        }

        private void OnSelectClick()
        {
            onSelectCallback?.Invoke(skill);
            isSelected = true;
            UpdateVisuals();
        }

        public void Deselect()
        {
            isSelected = false;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (selectButton != null)
            {
                var colors = selectButton.colors;
                colors.normalColor = isSelected ? selectedColor : normalColor;
                selectButton.colors = colors;
            }
        }
    }
}