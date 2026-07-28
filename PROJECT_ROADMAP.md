# RPG 2D – Projektstatus und Roadmap

Stand: 22.07.2026  
Arbeitsbasis: Branch `styles_and_fixes`

Dieses Dokument sammelt die technische Bestandsaufnahme, klare Fehler und die
geplante Reihenfolge der nächsten Features. Es wird während der weiteren
Entwicklung als gemeinsame Checkliste gepflegt.

## Aktueller Projektstand

- Godot 4.6 Mono / C# / .NET 8
- ENet-Multiplayer mit Host und lokalem Join
- Client Prediction und Server Reconciliation für Spielerbewegung
- Inventar mit Grid, Hotbar und Equipment-Slots
- Waffen, Offhand, Consumables und animierte Rüstungs-Layer
- HUD für Leben, Ausdauer und vorbereitete XP-Anzeige
- zwölf registrierte, gestreamte Biome in einem 3x4-Raster
- Runtime-Navigation und grundlegendes Pathfinding
- 84 `MobBase`-Unterklassen und 22 einfache NPC-Skripte
- Charakter-Assets mit 10 Haar-, 7 Augen- und 5 Hautvarianten

## Dringende Fehler

### Sicherheit

- [ ] Persönlichen GitLab-Token aus der Remote-URL entfernen und widerrufen

### Netzwerk und Inventar

- [x] Client bei Verbindungsfehler oder Host-Abbruch sauber ins Hauptmenü führen
- [ ] Sender-ID eines Commands beim RPC-Empfang speichern; nicht später
      `GetRemoteSenderId()` in `_PhysicsProcess()` verwenden
- [ ] Inventar-Reconciliation für Remote-Clients testen
- [ ] Drop-RPC-Authority für client-gesteuerte Player korrigieren
- [ ] Netzwerk-Slotadressen vor jedem Arrayzugriff validieren
- [ ] Offline-Pickups korrekt ins Inventar einfügen
- [ ] Pickup-Einfügung transaktional machen, damit bei vollem Inventar keine
      Teilmenge dupliziert werden kann
- [ ] Mehrere gleichzeitig erreichbare Interaktionsziele unterstützen
- [ ] Pickups über eindeutige Instanz-IDs statt nur über den Szenenpfad verfolgen
- [ ] Entfernte Pickup-IDs beim Start einer neuen Session zurücksetzen

### Spieler und Kampf

- [ ] Angriffscooldown auf `aktueller Tick + Cooldown` setzen
- [ ] Rolle um tatsächliche Bewegung und optional Unverwundbarkeit erweitern
- [ ] Respawn-System ergänzen
- [ ] Rüstungsschutz, Schildwirkung und Ringeffekte implementieren

### Mobs

- [ ] Verdeckte `PlayAnim()`-Methoden entfernen oder korrekt überschreiben
- [ ] Mob-Unterklassen dürfen `MobBase._PhysicsProcess()` nicht umgehen
- [ ] Doppelt abgespielte Todesanimationen entfernen
- [ ] `origin/feature/mob-combat` prüfen und gezielt auf den aktuellen Stand übertragen
- [ ] Mob-KI, Angriff, Schaden, Tod und Multiplayer mit zwei Clients testen

### Ressourcen und Projektpflege

- [ ] Falschen Pfad für `Imp2_Hurt_with_shadow.png` korrigieren
- [ ] Falschen Pfad für `orc1_walk_attack_with_shadow.png` korrigieren
- [ ] UID-Abweichungen in `GameManager.tscn` und `EscapeMenu.tscn` korrigieren
- [ ] Absolute und veraltete Pfade in `gen_frames.py` entfernen
- [ ] IDE-Dateien und `main_player.tscn.backup` aus dem Repository entfernen
- [ ] README an den tatsächlichen Projektstand anpassen
- [ ] Headless-Smoke-Test und erste automatisierte Tests einrichten

## Geplante Features

### 1. Charakteranpassung

- [x] Fenster mit `I` öffnen und schließen
- [x] Haar-, Augen- und Hautfarbe auswählen
- [x] Animierte Face-, Eyes- und Hair-Layer am Player verwenden
- [x] Auswahl lokal speichern
- [x] getrennte lokale Speicherprofile für Host und Join-Client verwenden
- [x] unveränderten Main-Charakter ohne Style-Layer als Standard anbieten
- [x] Auswahl serverseitig validieren und für andere Clients replizieren
- [x] Gameplay-Input blockieren, solange das Fenster offen ist
- [ ] Darstellung und Synchronisierung mit Host plus zweitem Client manuell prüfen

### 2. Mob- und Kampfbasis stabilisieren

- [ ] Gemeinsame Zustandsmaschine für Idle, Wander, Chase, Attack, Hurt und Death
- [ ] Serverautoritärer Schaden und verlässliche Trefferprüfung
- [ ] XP-Ereignis bei eindeutigem Mob-Tod

### 3. Level- und XP-System

- [ ] `PlayerProgress` mit Level, XP und benötigten XP
- [ ] XP-Belohnungen in Mob-Daten auslagern
- [ ] Level-up und skalierbare Attribute
- [ ] XP-Leiste und Levelanzeige im HUD aktivieren
- [ ] Fortschritt speichern und synchronisieren

