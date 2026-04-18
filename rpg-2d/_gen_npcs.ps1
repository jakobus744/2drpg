# === NPC Scene Generator ===
# Creates scenes in Mobs/NPC/ for all NPCs from:
#   Assets/Charakter/citizens/  (Citizens 1-5, Alchemist, Traders, etc.)
#   Assets/Charakter/NPC/       (Herbalist, Mages, OldMan, Boy, Guildmaster, Fisherman, Cobold)
param([switch]$DryRun)

$BASE   = "C:\Hochschule\6 Semester\Projekt\2drpg\rpg-2d"
$MOBS   = "$BASE\Mobs"
$ASSETS = "res://Assets"
$NoBom  = New-Object System.Text.UTF8Encoding $false

function Write-Scene($path, $content) {
    if ($DryRun) { Write-Host "[DRY] $path"; return }
    $dir = Split-Path $path -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($path, $content, $NoBom)
    Write-Host "OK  $path"
}

# ---- frame helpers ----
function DirAtlas($animKey, $dir, $extId, $n, $fw, $fh, $row) {
    $sb = [System.Text.StringBuilder]::new()
    $y  = $row * $fh
    for ($i = 0; $i -lt $n; $i++) {
        $null = $sb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_${animKey}_${dir}_${i}`"]")
        $null = $sb.AppendLine("atlas = ExtResource(`"$extId`")")
        $null = $sb.AppendLine("region = Rect2($($i*$fw), $y, $fw, $fh)")
        $null = $sb.AppendLine("")
    }
    $sb.ToString()
}

function VertAtlas($animKey, $extId, $n, $fw, $fh) {
    # Vertical strip: x=0, y=i*fh
    $sb = [System.Text.StringBuilder]::new()
    for ($i = 0; $i -lt $n; $i++) {
        $null = $sb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_${animKey}_down_${i}`"]")
        $null = $sb.AppendLine("atlas = ExtResource(`"$extId`")")
        $null = $sb.AppendLine("region = Rect2(0, $($i*$fh), $fw, $fh)")
        $null = $sb.AppendLine("")
    }
    $sb.ToString()
}

function AnimDict($animKey, $dir, $n, $loop, $spd) {
    $ls = if ($loop) { "true" } else { "false" }
    $frames = (0..($n-1) | ForEach-Object {
        "{`"duration`": 1.0, `"texture`": SubResource(`"AT_${animKey}_${dir}_$_`")}"
    }) -join ", "
    "{`"frames`": [$frames], `"loop`": $ls, `"name`": &`"${animKey}_${dir}`", `"speed`": $spd}"
}

function NpcCs($name) {
    @"
using Godot;

public partial class $name : CharacterBody2D
{
    private AnimatedSprite2D _sprite;
    private string _dir = "down";

    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        // Play first available animation
        if (_sprite.SpriteFrames.HasAnimation("idle_down"))
            _sprite.Play("idle_down");
        else if (_sprite.SpriteFrames.HasAnimation("idle"))
            _sprite.Play("idle");
    }

    public void PlayAnim(string anim)
    {
        string full = anim + "_" + _dir;
        if (_sprite.SpriteFrames.HasAnimation(full))
            _sprite.Play(full);
        else if (_sprite.SpriteFrames.HasAnimation(anim))
            _sprite.Play(anim);
    }
}
"@
}

