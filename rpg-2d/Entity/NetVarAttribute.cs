using System;

namespace RPG2d.Entity;

[AttributeUsage(AttributeTargets.Field)]
public class NetVarAttribute(float tolerance = 0f) : Attribute
{
    public float Tolerance  { get; set; } = tolerance;
}