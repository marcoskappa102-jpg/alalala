using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MMOClient.Skills
{
    /// <summary>
    /// Gerenciador de Skills no Cliente
    /// </summary>
    public class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }

        [Header("Configurações")]
        public KeyCode[] skillSlotKeys = new KeyCode[]
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
            KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
        };

        // Skills aprendidas pelo jogador
        private List<LearnedSkill> learnedSkills = new List<LearnedSkill>();
        
        // Skills mapeadas por slot (1-9)
        private Dictionary<int, LearnedSkill> skillSlots = new Dictionary<int, LearnedSkill>();
        
        // Cooldowns visuais
        private Dictionary<int, float> cooldownTimers = new Dictionary<int, float>();
        
        // Casting
        private bool isCasting = false;
        private float castingStartTime = 0f;
        private float castingDuration = 0f;
        private LearnedSkill currentCastingSkill = null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Registra eventos do MessageHandler
            if (MessageHandler.Instance != null)
            {
                MessageHandler.Instance.OnMessageReceived += HandleSkillMessages;
            }
        }

        private void Update()
        {
            // Input de skills (1-9)
            for (int i = 0; i < skillSlotKeys.Length; i++)
            {
                if (Input.GetKeyDown(skillSlotKeys[i]))
                {
                    int slotNumber = i + 1;
                    TryUseSkill(slotNumber);
                }
            }

            // Atualiza casting
            UpdateCasting();
        }

        /// <summary>
        /// Carrega skills do personagem
        /// </summary>
        public void LoadSkills(List<LearnedSkill> skills)
        {
            learnedSkills = skills ?? new List<LearnedSkill>();
            
            // Mapeia skills por slot
            skillSlots.Clear();
            foreach (var skill in learnedSkills)
            {
                if (skill.slotNumber >= 1 && skill.slotNumber <= 9)
                {
                    skillSlots[skill.slotNumber] = skill;
                }
            }

            Debug.Log($"✅ Loaded {learnedSkills.Count} skills");
            
            // Atualiza UI
            if (SkillbarUI.Instance != null)
            {
                SkillbarUI.Instance.RefreshSkillbar(skillSlots);
            }
        }

        /// <summary>
        /// Tenta usar skill no slot
        /// </summary>
        public void TryUseSkill(int slotNumber)
        {
            if (!skillSlots.TryGetValue(slotNumber, out var skill))
            {
                Debug.Log($"⚠️ No skill in slot {slotNumber}");
                return;
            }

            if (skill.template == null)
            {
                Debug.LogWarning($"⚠️ Skill {skill.skillId} has no template!");
                return;
            }

            // Valida se pode usar
            if (!CanUseSkill(skill))
                return;

            // Inicia casting (se tiver)
            if (skill.template.castTime > 0f)
            {
                StartCasting(skill);
            }
            else
            {
                ExecuteSkill(skill);
            }
        }

        /// <summary>
        /// Verifica se pode usar skill
        /// </summary>
        private bool CanUseSkill(LearnedSkill skill)
        {
            // Já está castando
            if (isCasting)
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog("<color=yellow>⏳ Já está conjurando outra skill!</color>");
                }
                return false;
            }

            // Cooldown
            float currentTime = Time.time;
            if (skill.IsOnCooldown(currentTime))
            {
                float remaining = skill.GetCooldownRemaining(currentTime);
                
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog($"<color=orange>⏳ {skill.template.name} em cooldown ({remaining:F1}s)</color>");
                }
                return false;
            }

            // Mana
            if (WorldManager.Instance != null)
            {
                var charData = WorldManager.Instance.GetLocalCharacterData();
                
                if (charData != null && charData.mana < skill.template.manaCost)
                {
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.AddCombatLog($"<color=cyan>⚠️ Mana insuficiente! ({charData.mana}/{skill.template.manaCost})</color>");
                    }
                    return false;
                }
            }

    // ✅ NOVO: Valida range para skills de target único
    if (skill.template.targetType == "enemy")
    {
        var player = GetLocalPlayer();
        
        if (player == null)
        {
            Debug.LogError("❌ CanUseSkill: Player not found!");
            return false;
        }

        if (!player.targetMonsterId.HasValue)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=red>⚠️ Selecione um alvo primeiro!</color>");
            }
            return false;
        }

        // Verifica se o monstro ainda existe
        var monsterObj = GameObject.Find($"Monster_{player.targetMonsterId.Value}");
        
        if (monsterObj == null)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=red>⚠️ Alvo não encontrado!</color>");
            }
            return false;
        }

        var monster = monsterObj.GetComponent<MonsterController>();
        
        if (monster == null || !monster.isAlive)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=red>⚠️ Alvo está morto!</color>");
            }
            return false;
        }

        // Verifica range
        float distance = Vector3.Distance(player.transform.position, monster.transform.position);
        
        if (distance > skill.template.range)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=orange>⚠️ Alvo muito longe! ({distance:F1}m / {skill.template.range}m)</color>");
            }
            return false;
        }
    }

    return true;
        }

        /// <summary>
        /// Inicia casting da skill
        /// </summary>
        private void StartCasting(LearnedSkill skill)
        {
            isCasting = true;
            castingStartTime = Time.time;
            castingDuration = skill.template.castTime;
            currentCastingSkill = skill;

            Debug.Log($"🔮 Casting {skill.template.name} ({castingDuration}s)...");

            // Atualiza UI de casting
            if (SkillbarUI.Instance != null)
            {
                SkillbarUI.Instance.ShowCastBar(skill.template.name, castingDuration);
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=magenta>🔮 Conjurando {skill.template.name}...</color>");
            }
        }

        /// <summary>
        /// Atualiza casting
        /// </summary>
        private void UpdateCasting()
        {
            if (!isCasting)
                return;

            float elapsed = Time.time - castingStartTime;
            float progress = elapsed / castingDuration;

            // Atualiza barra de casting
            if (SkillbarUI.Instance != null)
            {
                SkillbarUI.Instance.UpdateCastBar(progress);
            }

            // Completo?
            if (elapsed >= castingDuration)
            {
                CompleteCasting();
            }
        }

        /// <summary>
        /// Completa casting e executa skill
        /// </summary>
        private void CompleteCasting()
        {
            if (currentCastingSkill != null)
            {
                ExecuteSkill(currentCastingSkill);
            }

            CancelCasting();
        }

        /// <summary>
        /// Cancela casting
        /// </summary>
        public void CancelCasting()
        {
            isCasting = false;
            castingStartTime = 0f;
            castingDuration = 0f;
            currentCastingSkill = null;

            if (SkillbarUI.Instance != null)
            {
                SkillbarUI.Instance.HideCastBar();
            }
        }

        /// <summary>
        /// Executa skill (envia para servidor)
        /// </summary>