# ============================================================
# Build scene with multiple animations in multiple directions
# ============================================================
function Build-NpcScene($name, $uid, $scriptResPath, $anims, $dirs, $capR, $capH) {
    $sb = [System.Text.StringBuilder]::new()
    $null = $sb.AppendLine("[gd_scene format=4 uid=`"uid://${uid}`"]")
    $null = $sb.AppendLine("")
    foreach ($a in $anims) {
        $null = $sb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$($a.path)`" id=`"$($a.extId)`"]")
    }
    $null = $sb.AppendLine("[ext_resource type=`"Script`" path=`"$scriptResPath`" id=`"scr_$name`"]")
    $null = $sb.AppendLine("")
    for ($di = 0; $di -lt $dirs.Count; $di++) {
        $dir = $dirs[$di]
        foreach ($a in $anims) {
            $null = $sb.Append((DirAtlas $a.key $dir $a.extId $a.n $a.fw $a.fh $di))
        }
    }
    $sfId  = "SF_$name"
    $capId = "Cap_$name"
    $null = $sb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"$sfId`"]")
    $entries = @()
    foreach ($dir in $dirs) {
        foreach ($a in $anims) {
            $entries += AnimDict $a.key $dir $a.n $a.loop $a.spd
        }
    }
    $null = $sb.AppendLine("animations = [$($entries -join ', ')]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[sub_resource type=`"CapsuleShape2D`" id=`"$capId`"]")
    $null = $sb.AppendLine("radius = $capR")
    $null = $sb.AppendLine("height = $capH")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"$name`" type=`"CharacterBody2D`"]")
    $null = $sb.AppendLine("script = ExtResource(`"scr_$name`")")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
    $null = $sb.AppendLine("sprite_frames = SubResource(`"$sfId`")")
    $null = $sb.AppendLine("animation = &`"$($anims[0].key)_$($dirs[0])`"")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"CollisionShape2D`" type=`"CollisionShape2D`" parent=`".`"]")
    $null = $sb.AppendLine("shape = SubResource(`"$capId`")")
    $sb.ToString()
}

# Single-animation, single-direction scene (for static NPCs)
function Build-StaticNpcScene($name, $uid, $scriptResPath, $animKey, $texPath, $n, $fw, $fh, $capR, $capH) {
    $sb = [System.Text.StringBuilder]::new()
    $null = $sb.AppendLine("[gd_scene format=4 uid=`"uid://${uid}`"]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$texPath`" id=`"tex_main`"]")
    $null = $sb.AppendLine("[ext_resource type=`"Script`" path=`"$scriptResPath`" id=`"scr_$name`"]")
    $null = $sb.AppendLine("")
    for ($i = 0; $i -lt $n; $i++) {
        $null = $sb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_${animKey}_down_${i}`"]")
        $null = $sb.AppendLine("atlas = ExtResource(`"tex_main`")")
        $null = $sb.AppendLine("region = Rect2($($i*$fw), 0, $fw, $fh)")
        $null = $sb.AppendLine("")
    }
    $sfId  = "SF_$name"
    $capId = "Cap_$name"
    $frames = (0..($n-1) | ForEach-Object {
        "{`"duration`": 1.0, `"texture`": SubResource(`"AT_${animKey}_down_$_`")}"
    }) -join ", "
    $null = $sb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"$sfId`"]")
    $null = $sb.AppendLine("animations = [{`"frames`": [$frames], `"loop`": true, `"name`": &`"${animKey}_down`", `"speed`": 8.0}]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[sub_resource type=`"CapsuleShape2D`" id=`"$capId`"]")
    $null = $sb.AppendLine("radius = $capR")
    $null = $sb.AppendLine("height = $capH")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"$name`" type=`"CharacterBody2D`"]")
    $null = $sb.AppendLine("script = ExtResource(`"scr_$name`")")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
    $null = $sb.AppendLine("sprite_frames = SubResource(`"$sfId`")")
    $null = $sb.AppendLine("animation = &`"${animKey}_down`"")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"CollisionShape2D`" type=`"CollisionShape2D`" parent=`".`"]")
    $null = $sb.AppendLine("shape = SubResource(`"$capId`")")
    $sb.ToString()
}

$D4 = [System.Collections.Generic.List[string]]@("down","up","left","right")
$D3 = [System.Collections.Generic.List[string]]@("down","left","right")
$D1 = [System.Collections.Generic.List[string]]@("down")

$NPC_BASE = "$MOBS\NPC"

# ============================================================
# Citizens 1-5  (Assets/Charakter/citizens/)
# Idle: 384x128, fw=32, fh=32, 4-dir, n=12
# Walk: 192x128, fw=32, fh=32, 4-dir, n=6
# ============================================================
$citizensBase = "Charakter/citizens"
for ($i = 1; $i -le 5; $i++) {
    $name   = "Citizen$i"
    $uid    = "npc_citizen$i"
    $folder = "$NPC_BASE\$name"
    $scriptRes = "res://Mobs/NPC/$name/$name.cs"
    $anims = @(
        @{key="idle"; extId="tex_idle"; path="$ASSETS/$citizensBase/${name}_Idle_with_shadow.png";  n=12; fw=32; fh=32; loop=$true;  spd=8}
        @{key="walk"; extId="tex_walk"; path="$ASSETS/$citizensBase/${name}_Walk_with_shadow.png";  n=6;  fw=32; fh=32; loop=$true;  spd=8}
    )
    Write-Scene "$folder\$name.tscn" (Build-NpcScene $name $uid $scriptRes $anims $D4 6 14)
    Write-Scene "$folder\$name.cs"   (NpcCs $name)
}

# ============================================================
# Herbalist (Assets/Charakter/NPC/)
# Idle+Walk: 192x128, fw=32, fh=32, 4-dir, n=6
# ============================================================
$npcBase = "Charakter/NPC"
$herbAnims = @(
    @{key="idle"; extId="tex_idle"; path="$ASSETS/$npcBase/Herbalist_Idle.png"; n=6; fw=32; fh=32; loop=$true; spd=8}
    @{key="walk"; extId="tex_walk"; path="$ASSETS/$npcBase/Herbalist_Walk.png"; n=6; fw=32; fh=32; loop=$true; spd=8}
)
$herbScript = "res://Mobs/NPC/Herbalist/Herbalist.cs"
Write-Scene "$NPC_BASE\Herbalist\Herbalist.tscn" (Build-NpcScene "Herbalist" "npc_herbalist" $herbScript $herbAnims $D4 6 14)
Write-Scene "$NPC_BASE\Herbalist\Herbalist.cs"   (NpcCs "Herbalist")

# ============================================================
# Mage1 — 384x32 single row, fw=32, fh=32, n=12, facing down
# ============================================================
$m1Script = "res://Mobs/NPC/Mage1/Mage1.cs"
Write-Scene "$NPC_BASE\Mage1\Mage1.tscn" (Build-StaticNpcScene "Mage1" "npc_mage1" $m1Script "idle" "$ASSETS/$npcBase/Mage1.png" 12 32 32 6 14)
Write-Scene "$NPC_BASE\Mage1\Mage1.cs"   (NpcCs "Mage1")

# Mage3 — 576x48 single row, fw=48, fh=48, n=12
$m3Script = "res://Mobs/NPC/Mage3/Mage3.cs"
Write-Scene "$NPC_BASE\Mage3\Mage3.tscn" (Build-StaticNpcScene "Mage3" "npc_mage3" $m3Script "idle" "$ASSETS/$npcBase/Mage3.png" 12 48 48 8 18)
Write-Scene "$NPC_BASE\Mage3\Mage3.cs"   (NpcCs "Mage3")

# Mage4 — 288x32 single row, fw=32, fh=32, n=9
$m4Script = "res://Mobs/NPC/Mage4/Mage4.cs"
Write-Scene "$NPC_BASE\Mage4\Mage4.tscn" (Build-StaticNpcScene "Mage4" "npc_mage4" $m4Script "idle" "$ASSETS/$npcBase/Mage4_without_shadow.png" 9 32 32 6 14)
Write-Scene "$NPC_BASE\Mage4\Mage4.cs"   (NpcCs "Mage4")

# ============================================================
# OldMan — 320x32 single row, fw=32, fh=32, n=10 — two animations (idle, orders)
# ============================================================
$oldManSb = [System.Text.StringBuilder]::new()
$null = $oldManSb.AppendLine("[gd_scene format=4 uid=`"uid://npc_oldman`"]")
$null = $oldManSb.AppendLine("")
$null = $oldManSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$npcBase/Old_man_idle.png`" id=`"tex_idle`"]")
$null = $oldManSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$npcBase/Old_man_orders.png`" id=`"tex_orders`"]")
$null = $oldManSb.AppendLine("[ext_resource type=`"Script`" path=`"res://Mobs/NPC/OldMan/OldMan.cs`" id=`"scr_OldMan`"]")
$null = $oldManSb.AppendLine("")
foreach ($aKey in @("idle","orders")) {
    $extId = "tex_$aKey"
    for ($i = 0; $i -lt 10; $i++) {
        $null = $oldManSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_${aKey}_down_${i}`"]")
        $null = $oldManSb.AppendLine("atlas = ExtResource(`"$extId`")")
        $null = $oldManSb.AppendLine("region = Rect2($($i*32), 0, 32, 32)")
        $null = $oldManSb.AppendLine("")
    }
}
$null = $oldManSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_OldMan`"]")
$idleFrames   = (0..9 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_idle_down_$_`")}" }) -join ", "
$ordersFrames = (0..9 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_orders_down_$_`")}" }) -join ", "
$entries = @(
    "{`"frames`": [$idleFrames], `"loop`": true, `"name`": &`"idle_down`", `"speed`": 8.0}"
    "{`"frames`": [$ordersFrames], `"loop`": true, `"name`": &`"orders_down`", `"speed`": 8.0}"
)
$null = $oldManSb.AppendLine("animations = [$($entries -join ', ')]")
$null = $oldManSb.AppendLine("")
$null = $oldManSb.AppendLine("[sub_resource type=`"CapsuleShape2D`" id=`"Cap_OldMan`"]")
$null = $oldManSb.AppendLine("radius = 6")
$null = $oldManSb.AppendLine("height = 14")
$null = $oldManSb.AppendLine("")
$null = $oldManSb.AppendLine("[node name=`"OldMan`" type=`"CharacterBody2D`"]")
$null = $oldManSb.AppendLine("script = ExtResource(`"scr_OldMan`")")
$null = $oldManSb.AppendLine("")
$null = $oldManSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $oldManSb.AppendLine("sprite_frames = SubResource(`"SF_OldMan`")")
$null = $oldManSb.AppendLine("animation = &`"idle_down`"")
$null = $oldManSb.AppendLine("")
$null = $oldManSb.AppendLine("[node name=`"CollisionShape2D`" type=`"CollisionShape2D`" parent=`".`"]")
$null = $oldManSb.AppendLine("shape = SubResource(`"Cap_OldMan`")")
Write-Scene "$NPC_BASE\OldMan\OldMan.tscn" $oldManSb.ToString()
Write-Scene "$NPC_BASE\OldMan\OldMan.cs"   (NpcCs "OldMan")

