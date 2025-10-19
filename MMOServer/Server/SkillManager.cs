using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using MMOServer.Models;

namespace MMOServer.Server
{
    /// <summary>
    /// Gerenciador de Skills no Servidor (Authoritative)
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
        private Dictionary<string, List<ActiveEffect>> activeEffects = new Dictionary<string, List<ActiveEffect>>();
        
        // ✅ Tempo do servidor (único e autoritativo)
        private static DateTime serverStartTime = DateTime.UtcNow;
        private int nextEffectId = 1;
        private Random random = new Random();

        /// <summary>
        /// ✅ Obtém tempo do servidor em segundos (desde o início)
        /// TODA validação de cooldown DEVE usar este método
        /// </summary>
        public static float GetServerTime()
        {
            return (float)(DateTime.UtcNow - serverStartTime).TotalSeconds;
        }

        public void Initialize()
        {
            Console.WriteLine("⚔️ SkillManager: Initializing...");
            
            serverStartTime = DateTime.UtcNow;
            LoadSkillTemplates();
            
            Console.WriteLine($"✅ SkillManager: Loaded {skillTemplates.Count} skill templates");
            Console.WriteLine($"⏰ Server time initialized at: {serverStartTime:yyyy-MM-dd HH:mm:ss}");
        }

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
                    