### 4. Allgemeines Interaktionssystem

- [ ] `IInteractable` beziehungsweise gemeinsame Interaction-Komponente
- [ ] Nächstes gültiges Ziel auswählen und Hinweis anzeigen
- [ ] Entfernung und Berechtigung serverseitig prüfen
- [ ] Basis für Truhen, NPCs, Türen, Händler und Häuser schaffen

### 5. Truhen und Loot

- [ ] Truhenszene mit Closed/Open-State
- [ ] wiederverwendbare `LootTable`-Ressourcen
- [ ] serverseitiges, einmaliges Öffnen
- [ ] Loot korrekt in Inventar oder auf den Boden geben
- [ ] Zustand für Late Join und Savegame speichern

### 6. NPCs, Dialoge und Quests

- [ ] datengetriebene Dialogressourcen
- [ ] Dialogfenster und Antwortoptionen
- [ ] Händler auf das bestehende Itemsystem aufbauen
- [ ] einfache Questzustände und Belohnungen

### 7. Kampf, Waffen und Items erweitern

- [ ] unterschiedliche Waffenarten und Angriffsmuster
- [ ] Fernkampf und Projektile
- [ ] Heil-, Ausdauer- und Effekttränke
- [ ] Statuswerte und zeitlich begrenzte Effekte
- [ ] Item- und Waffenwerte über Ressourcen balancieren

### 8. Biome, Village und Haus

- [ ] biomeigene Objekt-, Mob- und Loot-Konfiguration
- [ ] weitere statische und interaktive Weltobjekte
- [ ] Dungeon in das Weltlayout integrieren
- [ ] Village mit funktionalen NPCs und Gebäuden
- [ ] Innenräume und später ein eigenes Spielerhaus

### 9. Benutzerprofile, Charaktere und Spielstände

- [ ] zunächst lokale Profile mit stabiler Profil-ID einführen
- [ ] später Registrierung und Anmeldung über einen geeigneten Backend-Dienst ergänzen
- [ ] mehrere Charaktere pro Benutzer ermöglichen
- [ ] Aussehen, Inventar, Level und Haus einem Charakter statt einer Peer-ID zuordnen
- [ ] mehrere Welten beziehungsweise Spielstände mit Name, Datum und Vorschaudaten anbieten
- [ ] Weltzustand, Truhen, Pickups, NPCs, Quests, Village und Häuser speichern
- [ ] Savegame-Format versionieren und Migrationen für spätere Updates vorsehen
- [ ] Zugriffsrechte für Weltbesitzer und eingeladene Mitspieler festlegen

### 10. Sitzungsfortsetzung und Host-Migration

- [ ] stabile Spieler-ID unabhängig von der temporären ENet-Peer-ID einführen
- [ ] vollständigen autoritativen Sitzungszustand serialisierbar machen
- [ ] regelmäßig einen Wiederherstellungsstand an mögliche Nachfolge-Hosts senden
- [ ] neuen Host eindeutig wählen, beispielsweise niedrigste verbundene Peer-ID
- [ ] ENet-Verbindung neu aufbauen und verbliebene Clients kontrolliert verbinden
- [ ] Authority, Spieler-Nodes und Besitz nach dem Reconnect neu zuordnen
- [ ] Inventar, Aussehen, Mobs, Pickups und Zonen aus dem letzten Stand herstellen
- [ ] Wiederbeitritt des ehemaligen Hosts als normaler Client testen
- [ ] für Internetspiele NAT/Portfreigabe oder einen Relay-/Lobby-Dienst festlegen

## Branch-Reihenfolge

1. `styles_and_fixes` – aktuelle Style- und Rüstungslayer abschließen
2. `fix/runtime-stability` – klare Laufzeit-, Netzwerk- und Inventarfehler
3. `feature/character-customization` – Charakterfenster und Appearance-Sync
4. `fix/mob-foundation` – Mob-Basisklasse und vorhandenen Combat-Branch bereinigen
5. `feature/level-progression` – XP und Level
6. `feature/interaction-system` – gemeinsame Interaktionsbasis
7. `feature/chests-loot` – Truhen und LootTables
8. `feature/npc-dialogue` – NPC-Dialoge, Händler und Quests
9. `feature/combat-items` – Waffen, Tränke und zusätzliche Items
10. `feature/biome-content` – Biome und Weltobjekte
11. `feature/village-housing` – Village, Innenräume und Haus
12. `feature/player-profiles` – stabile Spieler- und Charakterprofile
13. `feature/save-slots` – mehrere Welten und versionierte Spielstände
14. `feature/session-recovery` – Reconnect, Sitzungsstand und Host-Migration

## Definition of Done pro Feature

- Projekt kompiliert ohne neue Warnungen
- Offline-Modus funktioniert
- Host und mindestens ein Client wurden getestet
- neue Netzwerkdaten werden ausschließlich serverseitig validiert
- Szenen und Ressourcen besitzen keine fehlenden Pfade
- Eingaben funktionieren nicht durch geöffnete UI-Fenster hindurch
- Änderungen bleiben klein, fokussiert und nachvollziehbar