# ============================================================
# Boy — 384x48 single row, fw=48, fh=48, n=8 — three animations (amazed, weaves, looking)
# ============================================================
$boySb = [System.Text.StringBuilder]::new()
$null = $boySb.AppendLine("[gd_scene format=4 uid=`"uid://npc_boy`"]")
$null = $boySb.AppendLine("")
foreach ($ak in @("amazed","weaves","looking")) {
    $fname = if ($ak -eq "amazed") { "Boy_amazed" } elseif ($ak -eq "weaves") { "Boy_weaves" } else { "Boy_looking" }
    $null = $boySb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$npcBase/$fname.png`" id=`"tex_$ak`"]")
}
$null = $boySb.AppendLine("[ext_resource type=`"Script`" path=`"res://Mobs/NPC/Boy/Boy.cs`" id=`"scr_Boy`"]")
$null = $boySb.AppendLine("")
foreach ($ak in @("amazed","weaves","looking")) {
    for ($i = 0; $i -lt 8; $i++) {
        $null = $boySb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_${ak}_down_${i}`"]")
        $null = $boySb.AppendLine("atlas = ExtResource(`"tex_$ak`")")
        $null = $boySb.AppendLine("region = Rect2($($i*48), 0, 48, 48)")
        $null = $boySb.AppendLine("")
    }
}
$null = $boySb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_Boy`"]")
$boyEntries = @("amazed","weaves","looking") | ForEach-Object {
    $ak = $_
    $fr = (0..7 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_${ak}_down_$_`")}" }) -join ", "
    "{`"frames`": [$fr], `"loop`": true, `"name`": &`"${ak}_down`", `"speed`": 8.0}"
}
$null = $boySb.AppendLine("animations = [$($boyEntries -join ', ')]")
$null = $boySb.AppendLine("")
$null = $boySb.AppendLine("[sub_resource type=`"CapsuleShape2D`" id=`"Cap_Boy`"]")
$null = $boySb.AppendLine("radius = 6")
$null = $boySb.AppendLine("height = 12")
$null = $boySb.AppendLine("")
$null = $boySb.AppendLine("[node name=`"Boy`" type=`"CharacterBody2D`"]")
$null = $boySb.AppendLine("script = ExtResource(`"scr_Boy`")")
$null = $boySb.AppendLine("")
$null = $boySb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $boySb.AppendLine("sprite_frames = SubResource(`"SF_Boy`")")
$null = $boySb.AppendLine("animation = &`"amazed_down`"")
$null = $boySb.AppendLine("")
$null = $boySb.AppendLine("[node name=`"CollisionShape2D`" type=`"CollisionShape2D`" parent=`".`"]")
$null = $boySb.AppendLine("shape = SubResource(`"Cap_Boy`")")
Write-Scene "$NPC_BASE\Boy\Boy.tscn" $boySb.ToString()
Write-Scene "$NPC_BASE\Boy\Boy.cs"   (NpcCs "Boy")

