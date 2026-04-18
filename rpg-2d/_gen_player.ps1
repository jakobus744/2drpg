# === Player Scene Generator ===
# Creates: Player/Female/Female.tscn + .cs
#          Player/Main/PlayerMain.tscn + .cs
param([switch]$DryRun)

$BASE   = "C:\Hochschule\6 Semester\Projekt\2drpg\rpg-2d"
$PLAYER = "$BASE\Player"
$ASSETS = "res://Assets"
$NoBom  = New-Object System.Text.UTF8Encoding $false

function Write-Scene($path, $content) {
    if ($DryRun) { Write-Host "[DRY] $path"; return }
    $dir = Split-Path $path -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($path, $content, $NoBom)
    Write-Host "OK  $path"
}

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

function Build4DirPlayerScene($name, $uid, $scriptResPath, $anims) {
    $dirs = @("down","up","left","right")
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
    $null = $sb.AppendLine("radius = 10")
    $null = $sb.AppendLine("height = 22")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"$name`" type=`"CharacterBody2D`"]")
    $null = $sb.AppendLine("script = ExtResource(`"scr_$name`")")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"AnimatedSprite2D`" type=`"AnimatedSprite2D`" parent=`".`"]")
    $null = $sb.AppendLine("sprite_frames = SubResource(`"$sfId`")")
    $null = $sb.AppendLine("animation = &`"idle_down`"")
    $null = $sb.AppendLine("")
    $null = $sb.AppendLine("[node name=`"CollisionShape2D`" type=`"CollisionShape2D`" parent=`".`"]")
    $null = $sb.AppendLine("shape = SubResource(`"$capId`")")
    $sb.ToString()
}

function PlayerCs($name) {
    @"
using Godot;

public partial class $name : CharacterBody2D
{
    private AnimatedSprite2D _sprite;
    private string _dir = "down";
    private float _speed = 120f;

    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        PlayAnim("idle");
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 input = Vector2.Zero;
        if (Input.IsActionPressed("ui_right")) input.X += 1;
        if (Input.IsActionPressed("ui_left"))  input.X -= 1;
        if (Input.IsActionPressed("ui_down"))  input.Y += 1;
        if (Input.IsActionPressed("ui_up"))    input.Y -= 1;

        Velocity = input.Normalized() * _speed;
        MoveAndSlide();

        if (Velocity.Length() > 0)
        {
            UpdateDir(Velocity);
            PlayAnim("run");
        }
        else
        {
            PlayAnim("idle");
        }
    }

    private void UpdateDir(Vector2 v)
    {
        if (Mathf.Abs(v.X) > Mathf.Abs(v.Y))
            _dir = v.X > 0 ? "right" : "left";
        else
            _dir = v.Y > 0 ? "down" : "up";
    }

    public void PlayAnim(string anim)
    {
        string full = anim + "_" + _dir;
        if (_sprite.SpriteFrames.HasAnimation(full))
            _sprite.Play(full);
    }
}
"@
}

# ============================================================
# Female — fw=64, fh=64, 4 dirs
# Files: Sword_{Key}_with_shadow.png (attack = lowercase)
# ============================================================
$femaleBase = "Charakter/Player/female"

# Frame counts derived from actual image widths (all 256px height → 4 rows × 64px)
# Idle=768 → n=12  Walk=384 → n=6  Run=512 → n=8  Attack=512 → n=8
# Hurt=320 → n=5   Death=448 → n=7  Run_Attack=512 → n=8  Walk_Attack=384 → n=6
$femaleAnims = @(
    @{key="idle";        extId="tex_idle";       path="$ASSETS/$femaleBase/Sword_Idle_with_shadow.png";       n=12; fw=64; fh=64; loop=$true;  spd=8}
    @{key="walk";        extId="tex_walk";       path="$ASSETS/$femaleBase/Sword_Walk_with_shadow.png";       n=6;  fw=64; fh=64; loop=$true;  spd=8}
    @{key="run";         extId="tex_run";        path="$ASSETS/$femaleBase/Sword_Run_with_shadow.png";        n=8;  fw=64; fh=64; loop=$true;  spd=10}
    @{key="attack";      extId="tex_attack";     path="$ASSETS/$femaleBase/Sword_attack_with_shadow.png";     n=8;  fw=64; fh=64; loop=$false; spd=10}
    @{key="hurt";        extId="tex_hurt";       path="$ASSETS/$femaleBase/Sword_Hurt_with_shadow.png";       n=5;  fw=64; fh=64; loop=$false; spd=8}
    @{key="death";       extId="tex_death";      path="$ASSETS/$femaleBase/Sword_Death_with_shadow.png";      n=7;  fw=64; fh=64; loop=$false; spd=6}
    @{key="run_attack";  extId="tex_runattack";  path="$ASSETS/$femaleBase/Sword_Run_Attack_with_shadow.png"; n=8;  fw=64; fh=64; loop=$false; spd=10}
    @{key="walk_attack"; extId="tex_walkattack"; path="$ASSETS/$femaleBase/Sword_Walk_Attack_with_shadow.png";n=6;  fw=64; fh=64; loop=$false; spd=8}
)

$femaleDir = "$PLAYER\Female"
Write-Scene "$femaleDir\Female.tscn" (Build4DirPlayerScene "Female" "player_female" "res://Player/Female/Female.cs" $femaleAnims)
Write-Scene "$femaleDir\Female.cs"   (PlayerCs "Female")

# ============================================================
# Main Player (Swordsman_lvl2) — same dims as female
# Files: Swordsman_lvl2_{Key}_without_shadow.png (attack = lowercase)
# ============================================================
$mainBase = "Charakter/Player/main"

$mainAnims = @(
    @{key="idle";        extId="tex_idle";       path="$ASSETS/$mainBase/Swordsman_lvl2_Idle_without_shadow.png";       n=12; fw=64; fh=64; loop=$true;  spd=8}
    @{key="walk";        extId="tex_walk";       path="$ASSETS/$mainBase/Swordsman_lvl2_Walk_without_shadow.png";       n=6;  fw=64; fh=64; loop=$true;  spd=8}
    @{key="run";         extId="tex_run";        path="$ASSETS/$mainBase/Swordsman_lvl2_Run_without_shadow.png";        n=8;  fw=64; fh=64; loop=$true;  spd=10}
    @{key="attack";      extId="tex_attack";     path="$ASSETS/$mainBase/Swordsman_lvl2_attack_without_shadow.png";     n=8;  fw=64; fh=64; loop=$false; spd=10}
    @{key="hurt";        extId="tex_hurt";       path="$ASSETS/$mainBase/Swordsman_lvl2_Hurt_without_shadow.png";       n=5;  fw=64; fh=64; loop=$false; spd=8}
    @{key="death";       extId="tex_death";      path="$ASSETS/$mainBase/Swordsman_lvl2_Death_without_shadow.png";      n=7;  fw=64; fh=64; loop=$false; spd=6}
    @{key="run_attack";  extId="tex_runattack";  path="$ASSETS/$mainBase/Swordsman_lvl2_Run_Attack_without_shadow.png"; n=8;  fw=64; fh=64; loop=$false; spd=10}
    @{key="walk_attack"; extId="tex_walkattack"; path="$ASSETS/$mainBase/Swordsman_lvl2_Walk_Attack_without_shadow.png";n=6;  fw=64; fh=64; loop=$false; spd=8}
)

$mainDir = "$PLAYER\Main"
Write-Scene "$mainDir\PlayerMain.tscn" (Build4DirPlayerScene "PlayerMain" "player_main" "res://Player/Main/PlayerMain.cs" $mainAnims)
Write-Scene "$mainDir\PlayerMain.cs"   (PlayerCs "PlayerMain")

Write-Host "`n=== Player scenes done ===" -ForegroundColor Green
