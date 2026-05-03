#!/usr/bin/env python3
"""Injects SpriteFrames for 'Base Animation' into main_player.tscn"""

TSCN = r"C:\Hochschule\6 Semester\Projekt\2drpg\rpg-2d\Player\Main\main_player.tscn"

# --- texture uid/id mapping ---
TEXTURES = {
    "new_walk":    ("uid://b0jb6koqgdaos", "res://Assets/Charakter/Player/new/man_lvl2_Walk_with_shadow.png"),
    "new_idle":    ("uid://df4acy3dffelb", "res://Assets/Charakter/Player/new/man_lvl2_Idle_with_shadow.png"),
    "new_run":     ("uid://chv0xex8huwr",  "res://Assets/Charakter/Player/new/man_lvl2_Run_with_shadow.png"),
    "new_attack":  ("uid://82vj0lbyyghx",  "res://Assets/Charakter/Player/new/man_lvl2_attack_with_shadow.png"),
    "new_walkatk": ("uid://bcuuoq7vy12vu", "res://Assets/Charakter/Player/new/man_lvl2_Walk_Attack_with_shadow.png"),
    "new_runatk":  ("uid://ckkexilxb8okq", "res://Assets/Charakter/Player/new/man_lvl2_Run_Attack_with_shadow.png"),
    "new_roll":    ("uid://584koom6ljs6",  "res://Assets/Charakter/Player/new/man_lvl2_roll_with_shadow.png"),
    "new_death":   ("uid://d1dxcyvcrmtpn", "res://Assets/Charakter/Player/new/man_lvl2_Death_with_shadow.png"),
    "new_hurt":    ("uid://cdagiag3ea10s", "res://Assets/Charakter/Player/new/man_lvl2_Hurt_with_shadow.png"),
}

DIRS = [("down","d",0), ("left","l",1), ("right","r",2), ("up","u",3)]

# (short_id, tex_key, anim_prefix, frames_per_dir, speed)
REGULAR = [
    ("wk",  "new_walk",    "walk",        6, 5.0),
    ("rn",  "new_run",     "run",         8, 9.0),
    ("at",  "new_attack",  "attack",      8, 12.0),
    ("wa",  "new_walkatk", "walk_attack", 6, 10.0),
    ("ra",  "new_runatk",  "run_attack",  8, 10.0),
    ("ro",  "new_roll",    "roll",        8, 10.0),
    ("dt",  "new_death",   "death",       7, 8.0),
    ("hu",  "new_hurt",    "hurt",        5, 6.0),
]

def at_id(short, dchar, fi):
    return f"n_{short}_{dchar}_{fi}"

def build_atlas_blocks():
    lines = []
    for (short, tex_key, _, fc, _) in REGULAR:
        for (_, dchar, didx) in DIRS:
            for fi in range(fc):
                lines.append(f'[sub_resource type="AtlasTexture" id="{at_id(short,dchar,fi)}"]')
                lines.append(f'atlas = ExtResource("{tex_key}")')
                lines.append(f'region = Rect2({fi*64}, {didx*64}, 64, 64)')
                lines.append('')
    # idle down/left/right: 12 frames
    for (_, dchar, didx) in DIRS[:3]:
        for fi in range(12):
            lines.append(f'[sub_resource type="AtlasTexture" id="{at_id("id",dchar,fi)}"]')
            lines.append(f'atlas = ExtResource("new_idle")')
            lines.append(f'region = Rect2({fi*64}, {didx*64}, 64, 64)')
            lines.append('')
    # idle up: 4 frames
    for fi in range(4):
        lines.append(f'[sub_resource type="AtlasTexture" id="{at_id("id","u",fi)}"]')
        lines.append(f'atlas = ExtResource("new_idle")')
        lines.append(f'region = Rect2({fi*64}, {3*64}, 64, 64)')
        lines.append('')
    return '\n'.join(lines)

def frame_entry(short, dchar, fi, dur=1.0):
    return f'{{\n"duration": {dur},\n"texture": SubResource("{at_id(short,dchar,fi)}")\n}}'

def build_anim_entry(name, frames_list, speed):
    frames_str = ', '.join(frames_list)
    return f'{{\n"frames": [{frames_str}],\n"loop": false,\n"name": &"{name}",\n"speed": {speed}\n}}'

def build_all_anim_entries():
    entries = {}
    for (short, _, prefix, fc, speed) in REGULAR:
        for (dname, dchar, _) in DIRS:
            name = f"{prefix}_{dname}"
            flist = [frame_entry(short, dchar, fi) for fi in range(fc)]
            entries[name] = build_anim_entry(name, flist, speed)
    # idle down/left/right
    idle_durs = [20.0,1.0,1.0,1.0,20.0,1.0,1.0,1.0,1.0,1.0,1.0,1.0]
    for (dname, dchar, _) in DIRS[:3]:
        name = f"idle_{dname}"
        flist = [frame_entry("id", dchar, fi, idle_durs[fi]) for fi in range(12)]
        entries[name] = build_anim_entry(name, flist, 5.0)
    # idle up
    flist = [frame_entry("id","u",fi, [15.0,1.0,1.0,1.0][fi]) for fi in range(4)]
    entries["idle_up"] = build_anim_entry("idle_up", flist, 3.0)
    return entries

def build_sprite_frames(anim_entries):
    sorted_entries = [anim_entries[k] for k in sorted(anim_entries.keys())]
    joined = ', '.join(sorted_entries)
    lines = [
        '[sub_resource type="SpriteFrames" id="SpriteFrames_rkm3v"]',
        f'animations = [{joined}]',
        ''
    ]
    return '\n'.join(lines)

def build_ext_resources():
    lines = []
    for key, (uid, path) in TEXTURES.items():
        lines.append(f'[ext_resource type="Texture2D" uid="{uid}" path="{path}" id="{key}"]')
    return '\n'.join(lines) + '\n'

def main():
    with open(TSCN, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Add ext_resources after last existing ext_resource line
    ext_marker = '[ext_resource type="Script" uid="uid://cg1uqlqcfps31" path="res://Player/PlayerInput.cs" id="12_rkm3v"]'
    new_ext = ext_marker + '\n' + build_ext_resources()
    content = content.replace(ext_marker, new_ext)

    # 2. Replace empty SpriteFrames with full content (prepend atlas blocks)
    old_sf = '[sub_resource type="SpriteFrames" id="SpriteFrames_rkm3v"]'
    anim_entries = build_all_anim_entries()
    atlas_blocks = build_atlas_blocks()
    full_sf = build_sprite_frames(anim_entries)
    content = content.replace(old_sf, atlas_blocks + '\n' + full_sf)

    with open(TSCN, 'w', encoding='utf-8') as f:
        f.write(content)

    print("Done! SpriteFrames injected into main_player.tscn")

if __name__ == "__main__":
    main()
