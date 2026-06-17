# === Mob Scene Generator v2 ===
# Fixes: no BOM, correct asset paths, all 3 monster variants, human fighters
param([switch]$DryRun)

$BASE   = "C:\Hochschule\6 Semester\Projekt\2drpg\rpg-2d"
$MOBS   = "$BASE\Mobs"
$ASSETS = "res://Assets"
$NoBom  = New-Object System.Text.UTF8Encoding $false   # no BOM!

# ---------- helpers ----------

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

function AnimDict($animKey, $dir, $n, $loop, $spd) {
    $ls = if ($loop) { "true" } else { "false" }
    $frames = (0..($n-1) | ForEach-Object {
        "{`"duration`": 1.0, `"texture`": SubResource(`"AT_${animKey}_${dir}_$_`")}"
    }) -join ", "
    "{`"frames`": [$frames], `"loop`": $ls, `"name`": &`"${animKey}_${dir}`", `"speed`": $spd}"
}

function Build4DirScene($name, $uid, $scriptResPath, $anims, $dirs, $capR, $capH) {
    $sb = [System.Text.StringBuilder]::new()
    $null = $sb.AppendLine("[gd_scene format=4 uid=`"uid://${uid}`"]")
    $null = $sb.AppendLine("")
    foreach ($a in $anims) {
        $null = $sb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$($a.path)`" id=`"$($a.extId)`"]")
    }
    $scriptId = "scr_$name"
    $null = $sb.AppendLine("[ext_resource type=`"Script`" path=`"$scriptResPath`" id=`"$scriptId`"]")
    $null = $sb.AppendLine("")
    for ($di = 0; $di -lt $dirs.Count; $di++) {
        $dir = $dirs[$di]
        foreach ($a in $anims) {
            $null = $sb.Append((DirAtlas $a.key $dir $a.extId $a.n $a.fw $a.fh $di))
        }
    }
    $sfId = "SF_$name"
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
    $null = $sb.AppendLine("script = ExtResource(`"$scriptId`")")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
    $null = $sb.AppendLine("sprite_frames = SubResource(`"$sfId`")")
    $null = $sb.AppendLine("animation = &`"idle_$($dirs[0])`"")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"CollisionShape2D`" type=`"CollisionShape2D`" parent=`".`"]")
    $null = $sb.AppendLine("shape = SubResource(`"$capId`")")
    $sb.ToString()
}

function MobCs($name) {
    @"
using Godot;

public partial class $name : MobBase
{
}
"@
}

function FarmAnimalCs($name) {
    @"
using Godot;

public partial class $name : MobBase
{
}
"@
}

function Write-Scene($path, $content) {
    if ($DryRun) { Write-Host "[DRY] $path"; return }
    $dir = Split-Path $path -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($path, $content, $NoBom)
    Write-Host "OK  $path"
}

$DIRS4 = [System.Collections.Generic.List[string]]@("down","up","left","right")
$DIRS3 = [System.Collections.Generic.List[string]]@("down","left","right")
$DIRS1 = [System.Collections.Generic.List[string]]@("down")

# ================================================================
# Helper: build anim list from template
# ================================================================
function MakeAnims($assetPathList, $keys, $ns, $loops, $spds, $fw, $fh) {
    $out = @()
    for ($i = 0; $i -lt $assetPathList.Count; $i++) {
        $out += @{
            key   = $keys[$i]
            path  = "$ASSETS/$($assetPathList[$i])"
            extId = "tex_$($keys[$i])"
            n     = $ns[$i]
            fw    = $fw
            fh    = $fh
            loop  = $loops[$i]
            spd   = $spds[$i]
        }
    }
    $out
}

# ================================================================
# STANDARD MONSTER VARIANTS (v1, v2, v3)
# For each: scene name, subfolder, asset folder, file prefix func, frame data
# ================================================================

# Standard monster data:
# @{ name; mobSub (in Mobs/Monster/); assetFolder; fw; fh; capR; capH;
#    vars = array of @{v; prefix; animFiles=@{key=>file}} }

function StdFile($prefix, $anim) { "${prefix}_${anim}_with_shadow.png" }
function OrcFile($v, $anim)      { "orc${v}_${anim}_with_shadow.png" }

# Animation specs (key, n, loop, spd) — same for all variants unless noted
$ANIMS_STD = @(
    @{key="idle";   n=4;  loop=$true;  spd=6}
    @{key="walk";   n=6;  loop=$true;  spd=8}
    @{key="run";    n=8;  loop=$true;  spd=10}
    @{key="attack"; n=8;  loop=$false; spd=8}
    @{key="hurt";   n=4;  loop=$false; spd=8}
    @{key="death";  n=6;  loop=$false; spd=6}
)

# Per-monster overrides for frame counts
$FRAME_OVERRIDES = @{
    "Ghost"    = @{idle=4; walk=6; run=6; attack=12; hurt=4; death=9}
    "Gnoll"    = @{idle=4; walk=6; run=8; attack=10; hurt=4; death=6}
    "Goblin"   = @{idle=4; walk=6; run=8; attack=5;  hurt=4; death=6}
    "Imp"      = @{idle=4; walk=6; run=8; attack=6;  hurt=4; death=10}
    "Lich"     = @{idle=4; walk=6; run=6; attack=8;  hurt=4; death=10}
    "Lizardman"= @{idle=4; walk=6; run=8; attack=7;  hurt=5; death=7}
    "Mushroom" = @{idle=4; walk=6; run=6; attack=8;  hurt=4; death=9}
    "Plant"    = @{idle=4; walk=6; run=8; attack=7;  hurt=5; death=10}
    "Skeleton" = @{idle=4; walk=6; run=8; attack=9;  hurt=4; death=6}
    "Vampir"   = @{idle=4; walk=6; run=8; attack=12; hurt=4; death=11}
    "Zombie"   = @{idle=4; walk=6; run=8; attack=10; hurt=4; death=9}
    "Beholder" = @{idle=12;walk=8; run=8; attack=12; hurt=6; death=9}
    "Ent"      = @{idle=4; walk=6; run=8; attack=7;  hurt=4; death=6}
    "Demon"    = @{idle=4; walk=6; run=8; attack=10; hurt=4; death=13}
    "Golem"    = @{idle=4; walk=8; run=8; attack=9;  hurt=4; death=8}
    "Rat"      = @{idle=6; walk=6; run=6; attack=8;  hurt=4; death=5}
    "Orc"      = @{idle=4; walk=6; run=8; attack=8;  hurt=6; death=8}
}

# Monster definitions with correct asset subfolder paths
$monsterDefs = @(
    # name, mobFolder, assetFolder, fw, fh, capR, capH,
    # filePrefixFunc: given version int -> prefix string (or special handling)
    @{n="Ghost";     sub="Monster/Ghost";     asf="ghost";    fw=64;  fh=64;  r=10; h=20;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Ghost$v" } }

    @{n="Gnoll";     sub="Monster/Gnoll";     asf="gnolls";   fw=64;  fh=64;  r=10; h=20;
      animKeys=@("idle","walk","run","attack","hurt","death")
      # death in v1 has no version number
      fileFn = { param($v) "Gnoll$v" }
      deathV1 = "Gnoll" }   # special: death filename prefix for v1

    @{n="Imp";       sub="Monster/Imp";       asf="imp";      fw=64;  fh=64;  r=8;  h=16;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Imp$v" } }

    @{n="Lich";      sub="Monster/Lich";      asf="lich";     fw=64;  fh=64;  r=10; h=20;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Lich$v" } }

    @{n="Lizardman"; sub="Monster/Lizardman"; asf="lizardmen";fw=64;  fh=64;  r=10; h=20;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Lizardman$v" } }

    @{n="Mushroom";  sub="Monster/Mushroom";  asf="Mushroom"; fw=64;  fh=64;  r=10; h=18;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Mushroom$v" } }

    @{n="Plant";     sub="Monster/Plant";     asf="plant";    fw=64;  fh=64;  r=10; h=18;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Plant$v" } }

    @{n="Skeleton";  sub="Monster/Skeleton";  asf="skeletons";fw=64;  fh=64;  r=10; h=20;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Skeleton$v" } }

    @{n="Vampir";    sub="Monster/Vampir";    asf="vampir";   fw=64;  fh=64;  r=10; h=20;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Vampires$v" } }

    @{n="Zombie";    sub="Monster/Zombie";    asf="zombie";   fw=64;  fh=64;  r=10; h=20;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Zombie$v" } }

    @{n="Beholder";  sub="Monster/Beholder";  asf="beholder"; fw=64;  fh=64;  r=14; h=24;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Beholder$v" } }

    # Large 128px
    @{n="Ent";       sub="Monster/Ent";       asf="ent";      fw=128; fh=128; r=20; h=40;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Ent$v" } }

    @{n="Demon";     sub="Monster/Demon";     asf="demons";   fw=128; fh=128; r=20; h=40;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Demon$v" } }

    @{n="Golem";     sub="Monster/Golem";     asf="golem";    fw=128; fh=128; r=22; h=44;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Golem$v" } }

    @{n="Rat";       sub="Monster/Rat";       asf="giant-rat";fw=128; fh=128; r=18; h=30;
      animKeys=@("idle","walk","run","attack","hurt","death")
      fileFn = { param($v) "Rat$v" } }
)

# Goblin is special: different file naming per variant
$goblinAnimKeys = @("idle","walk","run","attack","hurt","death","run_attack","walk_attack")
$goblinN        = @(4, 6, 8, 5, 4, 6, 8, 6)
$goblinLoops    = @($true,$true,$true,$false,$false,$false,$false,$false)
$goblinSpds     = @(6, 8, 10, 8, 8, 6, 8, 8)

# Orc also special: lowercase names
$orcAnimKeys = @("idle","walk","run","attack","hurt","death","run_attack","walk_attack")
$orcN        = @(4, 6, 8, 8, 6, 8, 8, 6)
$orcLoops    = @($true,$true,$true,$false,$false,$false,$false,$false)
$orcSpds     = @(6, 8, 10, 8, 8, 6, 8, 8)

function Build-MonsterVariants($def, $versions) {
    $frames  = $FRAME_OVERRIDES[$def.n]
    $animKeys = $def.animKeys

    foreach ($v in $versions) {
        $vSuffix = if ($v -eq 1) { "" } else { "$v" }
        $sceneName = "$($def.n)${vSuffix}"
        $uid = "mob_$($def.n.ToLower())_v${v}"

        # Build anim list
        $animList = @()
        foreach ($key in $animKeys) {
            # Determine file prefix
            $prefix = & $def.fileFn $v
            # Special: Gnoll death v1 has no version number
            if ($def.ContainsKey("deathV1") -and $key -eq "death" -and $v -eq 1) {
                $prefix = $def.deathV1
            }
            $file = "monster/$($def.asf)/$v/${prefix}_${key}_with_shadow.png"
            # Capitalize Anim for file (title-case the key)
            $animCap = (Get-Culture).TextInfo.ToTitleCase($key)
            $file = "monster/$($def.asf)/$v/${prefix}_${animCap}_with_shadow.png"

            $n = if ($frames) { $frames[$key] } else { 6 }
            $loop = switch ($key) { "idle" {$true} "walk" {$true} "run" {$true} default {$false} }
            $spd  = switch ($key) { "idle" {6} "walk" {8} "run" {10} "death" {6} default {8} }

            $animList += @{
                key   = $key
                path  = "$ASSETS/$file"
                extId = "tex_$key"
                n     = $n
                fw    = $def.fw
                fh    = $def.fh
                loop  = $loop
                spd   = $spd
            }
        }

        $sceneDir  = "$MOBS\$($def.sub)"
        $scenePath = "$sceneDir\${sceneName}.tscn"
        $csPath    = "$sceneDir\${sceneName}.cs"
        $scriptRes = "res://Mobs/$($def.sub.Replace('\','/'))/$(${sceneName}).cs"

        $tscn = Build4DirScene $sceneName $uid $scriptRes $animList $DIRS4 $def.r $def.h
        Write-Scene $scenePath $tscn
        Write-Scene $csPath (MobCs $sceneName)
    }
}

# Generate standard monsters v1-v3
foreach ($def in $monsterDefs) {
    Build-MonsterVariants $def @(1, 2, 3)
}

# ---------- Goblin ----------
for ($v = 1; $v -le 3; $v++) {
    $vSuffix   = if ($v -eq 1) { "" } else { "$v" }
    $sceneName = "Goblin${vSuffix}"
    $uid       = "mob_goblin_v${v}"
    $animList  = @()
    for ($i = 0; $i -lt $goblinAnimKeys.Count; $i++) {
        $key = $goblinAnimKeys[$i]
        # v1: "{Anim}0_with_shadow.png", v2/3: "{Anim}_with_shadow.png"
        $capKey = (Get-Culture).TextInfo.ToTitleCase($key.Replace("_"," ")).Replace(" ","_")
        if ($v -eq 1) { $file = "monster/goblin/1/${capKey}0_with_shadow.png" }
        else          { $file = "monster/goblin/${v}/${capKey}_with_shadow.png" }

        $animList += @{
            key   = $key
            path  = "$ASSETS/$file"
            extId = "tex_$($key.Replace('_',''))"
            n     = $goblinN[$i]
            fw    = 64; fh = 64
            loop  = $goblinLoops[$i]
            spd   = $goblinSpds[$i]
        }
    }
    $sub       = "Monster/Goblin"
    $sceneDir  = "$MOBS\$sub"
    $scriptRes = "res://Mobs/$($sub.Replace('\','/'))/$(${sceneName}).cs"
    Write-Scene "$sceneDir\${sceneName}.tscn" (Build4DirScene $sceneName $uid $scriptRes $animList $DIRS4 8 18)
    Write-Scene "$sceneDir\${sceneName}.cs"   (MobCs $sceneName)
}

# ---------- Orc ----------
# Anim names lowercase, run_attack file is "run_attack_front" for v1 only
$orcFileMap = @{
    "idle"        = "idle"
    "walk"        = "walk"
    "run"         = "run"
    "attack"      = "attack"
    "hurt"        = "hurt"
    "death"       = "death"
    "run_attack"  = "run_attack"   # v2/3 (v1 has "run_attack_front")
    "walk_attack" = "walk_attack"
}
for ($v = 1; $v -le 3; $v++) {
    $vSuffix   = if ($v -eq 1) { "" } else { "$v" }
    $sceneName = "Orc${vSuffix}"
    $uid       = "mob_orc_v${v}"
    $animList  = @()
    for ($i = 0; $i -lt $orcAnimKeys.Count; $i++) {
        $key     = $orcAnimKeys[$i]
        $fileKey = $orcFileMap[$key]
        # v1 run_attack has "front" in name
        if ($v -eq 1 -and $key -eq "run_attack") { $fileKey = "run_attack_front" }
        $file = "monster/orc/${v}/orc${v}_${fileKey}_with_shadow.png"
        $animList += @{
            key   = $key
            path  = "$ASSETS/$file"
            extId = "tex_$($key.Replace('_',''))"
            n     = $orcN[$i]
            fw    = 64; fh = 64
            loop  = $orcLoops[$i]
            spd   = $orcSpds[$i]
        }
    }
    $sub       = "Monster/Orc"
    $sceneDir  = "$MOBS\$sub"
    $scriptRes = "res://Mobs/$($sub.Replace('\','/'))/$(${sceneName}).cs"
    Write-Scene "$sceneDir\${sceneName}.tscn" (Build4DirScene $sceneName $uid $scriptRes $animList $DIRS4 10 20)
    Write-Scene "$sceneDir\${sceneName}.cs"   (MobCs $sceneName)
}

# ================================================================
# SLIMES — 3 variants per slime type
# Slimes are in monster/slime/{type}/Slime{N}_*
# ================================================================
$slimeDefs = @(
    @{n="Slime";        sub="Monster/Slime";        type="slime";          fileN=1; fw=64;  fh=64;  r=10; h=16;
      anims=@(@{k="idle";n=6;l=$true;s=6},@{k="walk";n=8;l=$true;s=8},@{k="run";n=8;l=$true;s=10},
              @{k="attack";n=10;l=$false;s=8},@{k="hurt";n=5;l=$false;s=8},@{k="death";n=10;l=$false;s=6})}
    @{n="SlimeBomb";    sub="Monster/SlimeBomb";    type="bomb";           fileN=1; fw=64;  fh=64;  r=10; h=16;
      anims=@(@{k="idle";n=6;l=$true;s=6},@{k="walk";n=8;l=$true;s=8},@{k="run";n=8;l=$true;s=10},
              @{k="attack";n=10;l=$false;s=8},@{k="hurt";n=5;l=$false;s=8},@{k="death";n=8;l=$false;s=6})}
    @{n="SlimeElectric";sub="Monster/SlimeElectric";type="electric";       fileN=2; fw=64;  fh=64;  r=10; h=16;
      anims=@(@{k="idle";n=6;l=$true;s=6},@{k="walk";n=8;l=$true;s=8},@{k="run";n=8;l=$true;s=10},
              @{k="attack";n=10;l=$false;s=8},@{k="hurt";n=5;l=$false;s=8},@{k="death";n=10;l=$false;s=6})}
    @{n="SlimeEvil";    sub="Monster/SlimeEvil";    type="evil";           fileN=2; fw=64;  fh=64;  r=10; h=16;
      anims=@(@{k="idle";n=6;l=$true;s=6},@{k="walk";n=8;l=$true;s=8},@{k="run";n=8;l=$true;s=10},
              @{k="attack";n=11;l=$false;s=8},@{k="hurt";n=5;l=$false;s=8},@{k="death";n=10;l=$false;s=6})}
    @{n="SlimeFire";    sub="Monster/SlimeFire";    type="fire";           fileN=2; fw=64;  fh=64;  r=10; h=16;
      anims=@(@{k="idle";n=6;l=$true;s=6},@{k="walk";n=8;l=$true;s=8},@{k="run";n=8;l=$true;s=10},
              @{k="attack";n=10;l=$false;s=8},@{k="hurt";n=5;l=$false;s=8},@{k="death";n=10;l=$false;s=6})}
    @{n="SlimeIce";     sub="Monster/SlimeIce";     type="ice";            fileN=1; fw=64;  fh=64;  r=10; h=16;
      anims=@(@{k="idle";n=6;l=$true;s=6},@{k="walk";n=8;l=$true;s=8},@{k="run";n=8;l=$true;s=10},
              @{k="attack";n=10;l=$false;s=8},@{k="hurt";n=5;l=$false;s=8},@{k="death";n=10;l=$false;s=6})}
    @{n="SlimeLava";    sub="Monster/SlimeLava";    type="lava";           fileN=3; fw=64;  fh=64;  r=10; h=16;
      anims=@(@{k="idle";n=6;l=$true;s=6},@{k="walk";n=8;l=$true;s=8},@{k="run";n=8;l=$true;s=10},
              @{k="attack";n=9;l=$false;s=8},@{k="hurt";n=5;l=$false;s=8},@{k="death";n=10;l=$false;s=6})}
    @{n="SlimeCrystal"; sub="Monster/SlimeCrystal"; type="glowing-crystal";fileN=3; fw=64;  fh=64;  r=10; h=16;
      anims=@(@{k="idle";n=6;l=$true;s=6},@{k="walk";n=8;l=$true;s=8},@{k="run";n=8;l=$true;s=10},
              @{k="attack";n=9;l=$false;s=8},@{k="hurt";n=5;l=$false;s=8},@{k="death";n=10;l=$false;s=6})}
    @{n="SlimeDevil";   sub="Monster/SlimeDevil";   type="devil";          fileN=3; fw=64;  fh=64;  r=10; h=16;
      anims=@(@{k="idle";n=6;l=$true;s=6},@{k="walk";n=8;l=$true;s=8},@{k="run";n=8;l=$true;s=10},
              @{k="attack";n=10;l=$false;s=8},@{k="hurt";n=5;l=$false;s=8},@{k="death";n=10;l=$false;s=6})}
    # Boss slimes — 128px, only 1 "variant" each (no v2/v3)
    @{n="SlimeCloud";   sub="Monster/SlimeCloud";   type="cloud";          fileN=3; fw=128; fh=128; r=20; h=36;
      anims=@(@{k="idle";n=6;l=$true;s=5},@{k="walk";n=8;l=$true;s=7},@{k="run";n=8;l=$true;s=9},
              @{k="attack";n=10;l=$false;s=7},@{k="attack2";n=10;l=$false;s=7},
              @{k="hurt";n=4;l=$false;s=8},@{k="death";n=10;l=$false;s=6})
      bossFile="Slime_boss3" }
    @{n="SlimeWater";   sub="Monster/SlimeWater";   type="water";          fileN=1; fw=128; fh=128; r=20; h=36;
      anims=@(@{k="idle";n=6;l=$true;s=5},@{k="walk";n=8;l=$true;s=7},@{k="run";n=8;l=$true;s=9},
              @{k="attack";n=10;l=$false;s=7},@{k="attack2";n=9;l=$false;s=7},
              @{k="hurt";n=4;l=$false;s=8},@{k="death";n=10;l=$false;s=6})
      bossFile="Slime_boss1" }
    @{n="SlimeEarth";   sub="Monster/SlimeEarth";   type="earth";          fileN=2; fw=128; fh=128; r=20; h=36;
      anims=@(@{k="idle";n=6;l=$true;s=5},@{k="walk";n=8;l=$true;s=7},@{k="run";n=8;l=$true;s=9},
              @{k="attack";n=10;l=$false;s=7},@{k="attack2";n=10;l=$false;s=7},
              @{k="hurt";n=4;l=$false;s=8},@{k="death";n=10;l=$false;s=6})
      bossFile="Slime_boss2" }
)

foreach ($sd in $slimeDefs) {
    $animList = $sd.anims | ForEach-Object {
        $filePrefix = if ($sd.ContainsKey("bossFile")) { $sd.bossFile } else { "Slime$($sd.fileN)" }
        $capKey = (Get-Culture).TextInfo.ToTitleCase($_.k)
        $file = "monster/slime/$($sd.type)/${filePrefix}_${capKey}_with_shadow.png"
        @{ key=$_.k; path="$ASSETS/$file"; extId="tex_$($_.k.Replace('_',''))"; n=$_.n; fw=$sd.fw; fh=$sd.fh; loop=$_.l; spd=$_.s }
    }
    $sceneDir  = "$MOBS\$($sd.sub)"
    $scriptRes = "res://Mobs/$($sd.sub.Replace('\','/'))/$(${sd}.n).cs"
    Write-Scene "$sceneDir\$($sd.n).tscn" (Build4DirScene $sd.n "mob_$($sd.n.ToLower())" $scriptRes $animList $DIRS4 $sd.r $sd.h)
    Write-Scene "$sceneDir\$($sd.n).cs"   (MobCs $sd.n)
}

# ================================================================
# HUNT ANIMALS — 4-dir, 32x32
# ================================================================
$huntAnimals = @(
    @{n="Boar"; uid="mob_boar"; sub="HuntAnimal/Boar"; fw=32; fh=32; r=12; h=18; anims=@(
        @{k="idle";   f="Boar_Idle.png";   n=4; l=$true;  s=6}
        @{k="walk";   f="Boar_Walk.png";   n=6; l=$true;  s=8}
        @{k="run";    f="Boar_Run.png";    n=5; l=$true;  s=10}
        @{k="attack"; f="Boar_Attack.png"; n=5; l=$false; s=8}
        @{k="hurt";   f="Boar_Hurt.png";   n=4; l=$false; s=8}
        @{k="death";  f="Boar_Death.png";  n=6; l=$false; s=6}
    )}
    @{n="Deer"; uid="mob_deer"; sub="HuntAnimal/Deer"; fw=32; fh=32; r=10; h=18; anims=@(
        @{k="idle";  f="Deer_Idle.png";  n=4; l=$true;  s=6}
        @{k="walk";  f="Deer_Walk.png";  n=6; l=$true;  s=8}
        @{k="run";   f="Deer_Run.png";   n=6; l=$true;  s=10}
        @{k="hurt";  f="Deer_Hurt.png";  n=4; l=$false; s=8}
        @{k="death"; f="Deer_Death.png"; n=7; l=$false; s=6}
    )}
    @{n="Fox"; uid="mob_fox"; sub="HuntAnimal/Fox"; fw=32; fh=32; r=8; h=14; anims=@(
        @{k="idle";  f="Fox_Idle.png";  n=4; l=$true;  s=6}
        @{k="walk";  f="Fox_walk.png";  n=6; l=$true;  s=8}
        @{k="run";   f="Fox_Run.png";   n=6; l=$true;  s=10}
        @{k="hurt";  f="Fox_Hurt.png";  n=4; l=$false; s=8}
        @{k="death"; f="Fox_Death.png"; n=6; l=$false; s=6}
    )}
    @{n="Hare"; uid="mob_hare"; sub="HuntAnimal/Hare"; fw=32; fh=32; r=7; h=12; anims=@(
        @{k="idle";  f="Hare_Idle.png";  n=4; l=$true;  s=6}
        @{k="walk";  f="Hare_Walk.png";  n=5; l=$true;  s=8}
        @{k="run";   f="Hare_Run.png";   n=6; l=$true;  s=10}
        @{k="hurt";  f="Hare_Hurt.png";  n=4; l=$false; s=8}
        @{k="death"; f="Hare_Death.png"; n=6; l=$false; s=6}
    )}
    @{n="BlackGrouse"; uid="mob_blackgrouse"; sub="HuntAnimal/BlackGrouse"; fw=32; fh=32; r=7; h=10; anims=@(
        @{k="idle";   f="Black_grouse_Idle.png";   n=4; l=$true;  s=6}
        @{k="walk";   f="Black_grouse_Walk.png";   n=6; l=$true;  s=8}
        @{k="flight"; f="Black_grouse_Flight.png"; n=6; l=$true;  s=10}
        @{k="hurt";   f="Black_grouse_Hurt.png";   n=4; l=$false; s=8}
    )}
)
foreach ($a in $huntAnimals) {
    $animList = $a.anims | ForEach-Object {
        @{key=$_.k; path="$ASSETS/Mobs/hunt-animal/$($_.f)"; extId="tex_$($_.k)"; n=$_.n; fw=$a.fw; fh=$a.fh; loop=$_.l; spd=$_.s}
    }
    $sceneDir  = "$MOBS\$($a.sub)"
    $scriptRes = "res://Mobs/$($a.sub.Replace('\','/'))/$(${a}.n).cs"
    Write-Scene "$sceneDir\$($a.n).tscn" (Build4DirScene $a.n $a.uid $scriptRes $animList $DIRS4 $a.r $a.h)
    Write-Scene "$sceneDir\$($a.n).cs"   (MobCs $a.n)
}

# ================================================================
# FARM ANIMALS
# village-farm-animal sprites use 2x2 grid layout (n=1 per direction cell):
#   Col 0=left half, Col 1=right half
#   Row 0=top half (down,up), Row 1=bottom half (left,right)
# ================================================================

# Farm animals: simple Sprite2D with full texture (1 image, 1 frame, no direction variants)
function BuildFarmStaticScene($name, $uid, $scriptResPath, $texPath, $capR, $capH) {
    $sb = [System.Text.StringBuilder]::new()
    $null = $sb.AppendLine("[gd_scene format=4 uid=`"uid://${uid}`"]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[ext_resource type=`"Texture2D`" path=`"$texPath`" id=`"tex_main`"]")
    $null = $sb.AppendLine("[ext_resource type=`"Script`" path=`"$scriptResPath`" id=`"scr_$name`"]")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[sub_resource type=`"CapsuleShape2D`" id=`"Cap_$name`"]")
    $null = $sb.AppendLine("radius = $capR")
    $null = $sb.AppendLine("height = $capH")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"$name`" type=`"CharacterBody2D`"]")
    $null = $sb.AppendLine("script = ExtResource(`"scr_$name`")")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"Sprite2D`" type=`"Sprite2D`" parent=`".`"]")
    $null = $sb.AppendLine("texture = ExtResource(`"tex_main`")")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"CollisionShape2D`" type=`"CollisionShape2D`" parent=`".`"]")
    $null = $sb.AppendLine("shape = SubResource(`"Cap_$name`")")
    $sb.ToString()
}

$farmAnimals4 = @(
    @{n="Cat";    sub="FarmAnimal/Cat";    r=6;  h=10; f="Mobs/village-farm-animal/Cat_animation_without_shadow.png"}
    @{n="Dog";    sub="FarmAnimal/Dog";    r=8;  h=14; f="Mobs/village-farm-animal/Dog_animation_without_shadow.png"}
    @{n="Calt";   sub="FarmAnimal/Calt";   r=10; h=18; f="Mobs/village-farm-animal/Calt_animation_without_shadow.png"}
    @{n="Buffalo";sub="FarmAnimal/Buffalo";r=18; h=32; f="Mobs/village-farm-animal/Buffalo_animation_without_shadow.png"}
    @{n="Donkey"; sub="FarmAnimal/Donkey"; r=14; h=28; f="Mobs/village-farm-animal/Donkey_animation_without_shadow.png"}
)
foreach ($a in $farmAnimals4) {
    $sceneDir  = "$MOBS\$($a.sub)"
    $scriptRes = "res://Mobs/$($a.sub.Replace('\','/'))/$(${a}.n).cs"
    Write-Scene "$sceneDir\$($a.n).tscn" (BuildFarmStaticScene $a.n "mob_$($a.n.ToLower())" $scriptRes "$ASSETS/$($a.f)" $a.r $a.h)
    Write-Scene "$sceneDir\$($a.n).cs"   (FarmAnimalCs $a.n)
}

$farmAnimals3 = @(
    @{n="Chicken";sub="FarmAnimal/Chicken";fw=64;  fh=64;  r=5;  h=8;  frames=4; f="Mobs/farm-animal/Chicken_animation.png"}
    @{n="Pig";    sub="FarmAnimal/Pig";    fw=64;  fh=64;  r=8;  h=14; frames=4; f="Mobs/farm-animal/Pig_animation.png"}
    @{n="Cow";    sub="FarmAnimal/Cow";    fw=128; fh=128; r=16; h=28; frames=4; f="Mobs/farm-animal/Cow_animation.png"}
)
foreach ($a in $farmAnimals3) {
    $animList = @(@{key="idle";path="$ASSETS/$($a.f)";extId="tex_idle";n=$a.frames;fw=$a.fw;fh=$a.fh;loop=$true;spd=6})
    $sceneDir  = "$MOBS\$($a.sub)"
    $scriptRes = "res://Mobs/$($a.sub.Replace('\','/'))/$(${a}.n).cs"
    Write-Scene "$sceneDir\$($a.n).tscn" (Build4DirScene $a.n "mob_$($a.n.ToLower())" $scriptRes $animList $DIRS3 $a.r $a.h)
    Write-Scene "$sceneDir\$($a.n).cs"   (FarmAnimalCs $a.n)
}

# ================================================================
# HUMAN FIGHTERS (Assets/Charakter/Fighter/)
# FighterSword1-5: 384x256 = 64px, 4-dir, 6 frames, single "walk" anim
# Archer1-3:       192x32  = 32px, 1-dir (down), 6 frames, "idle" anim
# ================================================================
for ($v = 1; $v -le 5; $v++) {
    $sceneName = if ($v -eq 1) { "FighterSword" } else { "FighterSword$v" }
    $uid       = "mob_fightersword_v${v}"
    $animList  = @(@{key="walk";path="$ASSETS/Charakter/Fighter/Fighter_sword${v}_with_shadow.png";extId="tex_walk";n=6;fw=64;fh=64;loop=$true;spd=8})
    $sub       = "Human/FighterSword"
    $sceneDir  = "$MOBS\$sub"
    $scriptRes = "res://Mobs/$($sub.Replace('\','/'))/$(${sceneName}).cs"
    Write-Scene "$sceneDir\${sceneName}.tscn" (Build4DirScene $sceneName $uid $scriptRes $animList $DIRS4 10 20)
    Write-Scene "$sceneDir\${sceneName}.cs"   (MobCs $sceneName)
}

for ($v = 1; $v -le 3; $v++) {
    $sceneName = if ($v -eq 1) { "Archer" } else { "Archer$v" }
    $uid       = "mob_archer_v${v}"
    # Archer: 192x32 single row — 1 direction "down", 6 frames at 32x32
    $animList  = @(@{key="idle";path="$ASSETS/Charakter/Fighter/Archer${v}_with_shadow.png";extId="tex_idle";n=6;fw=32;fh=32;loop=$true;spd=8})
    $sub       = "Human/Archer"
    $sceneDir  = "$MOBS\$sub"
    $scriptRes = "res://Mobs/$($sub.Replace('\','/'))/$(${sceneName}).cs"
    Write-Scene "$sceneDir\${sceneName}.tscn" (Build4DirScene $sceneName $uid $scriptRes $animList $DIRS1 8 16)
    Write-Scene "$sceneDir\${sceneName}.cs"   (MobCs $sceneName)
}

Write-Host "`n=== Done ===" -ForegroundColor Green
