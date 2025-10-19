namespace MMOServer.Models
{
    /// <summary>
    /// Template de Skill (dados imutáveis do JSON)
    /// </summary>
    [Serializable]
    public class SkillTemplate
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string description { get; set; } = "";
        public string skillType { get; set; } = ""; // active, passive, buff
        public string damageType { get; set; } = ""; // physical, magical, true
        public string targetType { get; set; } = ""; // enemy, self, ally, area
        
        // Requisitos
        public int requiredLevel { get; set; } = 1;
        public string requiredClass { get; set; } = ""; // "", "Guerreiro", etc.
        public int maxLevel { get; set; } = 10; // Nível máximo da skill
        
        // Custos
        public int manaCost { get; set; } = 0;
        public int healthCost { get; set; } = 0; // Para skills que consomem HP
        
        // Timing
        public float cooldown { get; set; } = 0f; // Segundos
        public float castTime { get; set; } = 0f; // Tempo de conjuração
        public float duration { get; set; } = 0f; // Duração do efeito (buffs/debuffs)
        
        // Range e Área
        public float range { get; set; } = 3.5f; // Alcance
        public float areaRadius { get; set; } = 0f; // Raio de AOE (0 = single target)
        
        // Dano/Cura base por nível
        public List<SkillLevelData> levels { get; set; } = new List<SkillLevelData>();
        
        // Efeitos especiais
        public List<SkillEffect> effects { get; set; } = new List<SkillEffect>();
        
        // Visual/Audio
        public string animationTrigger { get; set; } = ""; // Trigger do Animator
        public string effectPrefab { get; set; } = ""; // Prefab do efeito visual
        public string soundEffect { get; set; } = ""; // Nome do som
        public string iconPath { get; set; } = ""; // Ícone da skill
    }

    /// <summary>
    /// Dados por nível da skill
    /// </summary>
    [Serializable]
    public class SkillLevelData
    {
        public int level { get; set; }
        public int baseDamage { get; set; } = 0;
        public int baseHealing { get; set; } = 0;
        public float damageMultiplier { get; set; } = 1.0f; // % do ATK/MATK
        public float critChanceBonus { get; set; } = 0f; // Bônus de crítico
        public int statusPointCost { get; set; } = 1; // Custo para subir de nível
    }

    /// <summary>
    /// Efeitos adicionais da skill
    /// </summary>
    [Serializable]
    public class SkillEffect
    {
        public string effectType { get; set; } = ""; // stun, slow, dot, hot, buff_stat
        public string targetStat { get; set; } = ""; // strength, speed, defense, etc.
        public int value { get; set; } = 0;
        public float duration { get; set; } = 0f;
        public float chance { get; set; } = 1.0f; // 0.0 a 1.0
    }

    /// <summary>
    /// Skill aprendida por um personagem
    /// </summary>
    [Serializable]
    public class LearnedSkill
    {
        public int skillId { get; set; }
        public int currentLevel { get; set; } = 1;
        public int slotNumber { get; set; } = 0; // 1-9 para teclas
        public long lastUsedTime { get; set; } = 0; // Unix timestamp
        
        [NonSerialized]
        public SkillTemplate? template;
    }

    /// <summary>
    /// Resultado do uso de uma skill
    /// </summary>
    [Serializable]
    public class SkillResult
    {
        public bool success { get; set; }
        public string failReason { get; set; } = ""; // COOLDOWN, NO_MANA, OUT_OF_RANGE, etc.
        
        public string attackerId { get; set; } = "";
        public string attackerName { get; set; } = "";
        public string attackerType { get; set; } = "player"; // player, monster
        
        public List<SkillTargetResult> targets { get; set; } = new List<SkillTargetResult>();
        
        public int manaCost { get; set; } = 0;
        public int healthCost { get; set; } = 0;
    }

    /// <summary>
    /// Resultado por alvo atingido
    /// </summary>
    [Serializable]
    public class SkillTargetResult
    {
        public string targetId { get; set; } = "";
        public string targetName { get; set; } = "";
        public string targetType { get; set; } = "monster"; // player, monster
        
        public int damage { get; set; } = 0;
        public int healing { get; set; } = 0;
        public bool isCritical { get; set; } = false;
        public bool targetDied { get; set; } = false;
        
        public int remainingHealth { get; set; } = 0;
        public int experienceGained { get; set; } = 0;
        public bool leveledUp { get; set; } = false;
        public int newLevel { get; set; } = 0;
        
        public List<AppliedEffect> appliedEffects { get; set; } = new List<AppliedEffect>();
    }

    /// <summary>
    /// Efeito aplicado no alvo
    /// </summary>
    [Serializable]
    public class AppliedEffect
    {
        public string effectType { get; set; } = "";
        public int value { get; set; } = 0;
        public float duration { get; set; } = 0f;
    }

    /// <summary>
    /// Buff/Debuff ativo em um personagem
    /// </summary>
    [Serializable]
    public class ActiveEffect
    {
        public int id { get; set; } // ID único do efeito ativo
        public int skillId { get; set; } // Skill que causou
        public string effectType { get; set; } = "";
        public string targetStat { get; set; } = "";
        public int value { get; set; } = 0;
        public float startTime { get; set; } = 0f; // Timestamp do servidor
        public float duration { get; set; } = 0f;
        public string sourceId { get; set; } = ""; // Quem lançou
        
        public bool IsExpired(float currentTime)
        {
            return currentTime >= startTime + duration;
        }
    }

    /// <summary>
    /// Request de uso de skill
    /// </summary>
    [Serializable]
    public class UseSkillRequest
    {
        public int skillId { get; set; }
        public int slotNumber { get; set; }
        public string? targetId { get; set; } // null para self/AOE
        public string targetType { get; set; } = "monster"; // player, monster
        public Position? targetPosition { get; set; } // Para skills de área no chão
    }
}