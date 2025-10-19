using UnityEngine;
using System.Collections.Generic;

namespace MMOClient.Skills
{
    /// <summary>
    /// Gerenciador de efeitos visuais de skills
    /// </summary>
    public class SkillEffectsManager : MonoBehaviour
    {
        public static SkillEffectsManager Instance { get; private set; }

        [Header("Effect Prefabs")]
        public GameObject defaultSkillEffect;
        public GameObject castingEffect;
        
        [Header("Settings")]
        public float defaultEffectDuration = 2f;

        // Cache de prefabs carregados
        private Dictionary<string, GameObject> effectCache = new Dictionary<string, GameObject>();

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

        /// <summary>
        /// Toca efeito visual de skill
        /// </summary>
        public void PlaySkillEffect(string effectPrefabPath, Vector3 position, Quaternion rotation = default)
        {
            if (string.IsNullOrEmpty(effectPrefabPath))
            {
                PlayDefaultEffect(position);
                return;
            }

            GameObject prefab = LoadEffectPrefab(effectPrefabPath);
            
            if (prefab != null)
            {
                GameObject effectObj = Instantiate(prefab, position, rotation == default ? Quaternion.identity : rotation);
                Destroy(effectObj, defaultEffectDuration);
            }
            else
            {
                PlayDefaultEffect(position);
            }
        }

        /// <summary>
        /// Toca efeito no alvo
        /// </summary>
        public void PlaySkillEffectOnTarget(string effectPrefabPath, Transform target)
        {
            if (target == null)
                return;

            PlaySkillEffect(effectPrefabPath, target.position + Vector3.up * 1.5f);
        }

        /// <summary>
        /// Toca efeito padrão
        /// </summary>
        private void PlayDefaultEffect(Vector3 position)
        {
            if (defaultSkillEffect != null)
            {
                GameObject effectObj = Instantiate(defaultSkillEffect, position, Quaternion.identity);
                Destroy(effectObj, defaultEffectDuration);
            }
        }

        /// <summary>
        /// Toca efeito de casting
        /// </summary>
        public GameObject PlayCastingEffect(Transform caster)
        {
            if (castingEffect == null || caster == null)
                return null;

            GameObject effectObj = Instantiate(castingEffect, caster.position + Vector3.up * 1f, Quaternion.identity);
            effectObj.transform.SetParent(caster);

            return effectObj;
        }

        /// <summary>
        /// Para efeito de casting
        /// </summary>
        public void StopCastingEffect(GameObject effectObj)
        {
            if (effectObj != null)
            {
                Destroy(effectObj);
            }
        }

        /// <summary>
        /// Carrega prefab de efeito
        /// </summary>
        private GameObject LoadEffectPrefab(string path)
        {
            // Verifica cache
            if (effectCache.TryGetValue(path, out GameObject cached))
            {
                return cached;
            }

            // Carrega de Resources
            GameObject prefab = Resources.Load<GameObject>(path);
            
            if (prefab != null)
            {
                effectCache[path] = prefab;
                Debug.Log($"✅ Loaded skill effect: {path}");
            }
            else
            {
                Debug.LogWarning($"⚠️ Skill effect not found: {path}");
            }

            return prefab;
        }

        /// <summary>
        /// Toca som de skill
        /// </summary>
        public void PlaySkillSound(string soundName, Vector3 position)
        {
            if (string.IsNullOrEmpty(soundName))
                return;

            // TODO: Integrar com sistema de áudio
            AudioClip clip = Resources.Load<AudioClip>($"Sounds/Skills/{soundName}");
            
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position);
            }
        }

        /// <summary>
        /// Cria linha de projétil (para skills ranged)
        /// </summary>
        public void CreateProjectile(Vector3 startPos, Vector3 targetPos, string effectPath, float speed = 10f)
        {
            GameObject prefab = LoadEffectPrefab(effectPath);
            
            if (prefab == null)
                return;

            GameObject projectile = Instantiate(prefab, startPos, Quaternion.identity);
            
            // Adiciona componente de movimento
            var mover = projectile.AddComponent<ProjectileMover>();
            mover.Initialize(targetPos, speed, defaultEffectDuration);
        }
    }

    /// <summary>
    /// Componente para mover projéteis
    /// </summary>
    public class ProjectileMover : MonoBehaviour
    {
        private Vector3 target;
        private float speed;
        private float lifetime;
        private float startTime;

        public void Initialize(Vector3 targetPos, float moveSpeed, float maxLifetime)
        {
            target = targetPos;
            speed = moveSpeed;
            lifetime = maxLifetime;
            startTime = Time.time;

            // Rotaciona para o alvo
            Vector3 direction = (target - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void Update()
        {
            // Move em direção ao alvo
            float step = speed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, target, step);

            // Chegou ao alvo?
            if (Vector3.Distance(transform.position, target) < 0.1f)
            {
                Destroy(gameObject);
                return;
            }

            // Timeout
            if (Time.time - startTime > lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}