# ============================================================
# Guildmaster — 128x128, fw=32, fh=32, 4 dirs, n=4
# ============================================================
$gmAnims = @(
    @{key="idle"; extId="tex_idle"; path="$ASSETS/$npcBase/Guildmaster.png"; n=4; fw=32; fh=32; loop=$true; spd=6}
)
$gmScript = "res://Mobs/NPC/Guildmaster/Guildmaster.cs"
Write-Scene "$NPC_BASE\Guildmaster\Guildmaster.tscn" (Build-NpcScene "Guildmaster" "npc_guildmaster" $gmScript $gmAnims $D4 8 18)
Write-Scene "$NPC_BASE\Guildmaster\Guildmaster.cs"   (NpcCs "Guildmaster")

# ============================================================
# Fisherman — 320x240 boat animation
# Treated as 2 single-dir (down) animations: idle + throws
# fw=32, fh=48, n=10 (320/32=10 cols), using only first row (y=0..48)
# ============================================================
$fishSb = [System.Text.StringBuilder]::new()
$null = $fishSb.AppendLine("[gd_scene format=4 uid=`"uid://npc_fisherman`"]")
$null = $fishSb.AppendLine("")
$null = $fishSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$npcBase/Fisherman_boat1_idle.png`" id=`"tex_idle`"]")
$null = $fishSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$npcBase/Fisherman_boat1_throws.png`" id=`"tex_throws`"]")
$null = $fishSb.AppendLine("[ext_resource type=`"Script`" path=`"res://Mobs/NPC/Fisherman/Fisherman.cs`" id=`"scr_Fisherman`"]")
$null = $fishSb.AppendLine("")
foreach ($ak in @("idle","throws")) {
    for ($i = 0; $i -lt 10; $i++) {
        $null = $fishSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_${ak}_down_${i}`"]")
        $null = $fishSb.AppendLine("atlas = ExtResource(`"tex_$ak`")")
        $null = $fishSb.AppendLine("region = Rect2($($i*32), 0, 32, 48)")
        $null = $fishSb.AppendLine("")
    }
}
$null = $fishSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_Fisherman`"]")
$fishEntries = @("idle","throws") | ForEach-Object {
    $ak = $_
    $ls = if ($ak -eq "idle") { "true" } else { "false" }
    $fr = (0..9 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_${ak}_down_$_`")}" }) -join ", "
    "{`"frames`": [$fr], `"loop`": $ls, `"name`": &`"${ak}_down`", `"speed`": 6.0}"
}
$null = $fishSb.AppendLine("animations = [$($fishEntries -join ', ')]")
$null = $fishSb.AppendLine("")
$null = $fishSb.AppendLine("[sub_resource type=`"CapsuleShape2D`" id=`"Cap_Fisherman`"]")
$null = $fishSb.AppendLine("radius = 8")
$null = $fishSb.AppendLine("height = 18")
$null = $fishSb.AppendLine("")
$null = $fishSb.AppendLine("[node name=`"Fisherman`" type=`"CharacterBody2D`"]")
$null = $fishSb.AppendLine("script = ExtResource(`"scr_Fisherman`")")
$null = $fishSb.AppendLine("")
$null = $fishSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $fishSb.AppendLine("sprite_frames = SubResource(`"SF_Fisherman`")")
$null = $fishSb.AppendLine("animation = &`"idle_down`"")
$null = $fishSb.AppendLine("")
$null = $fishSb.AppendLine("[node name=`"CollisionShape2D`" type=`"CollisionShape2D`" parent=`".`"]")
$null = $fishSb.AppendLine("shape = SubResource(`"Cap_Fisherman`")")
Write-Scene "$NPC_BASE\Fisherman\Fisherman.tscn" $fishSb.ToString()
Write-Scene "$NPC_BASE\Fisherman\Fisherman.cs"   (NpcCs "Fisherman")

