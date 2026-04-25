# 🗡️ 2D RPG

Ein Multiplayer-fähiges 2D Top-Down RPG, entwickelt mit **Godot 4** und **C#** im Rahmen eines Hochschulprojekts (6. Semester, HS Flensburg).

---

## 🎮 Über das Projekt

Ein handgezeichnetes 2D RPG mit Pixel-Art Grafiken, Echtzeit-Multiplayer über das lokale Netzwerk, animierten Welten und einem wachsenden Kampfsystem.

**Engine:** Godot 4.6 (Mono / C#)  
**Sprache:** C#  
**Repository:** [GitLab – HS Flensburg](https://gitlab.hs-flensburg.de/roja6078/2drpg)

---

## ✅ Bisher umgesetzt

### 🌍 Welt & Map
- Forest Zone mit animierten Bäumen (Idle-Animation, Autoplay)
- Y-Sort korrekt eingerichtet – Spieler verschwindet hinter Bäumen je nach Y-Position
- TileMap mit mehreren Layern:
  - `Ground` – Boden
  - `Ground2` – Wasser, Pfade, Details (z=1, Y-Sort)
  - `Y-sort` – Objekte mit Tiefensortierung (z=2, Y-Sort)
  - `YY-leaves` – Baumkronen / Überhänge (z=3, immer über Spieler)
- Terrain-Blending via TileSet Terrain Sets
- Kollisionsformen auf Tiles (Physics Layer im TileSet)
- Wasser-Animationen, Seerosen, Waldlichtungen

### 🧍 Spieler
- Bewegung (Walk / Run) mit Animationen in alle 4 Richtungen
- Angriffs-System (Basis)
- Roll-Mechanik mit Richtungsanimation
- Multiplayer Authority (jeder Spieler kontrolliert sich selbst)
- Y-Sort korrekt – Spieler sortiert sich korrekt hinter Weltobjects
- Kamera folgt lokalem Spieler (deaktiviert bei Remote-Spielern)

### 🌐 Multiplayer
- ENet-basierter Host/Client Aufbau
- `GameManager` als Autoload-Singleton
- Spieler werden direkt in `Main_World` gespawnt (für korrektes Y-Sort)
- `MultiplayerSpawner` synchronisiert Spieler-Nodes auf Clients
- Position & Animation werden über `MultiplayerSynchronizer` synchronisiert
- Main Menu mit Host- und Join-Button

### 🗺️ Zonen (geplant / in Arbeit)
Folgende Biome sind als Assets / Zonen vorbereitet:
`Forest` · `Grassland` · `Village` · `Coast` · `Swamp` · `Desert` · `Winter` · `Dungeon` · `GlowingCave` · `LavaCave` · `CursedLands` · `SkeletonPoison` · `SkyEndgame`

### 🐾 Mob-System (Basis)
- Abstrakte `MobBase`-Klasse mit `MaxHealth`, `MoveSpeed`, `AttackDamage`
- Kategorien vorbereitet: Monster, NPC, HuntAnimal, FarmAnimal, Human

---

## 📋 To-Do / Roadmap

### 🔧 Gameplay
- [ ] Kampfsystem ausbauen (Hitbox, Schaden, Knockback)
- [ ] Gegner-KI (Patrol, Chase, Angriff)
- [ ] Inventar- und Item-System
- [ ] Equipment / Waffen-System (Waffe am Spieler sichtbar)
- [ ] Erfahrungspunkte & Level-Up System
- [ ] Quests / Aufgaben-System

### 🌍 Welt
- [ ] Weitere Zonen ausbauen und verbinden (Übergänge zwischen Biomen)
- [ ] Dungeon-Layout
- [ ] Interaktive Objekte (Truhen, Türen, NPCs)
- [ ] Tag/Nacht-System

### 🌐 Multiplayer
- [ ] Über LAN hinaus (IP-Eingabe im Menü)
- [ ] Spieler-Liste / HUD mit anderen Spielern
- [ ] Synchronisierung von Mob-Positionen
- [ ] Spieler-Tod & Respawn synchronisiert

### 🎨 UI / HUD
- [ ] Health Bar
- [ ] Inventar-Overlay
- [ ] Minimap
- [ ] Dialog-System für NPCs

---

## 🚀 Projekt starten

**Voraussetzungen:**
- Godot 4.6 (Mono) installiert
- .NET SDK (für C#)

**Schritte:**
1. Repository klonen:
   ```
   git clone https://gitlab.hs-flensburg.de/roja6078/2drpg.git
   ```
2. Projekt in Godot öffnen (`project.godot` in `rpg-2d/`)
3. `GameManager.tscn` als Autoload prüfen (Project → Globals → Autoload)
4. Szene starten → **Host** oder **Join** im Hauptmenü

---

## 👥 Team

Hochschulprojekt – HS Flensburg, 6. Semester
