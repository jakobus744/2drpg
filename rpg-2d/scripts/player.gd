extends CharacterBody2D


const speed = 100
const speedmax = 150
var current_dir = "down"

func _physics_process(delta):
	player_movement(delta)

func player_movement(_delta):
	
	if Input.is_action_pressed("right"):
		current_dir = "right"
		play_anim(1)
		velocity.x = speed
		velocity.y = 0
	elif Input.is_action_pressed("left"):
		current_dir = "left"
		play_anim(1)
		velocity.x = -speed
		velocity.y = 0
	elif Input.is_action_pressed("down"):
		current_dir = "down"
		play_anim(1)
		velocity.x = 0
		velocity.y = speed
	elif Input.is_action_pressed("up"):
		current_dir = "up"
		play_anim(1)
		velocity.x = 0
		velocity.y = -speed
		
	elif Input.is_action_pressed("run_left"):
		current_dir = "run_left"
		play_anim(1)
		velocity.x = 0
		velocity.y = -speedmax
		
			
	elif Input.is_action_pressed("attack"):
		current_dir = "attack"
		play_anim(1)
		velocity.x = 0
		velocity.y = 0
	
	
	else:
		play_anim(0)
		velocity.x = 0
		velocity.y = 0
	
	move_and_slide()


func play_anim(movement):
	var dir = current_dir
	var anim = $AnimatedSprite2D
	
	if dir == "right":

		if movement == 1:
			anim.play("walk_right")
		elif movement == 0:
			anim.play("idle_right.2")
	
	if dir == "left":

		if movement == 1:
			anim.play("walk_left")
		elif movement == 0:
			anim.play("idle_left.2")
		
	if dir == "up":
		if movement == 1:
			anim.play("walk_up")
		elif movement == 0:
			anim.play("idle_up")
		
	if dir == "down":
		if movement == 1:
			anim.play("walk_down")
		elif movement == 0:
			anim.play("idle_down.2")
			
	if dir == "run_left":
		if movement == 1:
			anim.play("run_left")
		elif movement == 0:
			anim.play("idle_left.1")
	
	if dir == "attack":
		if movement == 1:
			anim.play("run_attack_left")
		elif movement == 0:
			anim.play("idele_left.2")
