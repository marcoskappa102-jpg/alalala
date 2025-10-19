-- ============================================
-- SCRIPT COMPLETO DE BANCO DE DADOS - MMORPG
-- ============================================
-- Este script:
-- 1. Remove o banco antigo (se existir)
-- 2. Cria um banco novo
-- 3. Cria todas as tabelas necessárias
-- 4. Insere dados iniciais de monstros
-- 5. Cria conta de teste (opcional)
-- 6. Adiciona suporte a STATUS POINTS
-- ============================================

-- ====================
-- PASSO 1: REMOVER BANCO ANTIGO
-- ====================
DROP DATABASE IF EXISTS mmo_game;

-- ====================
-- PASSO 2: CRIAR BANCO NOVO
-- ====================
CREATE DATABASE mmo_game CHARACTER SET utf8mb4 COLLATE=utf8mb4_unicode_ci;
USE mmo_game;

-- ====================
-- PASSO 3: CRIAR TABELAS
-- ====================

-- Tabela de Contas de Usuário
CREATE TABLE accounts (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP NULL,
    INDEX idx_username (username)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Personagens
CREATE TABLE characters (
    id INT AUTO_INCREMENT PRIMARY KEY,
    account_id INT NOT NULL,
    nome VARCHAR(50) NOT NULL,
    raca VARCHAR(50) NOT NULL,
    classe VARCHAR(50) NOT NULL,
    
    -- Level e Experiência
    level INT DEFAULT 1,
    experience INT DEFAULT 0,

    -- Pontos de Status (adicionado posteriormente)
    status_points INT DEFAULT 0,
    
    -- Vida e Mana
    health INT DEFAULT 100,
    max_health INT DEFAULT 100,
    mana INT DEFAULT 100,
    max_mana INT DEFAULT 100,
    
    -- Atributos Base
    strength INT DEFAULT 10,
    intelligence INT DEFAULT 10,
    dexterity INT DEFAULT 10,
    vitality INT DEFAULT 10,
    
    -- Atributos Calculados
    attack_power INT DEFAULT 10,
    magic_power INT DEFAULT 10,
    defense INT DEFAULT 5,
    attack_speed FLOAT DEFAULT 1.0,
    
    -- Posição no Mundo
    pos_x FLOAT DEFAULT 0,
    pos_y FLOAT DEFAULT 0,
    pos_z FLOAT DEFAULT 0,
    
    -- Estado
    is_dead BOOLEAN DEFAULT FALSE,
    
    -- Timestamps
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP NULL,
    
    FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE,
    INDEX idx_account (account_id),
    INDEX idx_nome (nome)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Templates de Monstros
CREATE TABLE monster_templates (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    level INT NOT NULL,
    max_health INT NOT NULL,
    attack_power INT NOT NULL,
    defense INT NOT NULL,
    experience_reward INT NOT NULL,
    attack_speed FLOAT DEFAULT 1.5,
    movement_speed FLOAT DEFAULT 3.0,
    aggro_range FLOAT DEFAULT 10.0,
    
    -- Spawn Settings
    spawn_x FLOAT NOT NULL,
    spawn_y FLOAT NOT NULL,
    spawn_z FLOAT NOT NULL,
    spawn_radius FLOAT DEFAULT 5.0,
    respawn_time INT DEFAULT 30,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Instâncias de Monstros
CREATE TABLE monster_instances (
    id INT AUTO_INCREMENT PRIMARY KEY,
    template_id INT NOT NULL,
    current_health INT NOT NULL,
    pos_x FLOAT NOT NULL,
    pos_y FLOAT NOT NULL,
    pos_z FLOAT NOT NULL,
    is_alive BOOLEAN DEFAULT TRUE,
    last_respawn TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (template_id) REFERENCES monster_templates(id) ON DELETE CASCADE,
    INDEX idx_template (template_id),
    INDEX idx_alive (is_alive)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Log de Combate
CREATE TABLE combat_log (
    id INT AUTO_INCREMENT PRIMARY KEY,
    character_id INT NULL,
    monster_id INT NULL,
    damage_dealt INT NOT NULL,
    damage_type VARCHAR(20) DEFAULT 'physical',
    is_critical BOOLEAN DEFAULT FALSE,
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE SET NULL,
    INDEX idx_character (character_id),
    INDEX idx_timestamp (timestamp)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ====================
-- PASSO 4: INSERIR DADOS DE MONSTROS
-- ====================

INSERT INTO monster_templates (name, level, max_health, attack_power, defense, experience_reward, attack_speed, movement_speed, aggro_range, spawn_x, spawn_y, spawn_z, spawn_radius, respawn_time) VALUES
('Lobo Selvagem', 1, 50, 8, 2, 15, 1.5, 4.0, 8.0, 10, 0, 10, 8.0, 30),
('Lobo Selvagem', 1, 50, 8, 2, 15, 1.5, 4.0, 8.0, -10, 0, -10, 8.0, 30),
('Goblin Explorador', 2, 80, 12, 3, 25, 1.8, 3.5, 10.0, 15, 0, 0, 10.0, 40),
('Goblin Explorador', 2, 80, 12, 3, 25, 1.8, 3.5, 10.0, 0, 0, 15, 10.0, 40),
('Javali Raivoso', 3, 120, 15, 5, 40, 2.0, 3.0, 7.0, 20, 0, 20, 12.0, 45),
('Aranha Gigante', 2, 70, 10, 2, 20, 1.6, 3.8, 9.0, 50, 0, 40, 10.0, 35),
('Corvo Sombrio', 3, 90, 14, 3, 30, 1.4, 5.0, 12.0, 45, 0, 55, 15.0, 40),
('Lobo das Sombras', 4, 150, 20, 6, 55, 1.7, 4.5, 10.0, 55, 0, 45, 12.0, 50);

INSERT INTO monster_instances (template_id, current_health, pos_x, pos_y, pos_z, is_alive)
SELECT id, max_health, spawn_x, spawn_y, spawn_z, TRUE FROM monster_templates;

-- ====================
-- PASSO 5: DADOS DE TESTE
-- ====================

INSERT INTO accounts (username, password) VALUES 
('admin', 'admin123'),

INSERT INTO characters (account_id, nome, raca, classe, level, experience, status_points, health, max_health, mana, max_mana, strength, intelligence, dexterity, vitality, attack_power, magic_power, defense, attack_speed, pos_x, pos_y, pos_z, is_dead)
VALUES 
(1, 'Heroi', 'Humano', 'Guerreiro', 1, 0, 0, 160, 160, 74, 74, 15, 8, 10, 12, 40, 26, 17, 1.2, 0, 0, 0, FALSE),

-- ====================
-- PASSO 6: VERIFICAÇÃO
-- ====================
SELECT 'Tabelas criadas com sucesso!' AS Status;

SELECT 
    'accounts' AS Tabela, COUNT(*) AS Total FROM accounts
UNION ALL SELECT 'characters', COUNT(*) FROM characters
UNION ALL SELECT 'monster_templates', COUNT(*) FROM monster_templates
UNION ALL SELECT 'monster_instances', COUNT(*) FROM monster_instances;
-- Verificar tabelas criadas
SHOW TABLES;

-- Verificar estrutura das tabelas
DESCRIBE accounts;
DESCRIBE characters;

SELECT 'Database setup completed successfully!' AS Status;







-- ============================================
-- ATUALIZAÇÃO DO BANCO PARA SISTEMA DE ITENS
-- ============================================
-- Execute este script no seu banco mmo_game

USE mmo_game;

-- ====================
-- TABELA DE INVENTÁRIOS
-- ====================
CREATE TABLE IF NOT EXISTS inventories (
    character_id INT PRIMARY KEY,
    max_slots INT DEFAULT 50,
    gold INT DEFAULT 0,
    weapon_id INT NULL,
    armor_id INT NULL,
    helmet_id INT NULL,
    boots_id INT NULL,
    gloves_id INT NULL,
    ring_id INT NULL,
    necklace_id INT NULL,
    FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE,
    INDEX idx_character (character_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ====================
-- TABELA DE INSTÂNCIAS DE ITENS
-- ====================
CREATE TABLE IF NOT EXISTS item_instances (
    instance_id INT PRIMARY KEY,
    character_id INT NOT NULL,
    template_id INT NOT NULL,
    quantity INT DEFAULT 1,
    slot INT DEFAULT -1,
    is_equipped BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE,
    INDEX idx_character (character_id),
    INDEX idx_template (template_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ====================
-- TABELA DE CONTADOR DE IDS
-- ====================
CREATE TABLE IF NOT EXISTS item_id_counter (
    id INT PRIMARY KEY DEFAULT 1,
    next_instance_id INT DEFAULT 1
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Inicializa contador se não existir
INSERT IGNORE INTO item_id_counter (id, next_instance_id) VALUES (1, 1);

-- ====================
-- CRIA INVENTÁRIOS PARA PERSONAGENS EXISTENTES
-- ====================
INSERT INTO inventories (character_id, max_slots, gold)
SELECT id, 50, 100
FROM characters
WHERE id NOT IN (SELECT character_id FROM inventories);

-- ====================
-- ADICIONA ITENS INICIAIS (OPCIONAL)
-- ====================
-- Dá 5 poções de vida pequena para cada personagem
INSERT INTO item_instances (instance_id, character_id, template_id, quantity, slot, is_equipped)
SELECT 
    (SELECT next_instance_id FROM item_id_counter) + ROW_NUMBER() OVER (ORDER BY id) - 1 as instance_id,
    id as character_id,
    1 as template_id, -- Poção de Vida Pequena
    5 as quantity,
    0 as slot,
    FALSE as is_equipped
FROM characters
WHERE id NOT IN (
    SELECT character_id FROM item_instances WHERE template_id = 1
);

-- Atualiza contador
UPDATE item_id_counter 
SET next_instance_id = next_instance_id + (SELECT COUNT(*) FROM characters);

-- ====================
-- VERIFICAÇÃO
-- ====================
SELECT 'Inventories created:' AS Status, COUNT(*) AS Total FROM inventories;
SELECT 'Item instances created:' AS Status, COUNT(*) AS Total FROM item_instances;
SELECT 'Next instance ID:' AS Status, next_instance_id AS Value FROM item_id_counter;

SELECT 
    c.nome as Character,
    i.gold as Gold,
    COUNT(ii.instance_id) as Items
FROM characters c
LEFT JOIN inventories i ON c.id = i.character_id
LEFT JOIN item_instances ii ON c.id = ii.character_id
GROUP BY c.id, c.nome, i.gold
ORDER BY c.nome;

SELECT '✅ Item system database update completed!' AS Status;




-- Tabela de skills aprendidas por personagens
CREATE TABLE IF NOT EXISTS character_skills (
    id INT AUTO_INCREMENT PRIMARY KEY,
    character_id INT NOT NULL,
    skill_id INT NOT NULL,
    current_level INT DEFAULT 1,
    slot_number INT DEFAULT 0,
    last_used_time BIGINT DEFAULT 0,
    FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE,
    INDEX idx_character (character_id),
    INDEX idx_skill (skill_id),
    UNIQUE KEY unique_character_skill (character_id, skill_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SELECT '✅ Skills table created successfully!' AS Status;