private void ExecuteSkill(LearnedSkill skill)
{
    if (skill == null || skill.template == null)
    {
        Debug.LogError("❌ ExecuteSkill: Skill or template is null!");
        return;
    }

    // Determina alvo baseado no tipo
    string targetId = null;
    string targetType = null;
    
    Debug.Log($"🔍 ExecuteSkill: {skill.template.name} (Type: {skill.template.targetType})");

    // ✅ CORREÇÃO: Validação melhorada de target
    if (skill.template.targetType == "enemy") // ✅ MUDOU DE "Monster" PARA "enemy"
    {
        // Pega target atual do player
        var player = GetLocalPlayer();
        
        if (player == null)
        {
            Debug.LogError("❌ Local player not found!");
            
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=red>⚠️ Erro: Jogador não encontrado!</color>");
            }
            return;
        }

        Debug.Log($"🎯 Player target monster ID: {player.targetMonsterId}");

        if (!player.targetMonsterId.HasValue)
        {
            Debug.LogWarning($"⚠️ No target selected for skill {skill.template.name}");
            
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=red>⚠️ Selecione um alvo primeiro!</color>");
            }
            return;
        }

        targetId = player.targetMonsterId.Value.ToString();
        targetType = "monster";
        Debug.Log($"✅ Target found: Monster ID {targetId}");
    }
    else if (skill.template.targetType == "self")
    {
        // Skill em si mesmo
        var player = GetLocalPlayer();
        if (player != null)
        {
            targetId = player.playerId;
            targetType = "player";
            Debug.Log($"✅ Self-target: Player {targetId}");
        }
        else
        {
            Debug.LogError("❌ Local player not found for self-target skill!");
            return;
        }
    }
    else if (skill.template.targetType == "area")
    {
        // Skill em área - não precisa de target específico
        targetId = null;
        targetType = "area";
        Debug.Log($"✅ Area skill - no specific target needed");
    }

    // ✅ CORREÇÃO: Cria request com TODOS os campos necessários
    var request = new
    {
        type = "useSkill",
        skillId = skill.skillId,
        slotNumber = skill.slotNumber,
        targetId = targetId,
        targetType = targetType ?? "monster",
        targetPosition = (object)null // Pode ser usado para skills de área no futuro
    };

    string json = JsonConvert.SerializeObject(request);
    
    // 🔍 DEBUG: Log completo do que está sendo enviado
    Debug.Log($"📤 SENDING useSkill request:");
    Debug.Log($"   Skill ID: {skill.skillId}");
    Debug.Log($"   Skill Name: {skill.template.name}");
    Debug.Log($"   Slot: {skill.slotNumber}");
    Debug.Log($"   Target ID: {targetId ?? "NULL"}");
    Debug.Log($"   Target Type: {targetType ?? "NULL"}");
    Debug.Log($"   JSON: {json}");

    ClientManager.Instance.SendMessage(json);

    // Atualiza cooldown localmente (otimista)
    skill.lastUsedTime = (long)(Time.time * 1000);
    cooldownTimers[skill.skillId] = Time.time;

    // Atualiza UI
    if (SkillbarUI.Instance != null)
    {
        SkillbarUI.Instance.UpdateCooldown(skill.slotNumber, skill.template.cooldown);
    }

    Debug.Log($"✅ Skill request sent successfully");
}

        /// <summary>
        /// Processa mensagens de skill do servidor
        /// </summary>
        private void HandleSkillMessages(string message)
        {
            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(message);
                var type = json["type"]?.ToString();

                switch (type)
                {
                    case "skillUsed":
                        HandleSkillUsed(json);
                        break;

                    case "skillUseFailed":
                        HandleSkillUseFailed(json);
                        break;

                    case "skillLearned":
                        HandleSkillLearned(json);
                        break;

                    case "skillLeveledUp":
                        HandleSkillLeveledUp(json);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error handling skill message: {ex.Message}");
            }
        }

        private void HandleSkillUsed(Newtonsoft.Json.Linq.JObject json)
        {
            var result = json["result"]?.ToObject<SkillResult>();
            
            if (result == null)
                return;

            Debug.Log($"⚔️ Skill result: {result.attackerName} - Success: {result.success}");

            // Atualiza MP/HP local
            if (result.attackerId == ClientManager.Instance.PlayerId)
            {
                var charData = WorldManager.Instance?.GetLocalCharacterData();
                
                if (charData != null)
                {
                    charData.mana -= result.manaCost;
                    charData.health -= result.healthCost;

                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.UpdateManaBar(charData.mana, charData.maxMana);
                        UIManager.Instance.UpdateHealthBar(charData.health, charData.maxHealth);
                    }
                }
            }

            // Mostra dano nos alvos
            if (result.targets != null)
            {
                foreach (var target in result.targets)
                {
                    if (target.damage > 0)
                    {
                        // Encontra monstro e mostra dano
                        var monsterObj = GameObject.Find($"Monster_{target.targetName}_{target.targetId}");
                        
                        if (monsterObj != null)
                        {
                            var monster = monsterObj.GetComponent<MonsterController>();
                            monster?.ShowDamage(target.damage, target.isCritical);
                        }

                        if (UIManager.Instance != null)
                        {
                            string critText = target.isCritical ? " <color=red>CRÍTICO!</color>" : "";
                            UIManager.Instance.AddCombatLog($"<color=orange>⚔️ Causou {target.damage}{critText} em {target.targetName}</color>");
                        }
                    }
                }
            }
        }

        private void HandleSkillUseFailed(Newtonsoft.Json.Linq.JObject json)
        {
            var skillId = json["skillId"]?.ToObject<int>() ?? 0;
            var reason = json["reason"]?.ToString() ?? "";

            string message = reason switch
            {
                "COOLDOWN" => "⏳ Skill em cooldown!",
                "NO_MANA" => "💧 Mana insuficiente!",
                "NO_HEALTH" => "❤️ HP insuficiente!",
                "OUT_OF_RANGE" => "📏 Alvo muito longe!",
                "SKILL_NOT_LEARNED" => "📚 Skill não aprendida!",
                _ => $"⚠️ Não pode usar skill ({reason})"
            };

            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddCombatLog($"<color=yellow>{message}</color>");
            }

            Debug.LogWarning($"Skill {skillId} failed: {reason}");
        }

        private void HandleSkillLearned(Newtonsoft.Json.Linq.JObject json)
        {
            bool success = json["success"]?.ToObject<bool>() ?? false;
            
            if (success)
            {
                string skillName = json["skillName"]?.ToString() ?? "Skill";
                int slotNumber = json["slotNumber"]?.ToObject<int>() ?? 0;

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog($"<color=lime>✅ Aprendeu {skillName} (Slot {slotNumber})!</color>");
                }

                // Recarrega skills
                RequestSkills();
            }
        }

        private void HandleSkillLeveledUp(Newtonsoft.Json.Linq.JObject json)
        {
            bool success = json["success"]?.ToObject<bool>() ?? false;
            
            if (success)
            {
                int newLevel = json["newLevel"]?.ToObject<int>() ?? 1;

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.AddCombatLog($"<color=cyan>⬆️ Skill evoluiu para nível {newLevel}!</color>");
                }

                RequestSkills();
            }
        }

        /// <summary>
        /// Solicita skills do servidor
        /// </summary>
        public void RequestSkills()
        {
            var message = new { type = "getSkills" };
            string json = JsonConvert.SerializeObject(message);
            ClientManager.Instance.SendMessage(json);
        }

        /// <summary>
        /// Obtém skill por slot
        /// </summary>
        public LearnedSkill GetSkillInSlot(int slotNumber)
        {
            skillSlots.TryGetValue(slotNumber, out var skill);
            return skill;
        }

        /// <summary>
        /// Obtém todas as skills
        /// </summary>
        public List<LearnedSkill> GetAllSkills()
        {
            return learnedSkills;
        }

        private void OnDestroy()
        {
            if (MessageHandler.Instance != null)
            {
                MessageHandler.Instance.OnMessageReceived -= HandleSkillMessages;
            }
        }
		
		
		/// <summary>