                    Console.WriteLine($"✅ Loaded {skillTemplates.Count} skill templates from {filePath}");
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
                .Where(s => string.IsNullOrEmpty(s.requiredClass) || s.requiredClass == className)
                .ToList();
        }

        /// <summary>
        /// ✅ Usa skill (VERSÃO CORRIGIDA - Sempre usa GetServerTime())
        /// </summary>
        public SkillResult UseSkill(Player player, UseSkillRequest request, float currentTime)
        {
            var result = new SkillResult
            {
                attackerId = player.sessionId,
                attackerName = player.character.nome,
                attackerType = "player",
                skillId = request.skillId,
                targets = new List<SkillTargetResult>()
            };

            // Busca skill aprendida
            var skill = player.character.learnedSkills?.FirstOrDefault(s => s.skillId == request.skillId);
            
            if (skill == null)
            {
                result.success = false;
                result.failReason = "SKILL_NOT_LEARNED";
                return result;
            }

            // Carrega template
            var template = GetSkillTemplate(skill.skillId);
            
            if (template == null)
            {
                result.success = false;
                result.failReason = "TEMPLATE_NOT_FOUND";
                return result;
            }

            skill.template = template;

            // ✅ VALIDAÇÃO DE COOLDOWN (usando tempo do servidor)
            Console.WriteLine($"🕐 Cooldown check for {template.name}:");
            Console.WriteLine($"   Current time: {currentTime:F2}s");
            Console.WriteLine($"   Last used: {(skill.lastUsedTime / 1000f):F2}s");
            Console.WriteLine($"   Time since use: {(currentTime - skill.lastUsedTime / 1000f):F2}s");
            Console.WriteLine($"   Cooldown needed: {template.cooldown:F2}s");
            Console.WriteLine($"   Can use: {!skill.IsOnCooldown(currentTime)}");

            if (skill.IsOnCooldown(currentTime))
            {
                result.success = false;
                result.failReason = "COOLDOWN";
                return result;
            }

            // Validação de mana
            if (player.character.mana < template.manaCost)
            {
                result.success = false;
                result.failReason = "NO_MANA";
                return result;
            }

            // Validação de HP
            if (player.character.health < template.healthCost)
            {
                result.success = false;
                result.failReason = "NO_HEALTH";
                return result;
            }

            // Processa alvos
            var targets = GetSkillTargets(player, request, template);
            
            if (targets.Count == 0)
            {
                result.success = false;
                result.failReason = "NO_VALID_TARGET";
                return result;
            }

            // ✅ ATUALIZA COOLDOWN (ANTES de executar)
            skill.lastUsedTime = (long)(currentTime * 1000);
            result.lastUsedTime = skill.lastUsedTime;

            // Consome recursos
            player.character.mana -= template.manaCost;
            player.character.health -= template.healthCost;
            result.manaCost = template.manaCost;
            result.healthCost = template.healthCost;

            // Executa skill nos alvos
            foreach (var target in targets)
            {
                var targetResult = ExecuteSkillOnTarget(player, target, skill, currentTime);
                result.targets.Add(targetResult);
            }

            result.success = true;
            
            Console.WriteLine($"✅ Skill {template.name} used successfully by {player.character.nome}");
            Console.WriteLine($"   New lastUsedTime: {skill.lastUsedTime} ({currentTime:F2}s)");
            Console.WriteLine($"   Targets hit: {result.targets.Count}");

            return result;
        }

        private List<object> GetSkillTargets(Player player, UseSkillRequest request, SkillTemplate template)
        {
            var targets = new List<object>();

            switch (template.targetType)
            {
                case "enemy":
                    if (!string.IsNullOrEmpty(request.targetId))
                    {
                        var monster = MonsterManager.Instance.GetMonster(int.Parse(request.targetId));
                        
                        if (monster != null && monster.isAlive)
                        {
                            float distance = GetDistance(player.position, monster.position);
                            
                            Console.WriteLine($"📏 Distance to target: {distance:F2}m (max: {template.range:F1}m)");
                            
                            if (distance <= template.range)
                            {
                                targets.Add(monster);
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ Target out of range!");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Monster {request.targetId} not found or dead");
                        }
                    }
                    break;

                case "self":
                    targets.Add(player);
                    break;

                case "area":
                    var nearbyMonsters = MonsterManager.Instance.GetAllMonsters()
                        .Where(m => m.isAlive && GetDistance(player.position, m.position) <= template.areaRadius)
                        .ToList();
                    
                    targets.AddRange(nearbyMonsters);
                    break;
            }

            return targets;
        }

        private SkillTargetResult ExecuteSkillOnTarget(Player attacker, object target, LearnedSkill skill, float currentTime)
        {
            var result = new SkillTargetResult();
            var template = skill.template!;
            var levelData = template.levels.FirstOrDefault(l => l.level == skill.currentLevel);

            if (levelData == null)
            {
                levelData = template.levels.FirstOrDefault() ?? new SkillLevelData();
            }

            if (target is MonsterInstance monster)
            {
                result.targetId = monster.id.ToString();
                result.targetName = monster.template.name;
                result.targetType = "monster";

                // Calcula dano
                if (levelData.baseDamage > 0)
                {
                    int attackStat = template.damageType == "magical" 
                        ? attacker.character.magicPower 
                        : attacker.character.attackPower;

                    int totalDamage = levelData.baseDamage + (int)(attackStat * levelData.damageMultiplier);
                    
                    // Crítico
                    float critChance = 0.05f + levelData.critChanceBonus;
                    bool isCritical = random.NextDouble() < critChance;
                    
                    if (isCritical)
                    {
                        totalDamage = (int)(totalDamage * 1.5f);
                    }

                    result.damage = monster.TakeDamage(totalDamage);
                    result.isCritical = isCritical;
                    result.remainingHealth = monster.currentHealth;
                    result.targetDied = !monster.isAlive;

                    Console.WriteLine($"   💥 {attacker.character.nome} hit {monster.template.name} for {result.damage} damage");

                    if (result.targetDied)
                    {
                        int exp = monster.template.experienceReward;
                        bool leveledUp = attacker.character.GainExperience(exp);
                        
                        result.experienceGained = exp;
                        result.leveledUp = leveledUp;
                        result.newLevel = attacker.character.level;

                        Console.WriteLine($"   💀 {monster.template.name} died! {attacker.character.nome} gained {exp} XP");
                    }
                }
            }
            else if (target is Player targetPlayer)
            {
                result.targetId = targetPlayer.sessionId;
                result.targetName = targetPlayer.character.nome;
                result.targetType = "player";

                // Cura
                if (levelData.baseHealing > 0)
                {
                    int healing = levelData.baseHealing + (int)(attacker.character.magicPower * levelData.damageMultiplier);
                    int oldHealth = targetPlayer.character.health;
                    
                    targetPlayer.character.health = Math.Min(
                        targetPlayer.character.health + healing,
                        targetPlayer.character.maxHealth
                    );

                    result.healing = targetPlayer.character.health - oldHealth;
                    result.remainingHealth = targetPlayer.character.health;
                }
            }

            // Aplica efeitos
            if (template.effects != null && template.effects.Count > 0)
            {
                ApplySkillEffects(attacker, target, template, currentTime);
            }

            return result;
        }

        private void ApplySkillEffects(Player caster, object target, SkillTemplate template, float currentTime)
        {
            foreach (var effect in template.effects)
            {
                if (random.NextDouble() > effect.chance)
                    continue;

                string targetId = target is Player p ? p.sessionId : 
                                 target is MonsterInstance m ? m.id.ToString() : "";

                if (string.IsNullOrEmpty(targetId))
                    continue;

                var activeEffect = new ActiveEffect
                {
                    id = nextEffectId++,
                    skillId = template.id,
                    effectType = effect.effectType,
                    targetStat = effect.targetStat,
                    value = effect.value,
                    startTime = currentTime,
                    duration = effect.duration,
                    sourceId = caster.sessionId
                };

                if (!activeEffects.ContainsKey(targetId))
                {
                    activeEffects[targetId] = new List<ActiveEffect>();
                }

                activeEffects[targetId].Add(activeEffect);
                
                Console.WriteLine($"   ✨ Applied {effect.effectType} to {targetId}");
            }
        }

        public void UpdateActiveEffects(float currentTime)
        {
            foreach (var kvp in activeEffects.ToList())
            {
                var expired = kvp.Value.Where(e => e.IsExpired(currentTime)).ToList();
                
                foreach (var effect in expired)
                {
                    kvp.Value.Remove(effect);
                    Console.WriteLine($"   ⏱️ Effect {effect.effectType} expired on {kvp.Key}");
                }

                if (kvp.Value.Count == 0)
                {
                    activeEffects.Remove(kvp.Key);
                }
            }
        }

        public bool LearnSkill(Player player, int skillId, int slotNumber)
        {
            var template = GetSkillTemplate(skillId);
            
            if (template == null)
                return false;

            if (player.character.level < template.requiredLevel)
                return false;

            if (!string.IsNullOrEmpty(template.requiredClass) && 
                player.character.classe != template.requiredClass)
                return false;

            if (player.character.learnedSkills.Any(s => s.skillId == skillId))
                return false;

            var skill = new LearnedSkill
            {
                skillId = skillId,
                currentLevel = 1,
                slotNumber = slotNumber,
                lastUsedTime = 0,
                template = template
            };

            player.character.learnedSkills.Add(skill);
            DatabaseHandler.Instance.UpdateCharacter(player.character);
            
            Console.WriteLine($"📚 {player.character.nome} learned {template.name}");
            return true;
        }

        public bool LevelUpSkill(Player player, int skillId)
        {
            var skill = player.character.learnedSkills?.FirstOrDefault(s => s.skillId == skillId);
            
            if (skill == null)
                return false;

            var template = GetSkillTemplate(skillId);
            
            if (template == null)
                return false;

            if (skill.currentLevel >= template.maxLevel)
                return false;

            var nextLevelData = template.levels.FirstOrDefault(l => l.level == skill.currentLevel + 1);
            
            if (nextLevelData == null)
                return false;

            if (player.character.statusPoints < nextLevelData.statusPointCost)
                return false;

            player.character.statusPoints -= nextLevelData.statusPointCost;
            skill.currentLevel++;

            DatabaseHandler.Instance.UpdateCharacter(player.character);
            
            Console.WriteLine($"⬆️ {player.character.nome}'s {template.name} leveled up to {skill.currentLevel}");
            return true;
        }

        private float GetDistance(Position pos1, Position pos2)
        {
            float dx = pos1.x - pos2.x;
            float dz = pos1.z - pos2.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    [Serializable]
    public class SkillConfig
    {
        public List<SkillTemplate> skills { get; set; } = new List<SkillTemplate>();
    }
}
