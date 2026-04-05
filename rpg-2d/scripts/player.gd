extends CharacterBody2D

const SPEED_WALK := 70.0
const SPEED_RUN := 100.0

enum MoveState { IDLE, WALK, RUN }

var move_state: MoveState = MoveState.IDLE
var last_move_state: MoveState = MoveState.IDLE

var facing_direction: Vector2 = Vector2.DOWN
var is_attacking: bool = false

# Für "letzte Taste gewinnt"
var active_direction: String = ""  # "up", "down", "left", "right", oder ""
var pressed_directions: Array = []  # Reihenfolge der gedrückten Tasten

@onready var anim: AnimatedSprite2D = $AnimatedSprite2D

func _ready():
	anim.animation_finished.connect(_on_animation_finished)


# HAUPTSCHLEIFE
func _physics_process(_delta):
	handle_direction_input()
	handle_attack_input()
	handle_movement()
	update_animation()
	move_and_slide()

# ============================================================
# RICHTUNGS-INPUT (Letzte Taste gewinnt)
# ============================================================
func handle_direction_input():
	# Taste neu gedrückt? -> Ans Ende der Liste
	if Input.is_action_just_pressed("up"):
		add_direction("up")
	if Input.is_action_just_pressed("down"):
		add_direction("down")
	if Input.is_action_just_pressed("left"):
		add_direction("left")
	if Input.is_action_just_pressed("right"):
		add_direction("right")
	
	# Taste losgelassen? -> Aus Liste entfernen
	if Input.is_action_just_released("up"):
		remove_direction("up")
	if Input.is_action_just_released("down"):
		remove_direction("down")
	if Input.is_action_just_released("left"):
		remove_direction("left")
	if Input.is_action_just_released("right"):
		remove_direction("right")
	
	# Aktive Richtung = letzte in der Liste (oder leer)
	if pressed_directions.size() > 0:
		active_direction = pressed_directions.back()
	else:
		active_direction = ""

func add_direction(dir: String):
	# Nicht doppelt hinzufügen
	if dir not in pressed_directions:
		pressed_directions.append(dir)

func remove_direction(dir: String):
	pressed_directions.erase(dir)

# ============================================================
# ATTACK-INPUT
# ============================================================
func handle_attack_input():
	# Nur bei NEUEM Klick, nicht bei gehaltenem
	if Input.is_action_just_pressed("attack"):
		start_attack()

func start_attack():
	# Nicht doppelt attackieren
	if is_attacking:
		return
	
	is_attacking = true
	
	# Sofort richtige Animation abspielen
	var dir_name := get_direction_name()
	var attack_anim := get_attack_animation_name(dir_name)
	anim.play(attack_anim)

func get_attack_animation_name(dir_name: String) -> String:
	# Welche Attack-Animation basierend auf Bewegung?
	match move_state:
		MoveState.RUN:
			return "run_attack_" + dir_name
		MoveState.WALK:
			return "walk_attack_" + dir_name
		_:
			return "attack_" + dir_name

# ============================================================
# BEWEGUNG BERECHNEN
# ============================================================
func handle_movement():
	# Richtung zu Vector umwandeln
	var input_dir := direction_string_to_vector(active_direction)
	
	# Rennen gedrückt?
	var is_running := Input.is_action_pressed("run")
	
	# State und Velocity setzen
	if input_dir == Vector2.ZERO:
		# Stehenbleiben
		last_move_state = move_state
		move_state = MoveState.IDLE
		velocity = Vector2.ZERO
	else:
		# Bewegen
		facing_direction = input_dir
		
		if is_running:
			move_state = MoveState.RUN
			velocity = input_dir * SPEED_RUN
		else:
			move_state = MoveState.WALK
			velocity = input_dir * SPEED_WALK

func direction_string_to_vector(dir: String) -> Vector2:
	# Konvertiert "up" zu Vector2.UP etc.
	match dir:
		"up":
			return Vector2.UP
		"down":
			return Vector2.DOWN
		"left":
			return Vector2.LEFT
		"right":
			return Vector2.RIGHT
		_:
			return Vector2.ZERO

# ============================================================
# ANIMATION
# ============================================================
func update_animation():
	# Während Attack: Animation läuft schon, nicht unterbrechen!
	if is_attacking:
		return
	
	var dir_name := get_direction_name()
	
	match move_state:
		MoveState.IDLE:
			play_idle_animation(dir_name)
		MoveState.WALK:
			anim.play("walk_" + dir_name)
		MoveState.RUN:
			anim.play("run_" + dir_name)

func play_idle_animation(dir_name: String):
	# Nach Run -> idle.2, sonst idle.1 (mit Fallback)
	if last_move_state == MoveState.RUN:
		anim.play("idle_" + dir_name + ".2")
	else:
		# Versuche .1, falls nicht vorhanden nimm ohne Suffix
		var idle_anim := "idle_" + dir_name + ".1"
		if not anim.sprite_frames.has_animation(idle_anim):
			idle_anim = "idle_" + dir_name
		anim.play(idle_anim)

func get_direction_name() -> String:
	# Vector zu String für Animation-Namen
	if facing_direction == Vector2.RIGHT:
		return "right"
	elif facing_direction == Vector2.LEFT:
		return "left"
	elif facing_direction == Vector2.UP:
		return "up"
	else:
		return "down"

# ============================================================
# SIGNAL CALLBACKS
# ============================================================
func _on_animation_finished():
	# Nur Attack-Animationen beenden den Attack-State
	if "attack" in anim.animation:
		is_attacking = false