/// Helper para pegar o player local - VERSÃO MELHORADA
/// </summary>
private PlayerController GetLocalPlayer()
{
    // Método 1: Busca por Tag
    var localPlayerObj = GameObject.FindGameObjectWithTag("Player");
    
    if (localPlayerObj != null)
    {
        var player = localPlayerObj.GetComponent<PlayerController>();
        
        if (player != null && player.isLocalPlayer)
        {
            Debug.Log($"✅ Found local player: {player.characterName}");
            return player;
        }
    }

    // Método 2: Busca por isLocalPlayer
    var allPlayers = FindObjectsOfType<PlayerController>();
    
    foreach (var player in allPlayers)
    {
        if (player.isLocalPlayer)
        {
            Debug.Log($"✅ Found local player (search): {player.characterName}");
            return player;
        }
    }

    // Método 3: Via WorldManager
    if (WorldManager.Instance != null)
    {
        var charData = WorldManager.Instance.GetLocalCharacterData();
        
        if (charData != null)
        {
            Debug.Log($"🔍 Searching for player by character name: {charData.nome}");
            
            foreach (var player in allPlayers)
            {
                if (player.characterName == charData.nome)
                {
                    Debug.Log($"✅ Found local player by name: {player.characterName}");
                    return player;
                }
            }
        }
    }

    Debug.LogError("❌ GetLocalPlayer: Could not find local player using any method!");
    Debug.LogError($"   Total PlayerControllers in scene: {allPlayers.Length}");
    
    foreach (var p in allPlayers)
    {
        Debug.LogError($"   - Player: {p.characterName}, isLocal: {p.isLocalPlayer}, Tag: {p.tag}");
    }

    return null;
}
    }
}
