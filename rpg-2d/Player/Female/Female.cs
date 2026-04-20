using Godot;

public partial class Female : CharacterBody2D
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
