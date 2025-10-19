using System;
using System.Collections.Generic;

namespace MMOClient.Skills
{
    /// <summary>
    /// Template de Skill (dados imutáveis do servidor)
    /// </summary>
    [Serializable]
    public class SkillTemplate
    {
        public int id;
        public string name;
        public string description;
        public string skillType; // active, passive, buff
        public string damageType; // physical, magical, true
        public string targetType; // enemy, self, ally, area
        
        // Requisitos
        public int requiredLevel;
        public string requiredClass;
        public int maxLevel;
        
        // Custos
        public int manaCost;
        public int healthCost;
        
        // Timing
        public float cooldown;
        public float castTime;
        public float duration;
        
        // Range
        public float range;
        public float areaRadius;
        
        // Dano/Cura por nível
        public List<SkillLevelData> levels;
        
        // Efeitos
        public List<SkillEffect> effects;
        
        // Visual/Audio
        public string animationTrigger;
        public string effectPrefab;
        public string soundEffect;
        public string iconPath;
    }

    [Serializable]
    public class SkillLevelData
    {
        public int level;
        public int baseDamage;
        public int baseHealing;
        public float damageMultiplier;
        public float critChanceBonus;
        public int statusPointCost;
    }

    [Serializable]
    public class SkillEffect
    {
        public string effectType; // stun, slow, dot, hot, buff_stat
        public string targetStat;
        public int value;
        public float duration;
        public float chance;
    }

    /// <summary>
    /// Skill aprendida pelo personagem
    /// </summary>
    [Serializable]
    public class LearnedSkill
    {
        public int skillId;
        public int currentLevel;
        public int slotNumber; // 1-9
        public long lastUsedTime;
        
        [NonSerialized]
        public SkillTemplate template;
        
        // Runtime
        public float GetCooldownRemaining(float currentTime)
        {
            if (template == null) return 0f;
            
            float timeSinceUse = currentTime - (lastUsedTime / 1000f);
            float remaining = template.cooldown - timeSinceUse;
            
            return Math.Max(0f, remaining);
        }
        
        public bool IsOnCooldown(float currentTime)
        {
            return GetCooldownRemaining(currentTime) > 0f;
        }
    }

    /// <summary>
    /// Request de uso de skill
    /// </summary>
    [Serializable]
    public class UseSkillRequest
    {
        public int skillId;
        public int slotNumber;
        public string targetId;
        public string targetType;
        public Position targetPosition;
    }

    /// <summary>
    /// Resultado do uso de skill
    /// </summary>
    [Serializable]
    public class SkillResult
    {
        public bool success;
        public string failReason;
        
        public string attackerId;
        public string attackerName;
        public string attackerType;
        
        public List<SkillTargetResult> targets;
        
        public int manaCost;
        public int healthCost;
    }

    [Serializable]
    public class SkillTargetResult
    {
        public string targetId;
        public string targetName;
        public string targetType;
        
        public int damage;
        public int healing;
        public bool isCritical;
        public bool targetDied;
        
        public int remainingHealth;
        public int experienceGained;
        public bool leveledUp;
        public int newLevel;
        
        public List<AppliedEffect> appliedEffects;
    }

    [Serializable]
    public class AppliedEffect
    {
        public string effectType;
        public int value;
        public float duration;
    }

    /// <summary>
    /// Buff/Debuff ativo
    /// </summary>
    [Serializable]
    public class ActiveEffect
    {
        public int id;
        public int skillId;
        public string effectType;
        public string targetStat;
        public int value;
        public float startTime;
        public float duration;
        public string sourceId;
        
        public bool IsExpired(float currentTime)
        {
            return currentTime >= startTime + duration;
        }
        
        public float GetRemainingTime(float currentTime)
        {
            return Math.Max(0f, (startTime + duration) - currentTime);
        }
    }

    // Posição (para compatibilidade)
    [Serializable]
    public class Position
    {
        public float x;
        public float y;
        public float z;
    }
}