# ============================================================
# Cobold — 96x32, fw=32, fh=32, 1 dir, n=3
# ============================================================
$cobScript = "res://Mobs/NPC/Cobold/Cobold.cs"
Write-Scene "$NPC_BASE\Cobold\Cobold.tscn" (Build-StaticNpcScene "Cobold" "npc_cobold" $cobScript "idle" "$ASSETS/$npcBase/Cobold_on_floor.png" 3 32 32 6 12)
Write-Scene "$NPC_BASE\Cobold\Cobold.cs"   (NpcCs "Cobold")

# ============================================================
# Citizens folder — traders, alchemist, thief, etc.
# ============================================================

# Traders: single-row horizontal strips, 1 dir "down"
$traders = @(
    @{name="TraderBread";  uid="npc_traderbread";  file="Trader_bread_animation_with_shadow.png";  fw=32; n=12}
    @{name="TraderDrinks"; uid="npc_traderdrinks"; file="Trader_drinks_animation_with_shadow.png"; fw=32; n=12}
    @{name="TraderWeapon"; uid="npc_traderweapon"; file="Trader_weapon_animation_with_shadow.png"; fw=32; n=8}
    @{name="TraderMagic";  uid="npc_tradermagic";  file="Trader_magic_animation_with_shadow.png";  fw=48; fh=48; n=8}
    @{name="TraderFruits"; uid="npc_traderfruits"; file="Trader_fruits_animation.png";             fw=32; n=9}
)
foreach ($t in $traders) {
    $fh   = if ($t.ContainsKey("fh")) { $t.fh } else { $t.fw }
    $nm   = $t.name
    $scr  = "res://Mobs/NPC/$nm/$nm.cs"
    Write-Scene "$NPC_BASE\$nm\$nm.tscn" (Build-StaticNpcScene $nm $t.uid $scr "idle" "$ASSETS/Charakter/citizens/$($t.file)" $t.n $t.fw $fh 6 14)
    Write-Scene "$NPC_BASE\$nm\$nm.cs"   (NpcCs $nm)
}

