using MMOServer.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace MMOServer.Server
{
    /// <summary>
    /// Gerenciador de Skills - Sistema completo de habilidades
    /// </summary>
    public class SkillManager
    {
        private static SkillManager? instance;
        public static SkillManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new SkillManager();
                return instance;
            }
        }

        private Dictionary<int, SkillTemplate> skillTemplates = new Dictionary<int, SkillTemplate>();
        private ConcurrentDictionary<string, List<ActiveEffect>> activeEffects = new ConcurrentDictionary<string, List<ActiveEffect>>();
        private Random random = new Random();
        private int nextEffectId = 1;

        public void Initialize()
        {
            Console.WriteLine("⚔️ SkillManager: Initializing...");
            LoadSkillTemplates();
            Console.WriteLine($"✅ SkillManager: Loaded {skillTemplates.Count} skill templates");
        }

        // ==================== CONFIGURAÇÃO ====================

        private void LoadSkillTemplates()
        {
            string filePath = Path.Combine("Config", "skills.json");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"⚠️ {filePath} not found!");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var config = JsonConvert.DeserializeObject<SkillConfig>(json);

                if (config?.skills != null)
                {
                    foreach (var skill in config.skills)
                    {
                        skillTemplates[skill.id] = skill;
                    }
                    Console.WriteLine($"✅ Loaded {skillTemplates.Count} skills");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading skills: {ex.Message}");
            }
        }

        public SkillTemplate? GetSkillTemplate(int skillId)
        {
            skillTemplates.TryGetValue(skillId, out var template);
            return template;
        }

        public List<SkillTemplate> GetSkillsByClass(string className)
        {
            return skillTemplates.Values
                .Where(s => s.requiredClass == className)
                .OrderBy(s => s.requiredLevel)
                .ThenBy(s => s.id)
                .ToList();
        }

        // ==================== USO DE SKILLS ====================

        /// <summary>
        /// Usa uma skill
        /// </summary>
        public SkillResult UseSkill(Player player, UseSkillRequest request, float currentTime)
        {
            var result = new SkillResult
            {
                attackerId = player.sessionId,
                attackerName = player.character.nome,
                attackerType = "player"
            };

            // Valida skill aprendida
            var learnedSkill = player.character.learnedSkills?
                .FirstOrDefault(s => s.skillId == request.skillId);

            if (learnedSkill == null)
            {
                result.success = false;
                result.failReason = "SKILL_NOT_LEARNED";
                return result;
            }

            // Carrega template
            var template = GetSkillTemplate(request.skillId);
            if (template == null)
            {
                result.success = false;
                result.failReason = "SKILL_NOT_FOUND";
                return result;
            }

            learnedSkill.template = template;

            // Valida cooldown
            if (!CanUseSkill(player, learnedSkill, currentTime))
            {
                result.success = false;
                result.failReason = "COOLDOWN";
                return result;
            }

            // Valida custos
            var levelData = GetSkillLevelData(template, learnedSkill.currentLevel);
            if (levelData == null)
            {
                result.success = false;
                result.failReason = "INVALID_LEVEL";
                return result;
            }

            if (player.character.mana < template.manaCost)
            {
                result.success = false;
                result.failReason = "NO_MANA";
                return result;
            }

            if (player.character.health <= template.healthCost)
            {
                result.success = false;
                result.failReason = "NO_HEALTH";
                return result;
            }

            // Valida range (se tiver alvo específico)
            if (request.targetId != null && template.targetType == "enemy")
            {
                var monster = MonsterManager.Instance.GetMonster(int.Parse(request.targetId));
                if (monster != null)
                {
                    float distance = GetDistance(player.position, monster.position);
                    if (distance > template.range)
                    {
                        result.success = false;
                        result.failReason = "OUT_OF_RANGE";
                        return result;
                    }
                }
            }

            // Consome recursos
            player.character.mana -= template.manaCost;
            player.character.health -= template.healthCost;
            result.manaCost = template.manaCost;
            result.healthCost = template.healthCost;

            // Atualiza cooldown
            learnedSkill.lastUsedTime = (long)(currentTime * 1000);

            // Executa skill
            result.success = true;
            ExecuteSkill(player, template, levelData, request, result, currentTime);

            // Salva alterações
            DatabaseHandler.Instance.UpdateCharacter(player.character);

            return result;
        }

        private void ExecuteSkill(Player player, SkillTemplate template, SkillLevelData levelData, 
            UseSkillRequest request, SkillResult result, float currentTime)
        {
            switch (template.targetType)
            {
                case "enemy":
                    ExecuteSingleTargetSkill(player, template, levelData, request, result, currentTime);
                    break;

                case "area":
                    ExecuteAreaSkill(player, template, levelData, request, result, currentTime);
                    break;

                case "self":
                    ExecuteSelfSkill(player, template, levelData, result, currentTime);
                    break;

                case "ally":
                    ExecuteAllySkill(player, template, levelData, request, result, currentTime);
                    break;
            }
        }

        private void ExecuteSingleTargetSkill(Player player, SkillTemplate template, SkillLevelData levelData,
            UseSkillRequest request, SkillResult result, float currentTime)
        {
            if (request.targetId == null)
                return;

            var monster = MonsterManager.Instance.GetMonster(int.Parse(request.targetId));
            if (monster == null || !monster.isAlive)
                return;

            var targetResult = CalculateSkillDamage(player, monster, template, levelData);
            
            // Aplica dano
            int actualDamage = monster.TakeDamage(targetResult.damage);
            targetResult.damage = actualDamage;
            targetResult.remainingHealth = monster.currentHealth;
            targetResult.targetDied = !monster.isAlive;

            // XP e level up
            if (targetResult.targetDied)
            {
                int exp = CombatManager.Instance.CalculateExperienceReward(
                    player.character.level, monster.template.level, monster.template.experienceReward);
                bool leveledUp = player.character.GainExperience(exp);

                targetResult.experienceGained = exp;
                targetResult.leveledUp = leveledUp;
                targetResult.newLevel = player.character.level;

                Console.WriteLine($"💀 {monster.template.name} killed by {template.name}! +{exp} XP");
            }

            // Aplica efeitos
            ApplySkillEffects(player, monster, template, targetResult, currentTime);

            result.targets.Add(targetResult);
        }

        private void ExecuteAreaSkill(Player player, SkillTemplate template, SkillLevelData levelData,
            UseSkillRequest request, SkillResult result, float currentTime)
        {
            Position center = request.targetPosition ?? player.position;

            // Busca todos os monstros vivos
            var monsters = MonsterManager.Instance.GetAliveMonsters();

            foreach (var monster in monsters)
            {
                float distance = GetDistance(center, monster.position);
                
                if (distance <= template.areaRadius)
                {
                    var targetResult = CalculateSkillDamage(player, monster, template, levelData);
                    
                    int actualDamage = monster.TakeDamage(targetResult.damage);
                    targetResult.damage = actualDamage;
                    targetResult.remainingHealth = monster.currentHealth;
                    targetResult.targetDied = !monster.isAlive;

                    if (targetResult.targetDied)
                    {
                        int exp = CombatManager.Instance.CalculateExperienceReward(
                            player.character.level, monster.template.level, monster.template.experienceReward);
                        bool leveledUp = player.character.GainExperience(exp);

                        targetResult.experienceGained = exp;
                        targetResult.leveledUp = leveledUp;
                        targetResult.newLevel = player.character.level;
                    }

                    ApplySkillEffects(player, monster, template, targetResult, currentTime);

                    result.targets.Add(targetResult);
                }
            }

            Console.WriteLine($"💥 {template.name} hit {result.targets.Count} targets in area!");
        }

        private void ExecuteSelfSkill(Player player, SkillTemplate template, SkillLevelData levelData,
            SkillResult result, float currentTime)
        {
            var targetResult = new SkillTargetResult
            {
                targetId = player.sessionId,
                targetName = player.character.nome,
                targetType = "player"
            };

            // Cura
            if (levelData.baseHealing > 0)
            {
                int healing = CalculateHealing(player, template, levelData);
                player.character.health = Math.Min(player.character.health + healing, player.character.maxHealth);
                targetResult.healing = healing;
                targetResult.remainingHealth = player.character.health;
            }

            // Aplica buffs em si mesmo
            foreach (var effect in template.effects)
            {
                if (effect.effectType == "buff_stat")
                {
                    ApplyBuff(player.sessionId, player.sessionId, template.id, effect, currentTime);
                    
                    targetResult.appliedEffects.Add(new AppliedEffect
                    {
                        effectType = effect.effectType,
                        value = effect.value,
                        duration = effect.duration
                    });
                }
            }

            result.targets.Add(targetResult);
        }

        private void ExecuteAllySkill(Player player, SkillTemplate template, SkillLevelData levelData,
            UseSkillRequest request, SkillResult result, float currentTime)
        {
            // Por enquanto, usa em si mesmo
            // TODO: Implementar target de aliados quando houver party system
            ExecuteSelfSkill(player, template, levelData, result, currentTime);
        }

        // ==================== CÁLCULOS ====================

        private SkillTargetResult CalculateSkillDamage(Player player, MonsterInstance monster, 
            SkillTemplate template, SkillLevelData levelData)
        {
            var result = new SkillTargetResult
            {
                targetId = monster.id.ToString(),
                targetName = monster.template.name,
                targetType = "monster"
            };

            // Calcula dano base
            int baseDamage = levelData.baseDamage;

            // Multiplica por ATK ou MATK
            int attackPower = template.damageType == "magical" 
                ? player.character.magicPower 
                : player.character.attackPower;

            int scaledDamage = (int)(attackPower * levelData.damageMultiplier);
            int totalDamage = baseDamage + scaledDamage;

            // Crítico
            float critChance = template.damageType == "magical"
                ? 0.05f // Magos têm menos crítico base
                : 0.01f + (player.character.dexterity * 0.003f);
            
            critChance += levelData.critChanceBonus;

            result.isCritical = random.NextDouble() < critChance;
            if (result.isCritical)
            {
                totalDamage = (int)(totalDamage * 1.5f);
            }

            // Redução de defesa
            int defense = monster.template.defense;
            float defReduction = 1.0f - (defense / (float)(defense + 100));
            defReduction = Math.Max(defReduction, 0.1f);

            totalDamage = (int)(totalDamage * defReduction);
            totalDamage = Math.Max(1, totalDamage); // Mínimo 1

            result.damage = totalDamage;

            return result;
        }

        private int CalculateHealing(Player player, SkillTemplate template, SkillLevelData levelData)
        {
            int baseHealing = levelData.baseHealing;
            int scaledHealing = (int)(player.character.magicPower * levelData.damageMultiplier);
            return baseHealing + scaledHealing;
        }

        // ==================== EFEITOS E BUFFS ====================

        private void ApplySkillEffects(Player player, MonsterInstance monster, SkillTemplate template,
            SkillTargetResult targetResult, float currentTime)
        {
            foreach (var effect in template.effects)
            {
                if (random.NextDouble() <= effect.chance)
                {
                    switch (effect.effectType)
                    {
                        case "stun":
                            // TODO: Implementar stun
                            break;

                        case "dot": // Damage over time
                            // TODO: Implementar DOT
                            break;
                    }

                    targetResult.appliedEffects.Add(new AppliedEffect
                    {
                        effectType = effect.effectType,
                        value = effect.value,
                        duration = effect.duration
                    });
                }
            }
        }

        private void ApplyBuff(string targetId, string sourceId, int skillId, SkillEffect effect, float currentTime)
        {
            if (!activeEffects.ContainsKey(targetId))
            {
                activeEffects[targetId] = new List<ActiveEffect>();
            }

            var activeEffect = new ActiveEffect
            {
                id = nextEffectId++,
                skillId = skillId,
                effectType = effect.effectType,
                targetStat = effect.targetStat,
                value = effect.value,
                startTime = currentTime,
                duration = effect.duration,
                sourceId = sourceId
            };

            activeEffects[targetId].Add(activeEffect);

            Console.WriteLine($"✨ Buff applied: {effect.targetStat} +{effect.value} for {effect.duration}s");
        }

        public void UpdateActiveEffects(float currentTime)
        {
            foreach (var kvp in activeEffects)
            {
                var effects = kvp.Value;
                effects.RemoveAll(e => e.IsExpired(currentTime));

                if (effects.Count == 0)
                {
                    activeEffects.TryRemove(kvp.Key, out _);
                }
            }
        }

        public List<ActiveEffect> GetActiveEffects(string playerId)
        {
            if (activeEffects.TryGetValue(playerId, out var effects))
            {
                return effects.ToList();
            }
            return new List<ActiveEffect>();
        }

        // ==================== APRENDIZADO DE SKILLS ====================

        public bool LearnSkill(Player player, int skillId, int slotNumber)
        {
            var template = GetSkillTemplate(skillId);
            
            if (template == null)
            {
                Console.WriteLine($"❌ Skill {skillId} not found");
                return false;
            }

            // Valida requisitos
            if (player.character.level < template.requiredLevel)
            {
                Console.WriteLine($"❌ Level too low: {player.character.level} < {template.requiredLevel}");
                return false;
            }

            if (!string.IsNullOrEmpty(template.requiredClass) && 
                template.requiredClass != player.character.classe)
            {
                Console.WriteLine($"❌ Wrong class: {player.character.classe} != {template.requiredClass}");
                return false;
            }

            // Verifica se já aprendeu
            if (player.character.learnedSkills == null)
            {
                player.character.learnedSkills = new List<LearnedSkill>();
            }

            var existing = player.character.learnedSkills.FirstOrDefault(s => s.skillId == skillId);
            if (existing != null)
            {
                Console.WriteLine($"❌ Skill {template.name} already learned");
                return false;
            }

            // Valida slot (1-9)
            if (slotNumber < 1 || slotNumber > 9)
            {
                Console.WriteLine($"❌ Invalid slot: {slotNumber}");
                return false;
            }

            // Remove skill anterior do slot
            var oldSkillInSlot = player.character.learnedSkills.FirstOrDefault(s => s.slotNumber == slotNumber);
            if (oldSkillInSlot != null)
            {
                oldSkillInSlot.slotNumber = 0; // Remove do slot
            }

            // Adiciona skill
            var learnedSkill = new LearnedSkill
            {
                skillId = skillId,
                currentLevel = 1,
                slotNumber = slotNumber,
                lastUsedTime = 0
            };

            player.character.learnedSkills.Add(learnedSkill);
            DatabaseHandler.Instance.UpdateCharacter(player.character);

            Console.WriteLine($"✅ {player.character.nome} learned {template.name} (Slot {slotNumber})");
            return true;
        }

        public bool LevelUpSkill(Player player, int skillId)
        {
            var learnedSkill = player.character.learnedSkills?.FirstOrDefault(s => s.skillId == skillId);
            
            if (learnedSkill == null)
            {
                Console.WriteLine($"❌ Skill not learned");
                return false;
            }

            var template = GetSkillTemplate(skillId);
            if (template == null)
                return false;

            if (learnedSkill.currentLevel >= template.maxLevel)
            {
                Console.WriteLine($"❌ Skill already at max level");
                return false;
            }

            var nextLevelData = GetSkillLevelData(template, learnedSkill.currentLevel + 1);
            if (nextLevelData == null)
                return false;

            if (player.character.statusPoints < nextLevelData.statusPointCost)
            {
                Console.WriteLine($"❌ Not enough status points: {player.character.statusPoints} < {nextLevelData.statusPointCost}");
                return false;
            }

            // Consome status points
            player.character.statusPoints -= nextLevelData.statusPointCost;
            learnedSkill.currentLevel++;

            DatabaseHandler.Instance.UpdateCharacter(player.character);

            Console.WriteLine($"✅ {template.name} leveled up to {learnedSkill.currentLevel}!");
            return true;
        }

        // ==================== VALIDAÇÕES ====================

        private bool CanUseSkill(Player player, LearnedSkill learnedSkill, float currentTime)
        {
            if (learnedSkill.template == null)
                return false;

            float cooldown = learnedSkill.template.cooldown;
            float lastUsed = learnedSkill.lastUsedTime / 1000f;
            float timeSinceLastUse = currentTime - lastUsed;

            return timeSinceLastUse >= cooldown;
        }

        private SkillLevelData? GetSkillLevelData(SkillTemplate template, int level)
        {
            return template.levels.FirstOrDefault(l => l.level == level);
        }

        private float GetDistance(Position pos1, Position pos2)
        {
            float dx = pos1.x - pos2.x;
            float dz = pos1.z - pos2.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public void ReloadConfigs()
        {
            Console.WriteLine("🔄 Reloading skill configurations...");
            skillTemplates.Clear();
            LoadSkillTemplates();
            Console.WriteLine("✅ Skill configurations reloaded!");
        }
    }

    [Serializable]
    public class SkillConfig
    {
        public List<SkillTemplate> skills { get; set; } = new List<SkillTemplate>();
    }
}