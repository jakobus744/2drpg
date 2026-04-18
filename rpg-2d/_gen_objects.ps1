# === Biome Animation Objects Generator ===
# Creates Node2D scenes in Objects/{Zone}/ for all animated biome objects
param([switch]$DryRun)

$BASE    = "C:\Hochschule\6 Semester\Projekt\2drpg\rpg-2d"
$OBJ_ROOT = "$BASE\Objects"
$ASSETS  = "res://Assets"
$NoBom   = New-Object System.Text.UTF8Encoding $false

function Write-Scene($path, $content) {
    if ($DryRun) { Write-Host "[DRY] $path"; return }
    $dir = Split-Path $path -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($path, $content, $NoBom)
    Write-Host "OK  $path"
}

# Build a Node2D object scene (no collision, no CharacterBody2D)
# $frames: array of @{x;y;fw;fh} regions (or auto-compute from horizontal strip)
# $animName: name of the animation
function Build-ObjScene($name, $uid, $texResPath, $fw, $fh, $n, $animName, $loop, $spd) {
    $sb = [System.Text.StringBuilder]::new()
    $null = $sb.AppendLine("[gd_scene format=4 uid=`"uid://${uid}`"]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$texResPath`" id=`"tex_main`"]")
    $null = $sb.AppendLine("")
    for ($i = 0; $i -lt $n; $i++) {
        $null = $sb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_${i}`"]")
        $null = $sb.AppendLine("atlas = ExtResource(`"tex_main`")")
        $null = $sb.AppendLine("region = Rect2($($i*$fw), 0, $fw, $fh)")
        $null = $sb.AppendLine("")
    }
    $ls = if ($loop) { "true" } else { "false" }
    $frames = (0..($n-1) | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
    $null = $sb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_$name`"]")
    $null = $sb.AppendLine("animations = [{`"frames`": [$frames], `"loop`": $ls, `"name`": &`"$animName`", `"speed`": $spd}]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"$name`" type=`"Node2D`"]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
    $null = $sb.AppendLine("sprite_frames = SubResource(`"SF_$name`")")
    $null = $sb.AppendLine("animation = &`"$animName`"")
    $null = $sb.AppendLine("autoplay = `"$animName`"")
    $sb.ToString()
}

# Multi-animation variant: each row = one animation
# $anims: array of @{key; n; row; fw; fh; loop; spd}
function Build-ObjSceneMulti($name, $uid, $texResPath, $anims) {
    $sb = [System.Text.StringBuilder]::new()
    $null = $sb.AppendLine("[gd_scene format=4 uid=`"uid://${uid}`"]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$texResPath`" id=`"tex_main`"]")
    $null = $sb.AppendLine("")
    foreach ($a in $anims) {
        $y = $a.row * $a.fh
        for ($i = 0; $i -lt $a.n; $i++) {
            $null = $sb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$($a.key)_${i}`"]")
            $null = $sb.AppendLine("atlas = ExtResource(`"tex_main`")")
            $null = $sb.AppendLine("region = Rect2($($i*$a.fw), $y, $($a.fw), $($a.fh))")
            $null = $sb.AppendLine("")
        }
    }
    $null = $sb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_$name`"]")
    $entries = $anims | ForEach-Object {
        $a = $_
        $ls = if ($a.loop) { "true" } else { "false" }
        $fr = (0..($a.n-1) | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$($a.key)_$_`")}" }) -join ", "
        "{`"frames`": [$fr], `"loop`": $ls, `"name`": &`"$($a.key)`", `"speed`": $($a.spd)}"
    }
    $null = $sb.AppendLine("animations = [$($entries -join ', ')]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"$name`" type=`"Node2D`"]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
    $null = $sb.AppendLine("sprite_frames = SubResource(`"SF_$name`")")
    $null = $sb.AppendLine("animation = &`"$($anims[0].key)`"")
    $null = $sb.AppendLine("autoplay = `"$($anims[0].key)`"")
    $sb.ToString()
}

# Shorthand to write one object scene
function Obj($zone, $name, $uid, $assetRelPath, $fw, $fh, $n, $animName="animate", $loop=$true, $spd=8) {
    $tex   = "$ASSETS/$assetRelPath"
    $tscn  = Build-ObjScene $name $uid $tex $fw $fh $n $animName $loop $spd
    Write-Scene "$OBJ_ROOT\$zone\$name.tscn" $tscn
}

# ============================================================
# FOREST
# ============================================================
# bird_fly_animation: 432x1024, fw=48, fh=128, 4-dir rows, n=9
$birdAnims = @(
    @{key="fly_down";   n=9; row=0; fw=48; fh=128; loop=$true;  spd=10}
    @{key="fly_up";     n=9; row=1; fw=48; fh=128; loop=$true;  spd=10}
    @{key="fly_left";   n=9; row=2; fw=48; fh=128; loop=$true;  spd=10}
    @{key="fly_right";  n=9; row=3; fw=48; fh=128; loop=$true;  spd=10}
)
Write-Scene "$OBJ_ROOT\Forest\BirdFly.tscn" (Build-ObjSceneMulti "BirdFly" "obj_birdfly" "$ASSETS/Biome/1.Forest/animation/bird_fly_animation.png" $birdAnims)

# bird_jump_animation: 640x32, fw=32, fh=32, n=20
Obj "Forest" "BirdJump" "obj_birdjump" "Biome/1.Forest/animation/bird_jump_animation.png" 32 32 20 "jump"

# Smoke_animation: 288x48, fw=48, fh=48, n=6
Obj "Forest" "Smoke" "obj_smoke" "Biome/1.Forest/animation/Smoke_animation.png" 48 48 6 "smoke"

# Boiler: 384x48, fw=48, fh=48, n=8
Obj "Forest" "Boiler" "obj_boiler" "Biome/1.Forest/animation/Boiler.png" 48 48 8 "boil"

# Trees_animation: 576x1040, fw=48, fh=80, n=12 (first row = tree sway)
Obj "Forest" "Tree" "obj_tree_forest" "Biome/1.Forest/animation/Trees_animation.png" 48 80 12 "sway"

# water_lilis_animation: 288x64, fw=32, fh=32, n=9
Obj "Forest" "WaterLilies" "obj_waterlilies" "Biome/1.Forest/animation/water_lilis_animation.png" 32 32 9 "animate"

# cat_animation: 96x576 vertical strip, fw=96, fh=64, n=9 (use first row approach but vertical)
# Build manually (vertical strip)
$catSb = [System.Text.StringBuilder]::new()
$null = $catSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_forestcat`"]")
$null = $catSb.AppendLine("")
$null = $catSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/Biome/1.Forest/animation/cat_animation.png`" id=`"tex_main`"]")
$null = $catSb.AppendLine("")
for ($i = 0; $i -lt 9; $i++) {
    $null = $catSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $catSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $catSb.AppendLine("region = Rect2(0, $($i*64), 96, 64)")
    $null = $catSb.AppendLine("")
}
$catFrames = (0..8 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $catSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_ForestCat`"]")
$null = $catSb.AppendLine("animations = [{`"frames`": [$catFrames], `"loop`": true, `"name`": &`"idle`", `"speed`": 6.0}]")
$null = $catSb.AppendLine("")
$null = $catSb.AppendLine("[node name=`"ForestCat`" type=`"Node2D`"]")
$null = $catSb.AppendLine("")
$null = $catSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $catSb.AppendLine("sprite_frames = SubResource(`"SF_ForestCat`")")
$null = $catSb.AppendLine("animation = &`"idle`"")
$null = $catSb.AppendLine("autoplay = `"idle`"")
Write-Scene "$OBJ_ROOT\Forest\ForestCat.tscn" $catSb.ToString()

# ============================================================
# WINTER
# ============================================================
Obj "Winter" "WaterDetail" "obj_water_winter" "Biome/2.Winter/animation/water_detilazation_v3.png" 16 16 43 "animate"

# ============================================================
# GRASSLAND
# ============================================================
# Animated_objects_gras: 752x1440 — fw=48, fh=48, n=15 (first row)
Obj "Grassland" "GrassObjects" "obj_grass_anim" "Biome/3.Grasland/animation/Animated_objects_gras.png" 48 48 15 "animate"

# ============================================================
# DESERT
# ============================================================
# Objects_animated_dessert: 672x512, fw=32, fh=32, n=21 (first row)
Obj "Desert" "DesertObjects" "obj_desert_anim" "Biome/4.Dessert/animation/Objects_animated_dessert.png" 32 32 21 "animate"
# sand: 128x192, fw=32, fh=32, n=4 (first row, 3 rows = 3 types)
Obj "Desert" "Sand" "obj_sand" "Biome/4.Dessert/animation/sand.png" 32 32 4 "animate"

# ============================================================
# SWAMP
# ============================================================
# duckweed: 160x672 vertical strip, fw=160, fh=32, n=21... or fw=16, fh=16
Obj "Swamp" "Duckweed" "obj_duckweed" "Biome/5.Swamp/animation/duckweed.png" 16 16 10 "animate"
Obj "Swamp" "WaterLilies" "obj_swamp_lilies" "Biome/5.Swamp/animation/water_lilis.png" 16 16 7 "animate"

# ============================================================
# SKELETON POISON
# ============================================================
# Animation1_skellet_trees: 592x384, fw=48, fh=48, n=12 (first row)
Obj "SkeletonPoison" "SkeletonTree" "obj_sk_tree" "Biome/7.Skelett-Poisen/animation/Animation1_skellet_trees.png" 48 48 12 "animate"
Obj "SkeletonPoison" "SkeletonObj2" "obj_sk_obj2" "Biome/7.Skelett-Poisen/animation/Animation2.png" 48 48 10 "animate"
Obj "SkeletonPoison" "SkeletonObj3" "obj_sk_obj3" "Biome/7.Skelett-Poisen/animation/Animation3.png" 48 48 8  "animate"
Obj "SkeletonPoison" "SkeletonObj4" "obj_sk_obj4" "Biome/7.Skelett-Poisen/animation/Animation4.png" 48 48 10 "animate"
Obj "SkeletonPoison" "SkeletonObj5" "obj_sk_obj5" "Biome/7.Skelett-Poisen/animation/Animation5.png" 48 48 12 "animate"
Obj "SkeletonPoison" "SkeletonObj6" "obj_sk_obj6" "Biome/7.Skelett-Poisen/animation/Animation6.png" 48 48 6  "animate"

# ============================================================
# COAST
# ============================================================
# waterfall: 144x480 vertical strip — fw=144, fh=48, n=10
$wfCoastSb = [System.Text.StringBuilder]::new()
$null = $wfCoastSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_coast_waterfall`"]")
$null = $wfCoastSb.AppendLine("")
$null = $wfCoastSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/Biome/Coast/animation/waterfall.png`" id=`"tex_main`"]")
$null = $wfCoastSb.AppendLine("")
for ($i = 0; $i -lt 10; $i++) {
    $null = $wfCoastSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $wfCoastSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $wfCoastSb.AppendLine("region = Rect2(0, $($i*48), 144, 48)")
    $null = $wfCoastSb.AppendLine("")
}
$wfFrames = (0..9 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $wfCoastSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_CoastWaterfall`"]")
$null = $wfCoastSb.AppendLine("animations = [{`"frames`": [$wfFrames], `"loop`": true, `"name`": &`"fall`", `"speed`": 8.0}]")
$null = $wfCoastSb.AppendLine("")
$null = $wfCoastSb.AppendLine("[node name=`"CoastWaterfall`" type=`"Node2D`"]")
$null = $wfCoastSb.AppendLine("")
$null = $wfCoastSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $wfCoastSb.AppendLine("sprite_frames = SubResource(`"SF_CoastWaterfall`")")
$null = $wfCoastSb.AppendLine("animation = &`"fall`"")
$null = $wfCoastSb.AppendLine("autoplay = `"fall`"")
Write-Scene "$OBJ_ROOT\Coast\CoastWaterfall.tscn" $wfCoastSb.ToString()

# ============================================================
# DUNGEON
# ============================================================
$DA = "Biome/Dungeon/Skellet Dungeon/animation"

# candles: 432x224, fw=48, fh=48, n=9 (first row)
Obj "Dungeon" "Candle"       "obj_candle"      "$DA/candles.png"           48 48 9  "flicker"
# torches: 224x288, fw=32, fh=32, n=7 (first row)
Obj "Dungeon" "Torch"        "obj_torch"       "$DA/torches.png"           32 32 7  "flicker"
# Chest_door_lever: 96x208, fw=16, fh=16, n=6 (first row)
Obj "Dungeon" "ChestDoor"    "obj_chestdoor"   "$DA/Chest_door_lever.png"  16 16 6  "animate"
# doors: 128x288, fw=32, fh=32, n=4
Obj "Dungeon" "Door"         "obj_door"        "$DA/doors.png"             32 32 4  "open" $false
# Spikes: 288x64, fw=32, fh=32, n=9
Obj "Dungeon" "Spikes"       "obj_spikes"      "$DA/Spikes.png"            32 32 9  "animate"
# Spike_trap: 48x288 vertical — fw=48, fh=48, n=6
$spikeTrapSb = [System.Text.StringBuilder]::new()
$null = $spikeTrapSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_spiketrap`"]")
$null = $spikeTrapSb.AppendLine("")
$null = $spikeTrapSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$DA/Spike_trap.png`" id=`"tex_main`"]")
$null = $spikeTrapSb.AppendLine("")
for ($i = 0; $i -lt 6; $i++) {
    $null = $spikeTrapSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $spikeTrapSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $spikeTrapSb.AppendLine("region = Rect2(0, $($i*48), 48, 48)")
    $null = $spikeTrapSb.AppendLine("")
}
$stFrames = (0..5 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $spikeTrapSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_SpikeTrap`"]")
$null = $spikeTrapSb.AppendLine("animations = [{`"frames`": [$stFrames], `"loop`": true, `"name`": &`"extend`", `"speed`": 6.0}]")
$null = $spikeTrapSb.AppendLine("")
$null = $spikeTrapSb.AppendLine("[node name=`"SpikeTrap`" type=`"Node2D`"]")
$null = $spikeTrapSb.AppendLine("")
$null = $spikeTrapSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $spikeTrapSb.AppendLine("sprite_frames = SubResource(`"SF_SpikeTrap`")")
$null = $spikeTrapSb.AppendLine("animation = &`"extend`"")
$null = $spikeTrapSb.AppendLine("autoplay = `"extend`"")
Write-Scene "$OBJ_ROOT\Dungeon\SpikeTrap.tscn" $spikeTrapSb.ToString()

# fire_animation: 176x288, fw=16, fh=16, n=11
Obj "Dungeon" "DungeonFire"  "obj_dfire"       "$DA/fire_animation.png"    16 16 11 "burn"
# fire_animation2: 96x192, fw=32, fh=32, n=3
Obj "Dungeon" "DungeonFire2" "obj_dfire2"      "$DA/fire_animation2.png"   32 32 3  "burn"
# fire_trap: 1008x128, fw=48, fh=128, n=21
Obj "Dungeon" "FireTrap"     "obj_firetrap"    "$DA/fire_trap.png"         48 128 21 "animate"
# fountain_animation: 288x64, fw=32, fh=32, n=9
Obj "Dungeon" "Fountain"     "obj_fountain"    "$DA/fountain_animation.png" 32 32 9 "flow"
# Bomb: 576x48, fw=48, fh=48, n=12
Obj "Dungeon" "Bomb"         "obj_bomb"        "$DA/Bomb.png"              48 48 12 "tick" $false
# ghost_trap: 512x64, fw=64, fh=64, n=8
Obj "Dungeon" "GhostTrap"    "obj_ghosttrap"   "$DA/ghost_trap.png"        64 64 8  "animate"
# Guillotine: 576x48, fw=48, fh=48, n=12
Obj "Dungeon" "Guillotine"   "obj_guillotine"  "$DA/Guillotine.png"        48 48 12 "swing" $true 6
# column_trap: 288x64, fw=32, fh=32, n=9
Obj "Dungeon" "ColumnTrap"   "obj_columntrap"  "$DA/column_trap.png"       32 32 9  "animate"
# trap_saw: 384x256, fw=128, fh=128, n=3
Obj "Dungeon" "TrapSaw"      "obj_trapsaw"     "$DA/trap_saw.png"          128 128 3 "spin"
# Statue_fire: 480x80, fw=80, fh=80, n=6
Obj "Dungeon" "StatueFire"   "obj_statuefire"  "$DA/Statue_fire.png"       80 80 6  "animate"
# Waterfalls: 192x384 vertical — fw=192, fh=48, n=8
$dwfSb = [System.Text.StringBuilder]::new()
$null = $dwfSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_dung_waterfall`"]")
$null = $dwfSb.AppendLine("")
$null = $dwfSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$DA/Waterfalls.png`" id=`"tex_main`"]")
$null = $dwfSb.AppendLine("")
for ($i = 0; $i -lt 8; $i++) {
    $null = $dwfSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $dwfSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $dwfSb.AppendLine("region = Rect2(0, $($i*48), 192, 48)")
    $null = $dwfSb.AppendLine("")
}
$dwfFrames = (0..7 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $dwfSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_DungeonWaterfall`"]")
$null = $dwfSb.AppendLine("animations = [{`"frames`": [$dwfFrames], `"loop`": true, `"name`": &`"fall`", `"speed`": 8.0}]")
$null = $dwfSb.AppendLine("")
$null = $dwfSb.AppendLine("[node name=`"DungeonWaterfall`" type=`"Node2D`"]")
$null = $dwfSb.AppendLine("")
$null = $dwfSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $dwfSb.AppendLine("sprite_frames = SubResource(`"SF_DungeonWaterfall`")")
$null = $dwfSb.AppendLine("animation = &`"fall`"")
$null = $dwfSb.AppendLine("autoplay = `"fall`"")
Write-Scene "$OBJ_ROOT\Dungeon\DungeonWaterfall.tscn" $dwfSb.ToString()

# Arrow: 592x192, fw=16, fh=16, n=37 → too many. Use fw=48, fh=48 n=12
Obj "Dungeon" "Arrow"        "obj_arrow"       "$DA/Arrow.png"             48 48 12 "fly"
# dragon_trap: 320x496, fw=32, fh=32, n=10
Obj "Dungeon" "DragonTrap"   "obj_dragontrap"  "$DA/dragon_trap.png"       32 32 10 "animate"
# Flasks_monsters: 144x384, fw=48, fh=48, n=3 (first row)
Obj "Dungeon" "Flask"        "obj_flask"       "$DA/Flasks_monsters.png"   48 48 3  "animate"

# ============================================================
# FLYING ENDGAME
# ============================================================
$FE = "Biome/Flying-endgame/animation"
# Clouds_animated1: 384x112, fw=48, fh=112, n=8 → or fw=32 n=12
Obj "SkyEndgame" "Cloud1"       "obj_cloud1"      "$FE/Clouds_animated1.png"  48 112 8 "drift"
Obj "SkyEndgame" "Cloud2"       "obj_cloud2"      "$FE/Clouds_animated2.png"  48 112 8 "drift"
# Flying_flowers: 384x128, fw=48, fh=128, n=8
Obj "SkyEndgame" "FlyingFlower" "obj_flyflower"   "$FE/Flying_flowers.png"    48 128 8 "float"
# Statue_animated: 864x144, fw=144, fh=144, n=6
Obj "SkyEndgame" "Statue"       "obj_sky_statue"  "$FE/Statue_animated.png"  144 144 6 "animate"
# Tree_animated: 960x160, fw=160, fh=160, n=6
Obj "SkyEndgame" "SkyTree"      "obj_skytree"     "$FE/Tree_animated.png"    160 160 6 "sway"
# Waterfalls: 192x1056 vertical strip
$skyWfSb = [System.Text.StringBuilder]::new()
$null = $skyWfSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_sky_waterfall`"]")
$null = $skyWfSb.AppendLine("")
$null = $skyWfSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$FE/Waterfalls.png`" id=`"tex_main`"]")
$null = $skyWfSb.AppendLine("")
for ($i = 0; $i -lt 22; $i++) {
    $null = $skyWfSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $skyWfSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $skyWfSb.AppendLine("region = Rect2(0, $($i*48), 192, 48)")
    $null = $skyWfSb.AppendLine("")
}
$skyWfFrames = (0..21 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $skyWfSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_SkyWaterfall`"]")
$null = $skyWfSb.AppendLine("animations = [{`"frames`": [$skyWfFrames], `"loop`": true, `"name`": &`"fall`", `"speed`": 8.0}]")
$null = $skyWfSb.AppendLine("")
$null = $skyWfSb.AppendLine("[node name=`"SkyWaterfall`" type=`"Node2D`"]")
$null = $skyWfSb.AppendLine("")
$null = $skyWfSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $skyWfSb.AppendLine("sprite_frames = SubResource(`"SF_SkyWaterfall`")")
$null = $skyWfSb.AppendLine("animation = &`"fall`"")
$null = $skyWfSb.AppendLine("autoplay = `"fall`"")
Write-Scene "$OBJ_ROOT\SkyEndgame\SkyWaterfall.tscn" $skyWfSb.ToString()

# ============================================================
# GLOWING CAVE
# ============================================================
$GC = "Biome/glowing-cave/animation"
# Wisp1: 192x560 vertical — fw=192, fh=80, n=7
$wispSb = [System.Text.StringBuilder]::new()
$null = $wispSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_wisp`"]")
$null = $wispSb.AppendLine("")
$null = $wispSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$GC/Wisp1.png`" id=`"tex_main`"]")
$null = $wispSb.AppendLine("")
for ($i = 0; $i -lt 7; $i++) {
    $null = $wispSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $wispSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $wispSb.AppendLine("region = Rect2(0, $($i*80), 192, 80)")
    $null = $wispSb.AppendLine("")
}
$wispFrames = (0..6 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $wispSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_Wisp`"]")
$null = $wispSb.AppendLine("animations = [{`"frames`": [$wispFrames], `"loop`": true, `"name`": &`"float`", `"speed`": 6.0}]")
$null = $wispSb.AppendLine("")
$null = $wispSb.AppendLine("[node name=`"Wisp`" type=`"Node2D`"]")
$null = $wispSb.AppendLine("")
$null = $wispSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $wispSb.AppendLine("sprite_frames = SubResource(`"SF_Wisp`")")
$null = $wispSb.AppendLine("animation = &`"float`"")
$null = $wispSb.AppendLine("autoplay = `"float`"")
Write-Scene "$OBJ_ROOT\GlowingCave\Wisp.tscn" $wispSb.ToString()

# Totem_animation: 576x176, fw=48, fh=176, n=12
Obj "GlowingCave" "Totem"    "obj_totem"       "$GC/Totem_animation.png"   48 176 12 "animate"
# Shinies_animation: 480x192, fw=48, fh=48, n=10
Obj "GlowingCave" "Shinies"  "obj_shinies"     "$GC/Shinies_animation.png" 48 48 10 "shine"

# ============================================================
# LAVA CAVE
# ============================================================
$LC = "Biome/lava-Cave/animation"
# bubbles_source: 208x96, fw=16, fh=16, n=13
Obj "LavaCave" "LavaBubble"   "obj_lavabubble"  "$LC/bubbles_source.png"        16 16 13 "bubble"
# Objects_animated_source: 528x576, fw=48, fh=48, n=11 (first row)
Obj "LavaCave" "LavaObject"   "obj_lavaobj"     "$LC/Objects_animated_source.png" 48 48 11 "animate"
# Objects_animated2_source: 480x752, fw=48, fh=48, n=10
Obj "LavaCave" "LavaObject2"  "obj_lavaobj2"    "$LC/Objects_animated2_source.png" 48 48 10 "animate"

# ============================================================
# VILLAGE — farm-house
# ============================================================
# Sails_animation: 160x864 vertical — fw=160, fh=96, n=9
$sailSb = [System.Text.StringBuilder]::new()
$null = $sailSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_sails`"]")
$null = $sailSb.AppendLine("")
$null = $sailSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/Biome/village/farm-house/animation/Sails_animation.png`" id=`"tex_main`"]")
$null = $sailSb.AppendLine("")
for ($i = 0; $i -lt 9; $i++) {
    $null = $sailSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $sailSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $sailSb.AppendLine("region = Rect2(0, $($i*96), 160, 96)")
    $null = $sailSb.AppendLine("")
}
$sailFrames = (0..8 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $sailSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_Sails`"]")
$null = $sailSb.AppendLine("animations = [{`"frames`": [$sailFrames], `"loop`": true, `"name`": &`"spin`", `"speed`": 8.0}]")
$null = $sailSb.AppendLine("")
$null = $sailSb.AppendLine("[node name=`"Sails`" type=`"Node2D`"]")
$null = $sailSb.AppendLine("")
$null = $sailSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $sailSb.AppendLine("sprite_frames = SubResource(`"SF_Sails`")")
$null = $sailSb.AppendLine("animation = &`"spin`"")
$null = $sailSb.AppendLine("autoplay = `"spin`"")
Write-Scene "$OBJ_ROOT\Village\Sails.tscn" $sailSb.ToString()

# ============================================================
# VILLAGE — fishing-house
# ============================================================
# Boat1: 704x384, fw=64, fh=64, n=11 (first row)
Obj "Village" "Boat1"       "obj_boat1"   "Biome/village/fishing-house/animation/Boat1.png" 64 64 11 "rock"
# Boat2: 640x336, fw=64, fh=48, n=10
Obj "Village" "Boat2"       "obj_boat2"   "Biome/village/fishing-house/animation/Boat2.png" 64 48 10 "rock"

# ============================================================
# VILLAGE — guild-hall
# ============================================================
# Flags_animation: 96x576 vertical — fw=96, fh=48, n=12
$flagSb = [System.Text.StringBuilder]::new()
$null = $flagSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_guildflag`"]")
$null = $flagSb.AppendLine("")
$null = $flagSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/Biome/village/guild-hall/animation/Flags_animation.png`" id=`"tex_main`"]")
$null = $flagSb.AppendLine("")
for ($i = 0; $i -lt 12; $i++) {
    $null = $flagSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $flagSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $flagSb.AppendLine("region = Rect2(0, $($i*48), 96, 48)")
    $null = $flagSb.AppendLine("")
}
$flagFrames = (0..11 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $flagSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_GuildFlag`"]")
$null = $flagSb.AppendLine("animations = [{`"frames`": [$flagFrames], `"loop`": true, `"name`": &`"wave`", `"speed`": 8.0}]")
$null = $flagSb.AppendLine("")
$null = $flagSb.AppendLine("[node name=`"GuildFlag`" type=`"Node2D`"]")
$null = $flagSb.AppendLine("")
$null = $flagSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $flagSb.AppendLine("sprite_frames = SubResource(`"SF_GuildFlag`")")
$null = $flagSb.AppendLine("animation = &`"wave`"")
$null = $flagSb.AppendLine("autoplay = `"wave`"")
Write-Scene "$OBJ_ROOT\Village\GuildFlag.tscn" $flagSb.ToString()

# market Flags: 96x384 vertical, fw=96, fh=32, n=12
$mktFlagSb = [System.Text.StringBuilder]::new()
$null = $mktFlagSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_mktflag`"]")
$null = $mktFlagSb.AppendLine("")
$null = $mktFlagSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/Biome/village/market/animation/Flags_animation.png`" id=`"tex_main`"]")
$null = $mktFlagSb.AppendLine("")
for ($i = 0; $i -lt 12; $i++) {
    $null = $mktFlagSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $mktFlagSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $mktFlagSb.AppendLine("region = Rect2(0, $($i*32), 96, 32)")
    $null = $mktFlagSb.AppendLine("")
}
$mktFlagFrames = (0..11 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $mktFlagSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_MarketFlag`"]")
$null = $mktFlagSb.AppendLine("animations = [{`"frames`": [$mktFlagFrames], `"loop`": true, `"name`": &`"wave`", `"speed`": 8.0}]")
$null = $mktFlagSb.AppendLine("")
$null = $mktFlagSb.AppendLine("[node name=`"MarketFlag`" type=`"Node2D`"]")
$null = $mktFlagSb.AppendLine("")
$null = $mktFlagSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $mktFlagSb.AppendLine("sprite_frames = SubResource(`"SF_MarketFlag`")")
$null = $mktFlagSb.AppendLine("animation = &`"wave`"")
$null = $mktFlagSb.AppendLine("autoplay = `"wave`"")
Write-Scene "$OBJ_ROOT\Village\MarketFlag.tscn" $mktFlagSb.ToString()

# market candles: 432x224, fw=48, fh=48, n=9
Obj "Village" "MarketCandle" "obj_mktcandle" "Biome/village/market/animation/candles.png" 48 48 9 "flicker"

# ============================================================
# VILLAGE — magic-tower
# ============================================================
$MT = "Biome/village/magic-tower/animation"
# Demon: 400x320, fw=80, fh=80, n=5 (first row)
Obj "Village" "TowerDemon"  "obj_twrdemon"  "$MT/Demon.png"       80 80  5 "animate"
# Lightning: 320x384, fw=32, fh=32, n=10 (first row)
Obj "Village" "Lightning"   "obj_lightning" "$MT/Lightning.png"   32 32 10 "flash" $false 12
# Dragon_wing: 480x128, fw=96, fh=128, n=5
Obj "Village" "DragonWing"  "obj_dragonwing" "$MT/Dragon_wing.png" 96 128 5 "flap"

# ============================================================
# VILLAGE — shoping-house
# ============================================================
$SH = "Biome/village/shoping-house/animation"
# Forge: 384x96, fw=96, fh=96, n=4
Obj "Village" "Forge"       "obj_forge"   "$SH/Forge.png"   96 96 4 "spark"
# Customer: 96x64, fw=32, fh=32, n=3
Obj "Village" "Customer"    "obj_customer" "$SH/Customer.png" 32 32 3 "idle"

# ============================================================
# TAVERNE
# ============================================================
$TV = "Biome/village/taverne/animation"
# Windows/Doors: 640x320, fw=64, fh=64, n=10 (first row)
Obj "Village" "TaverneDoor"  "obj_tavernedoor" "$TV/Animation_windows_doors.png" 64 64 10 "open" $false
# door_small: 160x224 vertical — fw=160, fh=32, n=7
$dsmSb = [System.Text.StringBuilder]::new()
$null = $dsmSb.AppendLine("[gd_scene format=4 uid=`"uid://obj_doorsm`"]")
$null = $dsmSb.AppendLine("")
$null = $dsmSb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$ASSETS/$TV/door_small.png`" id=`"tex_main`"]")
$null = $dsmSb.AppendLine("")
for ($i = 0; $i -lt 7; $i++) {
    $null = $dsmSb.AppendLine("[sub_resource type=`"AtlasTexture`" id=`"AT_$i`"]")
    $null = $dsmSb.AppendLine("atlas = ExtResource(`"tex_main`")")
    $null = $dsmSb.AppendLine("region = Rect2(0, $($i*32), 160, 32)")
    $null = $dsmSb.AppendLine("")
}
$dsmFrames = (0..6 | ForEach-Object { "{`"duration`": 1.0, `"texture`": SubResource(`"AT_$_`")}" }) -join ", "
$null = $dsmSb.AppendLine("[sub_resource type=`"SpriteFrames`" id=`"SF_DoorSmall`"]")
$null = $dsmSb.AppendLine("animations = [{`"frames`": [$dsmFrames], `"loop`": false, `"name`": &`"open`", `"speed`": 8.0}]")
$null = $dsmSb.AppendLine("")
$null = $dsmSb.AppendLine("[node name=`"DoorSmall`" type=`"Node2D`"]")
$null = $dsmSb.AppendLine("")
$null = $dsmSb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
$null = $dsmSb.AppendLine("sprite_frames = SubResource(`"SF_DoorSmall`")")
$null = $dsmSb.AppendLine("animation = &`"open`"")
Write-Scene "$OBJ_ROOT\Village\DoorSmall.tscn" $dsmSb.ToString()

# Taverne character animations (single-row strips)
$taverneChars = @(
    @{name="TavernHost";   uid="obj_tvhost";   file="Animation_host.png";   fw=32;  fh=32;  n=6}
    @{name="TavernKnight"; uid="obj_tvknight"; file="Animation_knight.png"; fw=80;  fh=80;  n=5}
    @{name="TavernEater";  uid="obj_tveater";  file="Animation_eater.png";  fw=32;  fh=32;  n=3}
    @{name="TavernLute";   uid="obj_tvlute";   file="Animation_Lute_player_full.png"; fw=48; fh=48; n=4}
    @{name="TavernWatcher";uid="obj_tvwatch";  file="Animation_watcher.png";fw=48;  fh=48;  n=8}
    @{name="TavernSleep";  uid="obj_tvsleep";  file="Animation_sleep_guy.png"; fw=32; fh=48; n=4}
    @{name="TavernSleep2"; uid="obj_tvsleep2"; file="Animation_sleep_guy2.png"; fw=32; fh=32; n=10}
)
foreach ($tc in $taverneChars) {
    Obj "Village" $tc.name $tc.uid "Biome/village/taverne/animation/$($tc.file)" $tc.fw $tc.fh $tc.n "animate"
}

Write-Host "`n=== Object scenes done ===" -ForegroundColor Green