# Thief — 144x64, fw=48, fh=64, 1 dir, n=3 idle frames (Thief_idle)
$thiefScript = "res://Mobs/NPC/Thief/Thief.cs"
Write-Scene "$NPC_BASE\Thief\Thief.tscn" (Build-StaticNpcScene "Thief" "npc_thief" $thiefScript "idle" "$ASSETS/Charakter/citizens/Thief_idle.png" 3 48 64 8 16)
Write-Scene "$NPC_BASE\Thief\Thief.cs"   (NpcCs "Thief")

# Flutist — 192x48, fw=48, fh=48, 1 dir, n=4
$fluScript = "res://Mobs/NPC/Flutist/Flutist.cs"
Write-Scene "$NPC_BASE\Flutist\Flutist.tscn" (Build-StaticNpcScene "Flutist" "npc_flutist" $fluScript "play" "$ASSETS/Charakter/citizens/Flutist_animation_with_shadow.png" 4 48 48 6 14)
Write-Scene "$NPC_BASE\Flutist\Flutist.cs"   (NpcCs "Flutist")

# LutePlayer — 192x32, fw=32, fh=32, 1 dir, n=6
$luteScript = "res://Mobs/NPC/LutePlayer/LutePlayer.cs"
Write-Scene "$NPC_BASE\LutePlayer\LutePlayer.tscn" (Build-StaticNpcScene "LutePlayer" "npc_luteplayer" $luteScript "play" "$ASSETS/Charakter/citizens/Lute_player_animation_with_shadow.png" 6 32 32 6 14)
Write-Scene "$NPC_BASE\LutePlayer\LutePlayer.cs"   (NpcCs "LutePlayer")

Write-Host "`n=== NPC scenes done ===" -ForegroundColor Green
