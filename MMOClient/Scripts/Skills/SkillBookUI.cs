using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MMOClient.Skills
{
    /// <summary>
    /// UI do livro de skills - COMPLETO E FUNCIONAL
    /// Gerencia abertura com tecla K, lista de skills disponíveis, e aprendizado
    /// </summary>
    public class SkillBookUI : MonoBehaviour
    {
        public static SkillBookUI Instance { get; private set; }

        [Header("Panels")]
        public GameObject skillBookPanel;
        public GameObject skillListPanel;
        public GameObject skillDetailPanel;

        [Header("Skill List")]
        public Transform skillListContent;
        public GameObject skillEntryPrefab;

        [Header("Skill Details")]
        public TextMeshProUGUI skillNameText;
        public TextMeshProUGUI skillDescriptionText;
        public TextMeshProUGUI skillStatsText;
        public TextMeshProUGUI skillRequirementsText;
        public Image skillIconImage;
        public Button learnButton;
        public Button closeButton;

        [Header("Filter")]
        public TMP_Dropdown filterDropdown;

        private List<SkillTemplate> availableSkills = new List<SkillTemplate>();
        private List<SkillBookEntry> skillEntries = new List<SkillBookEntry>();
        private SkillTemplate selectedSkill;
        private bool isVisible = false;

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
            // Configura botões
            if (learnButton != null)
                learnButton.onClick.AddListener(OnLearnButtonClick);

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            // Configura filtro
            if (filterDropdown != null)
            {
                filterDropdown.onValueChanged.AddListener(OnFilterChanged);
            }

            // Registra eventos
            if (MessageHandler.Instance != null)
            {
                MessageHandler.Instance.OnMessageReceived += HandleServerMessage;
            }

            Hide();
        }

        private void Update()
        {
            // Hotkey K para abrir/fechar
            if (Input.GetKeyDown(KeyCode.K))
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            if (isVisible)
                Hide();
            else
                Show();
        }

        public void Show()
        {
            if (skillBookPanel != null)
                skillBookPanel.SetActive(true);

            isVisible = true;

            // Solicita lista de skills do servidor
            RequestSkillList();
        }

        public void Hide()
        {
            if (skillBookPanel != null)
                skillBookPanel.SetActive(false);

            if (skillDetailPanel != null)
                skillDetailPanel.SetActive(false);

            isVisible = false;
            selectedSkill = null;
        }

        private void RequestSkillList()
        {
            var message = new
            {
                type = "getSkillList"
            };

            string json = JsonConvert.SerializeObject(message);
            ClientManager.Instance.SendMessage(json);

            Debug.Log("📚 Requesting skill list from server...");
        }

        private void HandleServerMessage(string message)
        {
            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(message);
                var type = json["type"]?.ToString();

                if (type == "skillListResponse")
                {
                    HandleSkillListResponse(json);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error handling skill book message: {ex.Message}");
            }
        }

        private void HandleSkillListResponse(Newtonsoft.Json.Linq.JObject json)
        {
            try
            {
                var skillsArray = json["skills"];

                if (skillsArray == null)
                {
                    Debug.LogWarning("⚠️ No skills in response");
                    return;
                }

                availableSkills.Clear();

                foreach (var skillJson in skillsArray)
                {
                    var skill = new SkillTemplate
                    {
                        id = skillJson["id"]?.ToObject<int>() ?? 0,
                        name = skillJson["name"]?.ToString() ?? "",
                        description = skillJson["description"]?.ToString() ?? "",
                        skillType = skillJson["skillType"]?.ToString() ?? "",
                        damageType = skillJson["damageType"]?.ToString() ?? "",
                        targetType = skillJson["targetType"]?.ToString() ?? "",
                        requiredLevel = skillJson["requiredLevel"]?.ToObject<int>() ?? 1,
                        requiredClass = skillJson["requiredClass"]?.ToString() ?? "",
                        maxLevel = skillJson["maxLevel"]?.ToObject<int>() ?? 1,
                        manaCost = skillJson["manaCost"]?.ToObject<int>() ?? 0,
                        cooldown = skillJson["cooldown"]?.ToObject<float>() ?? 0f,
                        iconPath = skillJson["iconPath"]?.ToString() ?? "",
                        levels = new List<SkillLevelData>()
                    };

                    // Carrega dados por nível
                    var levelsArray = skillJson["levels"];
                    if (levelsArray != null)
                    {
                        foreach (var levelJson in levelsArray)
                        {
                            skill.levels.Add(new SkillLevelData
                            {
                                level = levelJson["level"]?.ToObject<int>() ?? 1,
                                baseDamage = levelJson["baseDamage"]?.ToObject<int>() ?? 0,
                                baseHealing = levelJson["baseHealing"]?.ToObject<int>() ?? 0,
                                damageMultiplier = levelJson["damageMultiplier"]?.ToObject<float>() ?? 1f,
                                critChanceBonus = levelJson["critChanceBonus"]?.ToObject<float>() ?? 0f,
                                statusPointCost = levelJson["statusPointCost"]?.ToObject<int>() ?? 1
                            });
                        }
                    }

                    // Verifica se pode aprender
                    bool canLearn = skillJson["canLearn"]?.ToObject<bool>() ?? false;

                    // Adiciona à lista se puder aprender
                    if (canLearn)
                    {
                        availableSkills.Add(skill);
                    }
                }

                Debug.Log($"📚 Received {availableSkills.Count} learnable skills");

                RefreshSkillList();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing skill list: {ex.Message}");
            }
        }

        private void RefreshSkillList()
        {
            // Limpa lista antiga
            foreach (var entry in skillEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            skillEntries.Clear();

            if (skillEntryPrefab == null || skillListContent == null)
            {
                Debug.LogError("❌ SkillBookUI: Missing prefab or content!");
                return;
            }

            // Filtra skills
            var filteredSkills = FilterSkills(availableSkills);

            // Cria entradas
            foreach (var skill in filteredSkills)
            {
                GameObject entryObj = Instantiate(skillEntryPrefab, skillListContent);
                SkillBookEntry entry = entryObj.GetComponent<SkillBookEntry>();

                if (entry != null)
                {
                    entry.Setup(skill, OnSkillSelected);
                    skillEntries.Add(entry);
                }
            }

            Debug.Log($"📚 Displayed {filteredSkills.Count} skills in list");
        }

        private List<SkillTemplate> FilterSkills(List<SkillTemplate> skills)
        {
            if (filterDropdown == null)
                return skills;

            int filterIndex = filterDropdown.value;

            return filterIndex switch
            {
                0 => skills, // Todas
                1 => skills.Where(s => s.skillType == "active").ToList(),
                2 => skills.Where(s => s.skillType == "passive").ToList(),
                3 => skills.Where(s => s.skillType == "buff").ToList(),
                _ => skills
            };
        }

        private void OnFilterChanged(int index)
        {
            RefreshSkillList();
        }

        private void OnSkillSelected(SkillTemplate skill)
        {
            selectedSkill = skill;

            // Desmarca outras entradas
            foreach (var entry in skillEntries)
            {
                entry.Deselect();
            }

            ShowSkillDetails(skill);
        }

        private void ShowSkillDetails(SkillTemplate skill)
        {
            if (skillDetailPanel != null)
                skillDetailPanel.SetActive(true);

            if (skillNameText != null)
                skillNameText.text = skill.name;

            if (skillDescriptionText != null)
                skillDescriptionText.text = skill.description;

            // Estatísticas
            if (skillStatsText != null)
            {
                var level1Data = skill.levels.FirstOrDefault(l => l.level == 1);

                string stats = $"<b>Informações:</b>\n";
                stats += $"Tipo: {TranslateSkillType(skill.skillType)}\n";
                stats += $"Alvo: {TranslateTargetType(skill.targetType)}\n";
                stats += $"Custo de Mana: {skill.manaCost}\n";
                stats += $"Cooldown: {skill.cooldown}s\n";
                stats += $"Nível Máximo: {skill.maxLevel}\n\n";

                if (level1Data != null)
                {
                    stats += $"<b>Nível 1:</b>\n";

                    if (level1Data.baseDamage > 0)
                        stats += $"Dano Base: {level1Data.baseDamage}\n";

                    if (level1Data.baseHealing > 0)
                        stats += $"Cura Base: {level1Data.baseHealing}\n";

                    if (level1Data.damageMultiplier > 0)
                        stats += $"Multiplicador: {level1Data.damageMultiplier:F1}x\n";
                }

                skillStatsText.text = stats;
            }

            // Requisitos
            if (skillRequirementsText != null)
            {
                var charData = WorldManager.Instance?.GetLocalCharacterData();

                string reqs = $"<b>Requisitos:</b>\n";
                reqs += $"Nível: {skill.requiredLevel}\n";

                if (!string.IsNullOrEmpty(skill.requiredClass))
                    reqs += $"Classe: {skill.requiredClass}\n";

                // Verifica se atende requisitos
                bool canLearn = true;
                if (charData != null)
                {
                    if (charData.level < skill.requiredLevel)
                    {
                        reqs += $"\n<color=red>❌ Nível insuficiente!</color>";
                        canLearn = false;
                    }

                    if (!string.IsNullOrEmpty(skill.requiredClass) && charData.classe != skill.requiredClass)
                    {
                        reqs += $"\n<color=red>❌ Classe incorreta!</color>";
                        canLearn = false;
                    }
                }

                if (canLearn)
                {
                    reqs += $"\n<color=lime>✅ Você pode aprender esta skill!</color>";
                }

                skillRequirementsText.text = reqs;

                // Ativa/desativa botão
                if (learnButton != null)
                    learnButton.interactable = canLearn;
            }

            // Ícone
            if (skillIconImage != null)
            {
                var sprite = Resources.Load<Sprite>(skill.iconPath);
                if (sprite != null)
                {
                    skillIconImage.sprite = sprite;
                    skillIconImage.enabled = true;
                }
                else
                {
                    skillIconImage.enabled = false;
                }
            }
        }

        private void OnLearnButtonClick()
        {
            if (selectedSkill == null)
            {
                Debug.LogWarning("⚠️ No skill selected");
                return;
            }

            // Abre seletor de slot
            if (SkillSlotSelectorUI.Instance != null)
            {
                SkillSlotSelectorUI.Instance.Show(selectedSkill, OnSlotSelected);
            }
            else
            {
                Debug.LogError("❌ SkillSlotSelectorUI not found!");
            }
        }

        private void OnSlotSelected(int slotNumber)
        {
            if (selectedSkill == null)
                return;

            // Envia request para servidor
            var message = new
            {
                type = "learnSkill",
                skillId = selectedSkill.id,
                slotNumber = slotNumber
            };

            string json = JsonConvert.SerializeObject(message);
            ClientManager.Instance.SendMessage(json);

            Debug.Log($"📚 Learning skill {selectedSkill.name} in slot {slotNumber}");

            // Fecha UI
            Hide();
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

        private void OnDestroy()
        {
            if (MessageHandler.Instance != null)
            {
                MessageHandler.Instance.OnMessageReceived -= HandleServerMessage;
            }
        }
    }
}