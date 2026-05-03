"""
Entfernt alle 'Base Animation:*' Tracks aus dem AnimationPlayer in main_player.tscn.
Danach werden die restlichen Tracks neu nummeriert (0, 1, 2, ...).
"""

import re

TSCN_PATH = "Player/Main/main_player.tscn"

def remove_base_animation_tracks(text: str) -> str:
    """
    Verarbeitet den tscn-Text.
    Findet alle Animation-Subresource-Blöcke und entfernt darin
    alle Track-Blöcke, deren 'path' NodePath("Base Animation:...") enthält.
    Nummeriert verbleibende Tracks neu ab 0.
    """

    # Wir teilen den Text an [sub_resource type="Animation" ...] Grenzen auf
    # und verarbeiten jeden Animationsblock separat.

    # Pattern, das einen kompletten Track-Block matched (inkl. mehrzeiliger keys-Dict)
    # tracks/N/<property> = <value>  (mehrere Zeilen, bis nächster tracks/ oder Leerzeile vor [)

    def clean_animation_block(block: str) -> str:
        """
        Entfernt Base Animation Tracks aus einem Animation-Ressource-Text-Block.
        Gibt den bereinigten Block zurück.
        """
        lines = block.split('\n')

        # Schritt 1: Alle Track-Nummern finden, die Base Animation-Pfade haben
        base_anim_track_nums = set()
        for line in lines:
            m = re.match(r'^tracks/(\d+)/path\s*=\s*NodePath\("Base Animation:', line)
            if m:
                base_anim_track_nums.add(int(m.group(1)))

        if not base_anim_track_nums:
            return block  # Nichts zu tun

        # Schritt 2: Zeilen filtern - Base Animation Track-Zeilen entfernen
        # Wir müssen mehrzeilige keys = { ... } korrekt behandeln
        result_lines = []
        skip = False
        brace_depth = 0
        current_track_num = None

        i = 0
        while i < len(lines):
            line = lines[i]

            # Prüfe ob diese Zeile zu einem Track gehört
            m = re.match(r'^tracks/(\d+)/', line)
            if m:
                track_num = int(m.group(1))
                current_track_num = track_num
                skip = track_num in base_anim_track_nums

            if skip:
                # Zähle geschweifte Klammern um mehrzeilige dicts zu überspringen
                brace_depth += line.count('{') - line.count('}')
                if brace_depth < 0:
                    brace_depth = 0
                i += 1
                continue
            else:
                # Wenn wir eine { öffnen, tracked die Tiefe
                # (für nicht-skip Zeilen brauchen wir das nicht)
                result_lines.append(line)
                i += 1
                continue

        cleaned = '\n'.join(result_lines)

        # Schritt 3: Verbleibende Track-Nummern neu nummerieren
        # Finde alle existierenden Track-Nummern
        existing_nums = sorted(set(
            int(m.group(1))
            for m in re.finditer(r'^tracks/(\d+)/', cleaned, re.MULTILINE)
        ))

        # Ersetze von hoch nach niedrig um Konflikte zu vermeiden
        # Zuerst alle auf temporäre Namen setzen, dann final
        temp_cleaned = cleaned
        for old_num in existing_nums:
            temp_cleaned = re.sub(
                r'^tracks/' + str(old_num) + r'/',
                f'tracks/TEMP{existing_nums.index(old_num)}/',
                temp_cleaned,
                flags=re.MULTILINE
            )

        # Dann TEMP-Namen auf finale Nummern
        for new_num in range(len(existing_nums)):
            temp_cleaned = re.sub(
                r'^tracks/TEMP' + str(new_num) + r'/',
                f'tracks/{new_num}/',
                temp_cleaned,
                flags=re.MULTILINE
            )

        return temp_cleaned

    # Teile den Text in Blöcke auf: [sub_resource type="Animation" ...]
    # und verarbeite jeden einzeln

    # Pattern für den Beginn eines Animation-Subresource-Blocks
    anim_start = re.compile(r'(\[sub_resource type="Animation" [^\]]+\]\n)')

    parts = anim_start.split(text)
    # parts = [pre_text, header1, body1, header2, body2, ...]
    # Wenn kein Match: parts = [text]

    result_parts = [parts[0]]

    i = 1
    while i < len(parts):
        header = parts[i]      # [sub_resource type="Animation" ...]
        if i + 1 < len(parts):
            body = parts[i + 1]
        else:
            body = ""

        # Finde das Ende des Body (bis zum nächsten [sub_resource oder [node)
        # Der Body wird bis zur nächsten [ Zeile begrenzt
        # (da anim_start.split() schon trennt, enthält body den Rest bis zur nächsten Animation)

        # Trenne Body in Animationsinhalt und Rest
        # (Rest = nächste [sub_resource oder [node Zeile)
        next_block = re.search(r'\n(?=\[)', body)
        if next_block:
            anim_body = body[:next_block.start() + 1]
            rest = body[next_block.start() + 1:]
        else:
            anim_body = body
            rest = ""

        cleaned_body = clean_animation_block(anim_body)

        result_parts.append(header)
        result_parts.append(cleaned_body)
        if rest:
            result_parts.append(rest)

        i += 2

    return ''.join(result_parts)


def main():
    print(f"Lese {TSCN_PATH}...")
    with open(TSCN_PATH, 'r', encoding='utf-8') as f:
        content = f.read()

    print("Entferne 'Base Animation:*' Tracks aus AnimationPlayer-Animationen...")
    cleaned = remove_base_animation_tracks(content)

    # Zähle wie viele Tracks entfernt wurden
    original_base_tracks = len(re.findall(r'NodePath\("Base Animation:', content))
    cleaned_base_tracks = len(re.findall(r'NodePath\("Base Animation:', cleaned))
    removed = original_base_tracks - cleaned_base_tracks

    print(f"Entfernt: {removed} Base Animation Track-Pfade")

    # Backup
    backup_path = TSCN_PATH + ".backup"
    with open(backup_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Backup gespeichert: {backup_path}")

    # Speichern
    with open(TSCN_PATH, 'w', encoding='utf-8') as f:
        f.write(cleaned)
    print(f"Gespeichert: {TSCN_PATH}")
    print("Fertig!")


if __name__ == "__main__":
    main()
