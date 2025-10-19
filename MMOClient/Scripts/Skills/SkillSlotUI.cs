using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace MMOClient.Skills
{
    /// <summary>
    /// Slot individual de skill na skillbar
    /// </summary>
    public class SkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI Elements")]
        public Image iconImage;
        public Image cooldownOverlay;
        public TextMeshProUGUI cooldownText;
        public TextMeshProUGUI hotkeyText;
        public GameObject emptyIndicator;

        [Header("Colors")]
        public Color normalColor = Color.white;
        public Color onCooldownColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);

        [HideInInspector]
        public LearnedSkill skill;
        
        private int slotNumber;
        private bool isOnCooldown = false;
        private float cooldownRemaining = 0f;
        private float cooldownTotal = 0f;

        public void Initialize(int slot)
        {
            slotNumber = slot;

            if (hotkeyText != null)
            {
                hotkeyText.text = slot.ToString();
            }

            Clear();
        }

        public void SetSkill(LearnedSkill learnedSkill)
        {
            skill = learnedSkill;

            if (skill == null || skill.template == null)
            {
                Clear();
                return;
            }

            // Ativa ícone
            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = LoadIcon(skill.template.iconPath);
                iconImage.color = normalColor;
            }

            // Oculta indicador vazio
            if (emptyIndicator != null)
            {
                emptyIndicator.SetActive(false);
            }

            // Limpa cooldown visual
            ClearCooldown();
        }

        public void Clear()
        {
            skill = null;

            if (iconImage != null)
            {
                iconImage.enabled = false;
            }

            if (emptyIndicator != null)
            {
                emptyIndicator.SetActive(true);
            }

            ClearCooldown();
        }

        /// <summary>
        /// Inicia cooldown visual
        /// </summary>
        public void StartCooldown(float duration)
        {
            isOnCooldown = true;
            cooldownRemaining = duration;
            cooldownTotal = duration;

            if (cooldownOverlay != null)
            {
                cooldownOverlay.enabled = true;
                cooldownOverlay.fillAmount = 1f;
            }

            if (iconImage != null)
            {
                iconImage.color = onCooldownColor;
            }
        }

        /// <summary>
        /// Atualiza cooldown visual
        /// </summary>
        public void UpdateCooldown(float remaining, float total)
        {
            if (!isOnCooldown && remaining > 0f)
            {
                StartCooldown(total);
            }

            cooldownRemaining = remaining;
            cooldownTotal = total;

            if (cooldownOverlay != null)
            {
                float percent = remaining / total;
                cooldownOverlay.fillAmount = percent;
            }

            if (cooldownText != null)
            {
                if (remaining > 1f)
                {
                    cooldownText.text = Mathf.Ceil(remaining).ToString();
                    cooldownText.enabled = true;
                }
                else if (remaining > 0f)
                {
                    cooldownText.text = remaining.ToString("F1");
                    cooldownText.enabled = true;
                }
                else
                {
                    cooldownText.enabled = false;
                }
            }

            if (remaining <= 0f)
            {
                ClearCooldown();
            }
        }

        /// <summary>
        /// Limpa cooldown visual
        /// </summary>
        public void ClearCooldown()
        {
            isOnCooldown = false;
            cooldownRemaining = 0f;

            if (cooldownOverlay != null)
            {
                cooldownOverlay.enabled = false;
                cooldownOverlay.fillAmount = 0f;
            }

            if (cooldownText != null)
            {
                cooldownText.enabled = false;
            }

            if (iconImage != null)
            {
                iconImage.color = normalColor;
            }
        }

        /// <summary>
        /// Carrega ícone da skill
        /// </summary>
        private Sprite LoadIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
                return null;

            // Tenta carregar de Resources
            Sprite sprite = Resources.Load<Sprite>(iconPath);
            
            if (sprite == null)
            {
                // Fallback: ícone padrão
                sprite = Resources.Load<Sprite>("Icons/Skills/default_skill");
            }

            return sprite;
        }

        // ==================== EVENTOS DE MOUSE ====================

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (skill != null && skill.template != null)
            {
                ShowTooltip();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Clique esquerdo: usa skill
                if (SkillManager.Instance != null)
                {
                    SkillManager.Instance.TryUseSkill(slotNumber);
                }
            }
        }

        /// <summary>
        /// Mostra tooltip da skill
        /// </summary>
        private void ShowTooltip()
        {
            if (skill?.template == null)
                return;

            var template = skill.template;
            var levelData = GetCurrentLevelData();

            string tooltip = $"<b><color=yellow>{template.name}</color></b>\n";
            tooltip += $"<color=white>{template.description}</color>\n\n";

            // Tipo
            tooltip += $"<color=cyan>Tipo:</color> {TranslateSkillType(template.skillType)}\n";
            tooltip += $"<color=magenta>Alvo:</color> {TranslateTargetType(template.targetType)}\n\n";

            // Custos
            if (template.manaCost > 0)
                tooltip += $"<color=cyan>Custo:</color> {template.manaCost} MP\n";
            if (template.healthCost > 0)
                tooltip += $"<color=red>Custo:</color> {template.healthCost} HP\n";

            // Cooldown
            if (template.cooldown > 0)
                tooltip += $"<color=yellow>Cooldown:</color> {template.cooldown}s\n";

            // Cast time
            if (template.castTime > 0)
                tooltip += $"<color=orange>Conjuração:</color> {template.castTime}s\n";

            // Range
            if (template.range > 0)
                tooltip += $"<color=white>Alcance:</color> {template.range}m\n";

            // Dano/Cura
            if (levelData != null)
            {
                tooltip += "\n";
                
                if (levelData.baseDamage > 0)
                {
                    int totalDamage = GetEstimatedDamage(levelData);
                    tooltip += $"<color=red>Dano:</color> ~{totalDamage}\n";
                }
                
                if (levelData.baseHealing > 0)
                {
                    int totalHealing = GetEstimatedHealing(levelData);
                    tooltip += $"<color=lime>Cura:</color> ~{totalHealing}\n";
                }

                if (levelData.critChanceBonus > 0)
                {
                    tooltip += $"<color=orange>+{(levelData.critChanceBonus * 100):F0}% Chance de Crítico</color>\n";
                }
            }

            // Nível atual
            tooltip += $"\n<color=gray>Nível {skill.currentLevel}/{template.maxLevel}</color>";

            // Mostra tooltip
            if (SkillTooltipUI.Instance != null)
            {
                SkillTooltipUI.Instance.Show(tooltip, transform.position);
            }
        }

        private void HideTooltip()
        {
            if (SkillTooltipUI.Instance != null)
            {
                SkillTooltipUI.Instance.Hide();
            }
        }

        private SkillLevelData GetCurrentLevelData()
        {
            if (skill?.template?.levels == null)
                return null;

            return skill.template.levels.Find(l => l.level == skill.currentLevel);
        }

        private int GetEstimatedDamage(SkillLevelData levelData)
        {
            var charData = WorldManager.Instance?.GetLocalCharacterData();
            if (charData == null)
                return levelData.baseDamage;

            int attackPower = skill.template.damageType == "magical" 
                ? charData.magicPower 
                : charData.attackPower;

            return levelData.baseDamage + (int)(attackPower * levelData.damageMultiplier);
        }

        private int GetEstimatedHealing(SkillLevelData levelData)
        {
            var charData = WorldManager.Instance?.GetLocalCharacterData();
            if (charData == null)
                return levelData.baseHealing;

            return levelData.baseHealing + (int)(charData.magicPower * levelData.damageMultiplier);
        }

        private string TranslateSkillType(string type)
        {
            return type switch
            {
                "active" => "Ativa",
                "passive" => "Passiva",
                "buff" => "Buff",
                _ => type
            };
        }

        private string TranslateTargetType(string type)
        {
            return type switch
            {
                "enemy" => "Inimigo",
                "self" => "Próprio",
                "ally" => "Aliado",
                "area" => "Área",
                _ => type
            };
        }
    }
}