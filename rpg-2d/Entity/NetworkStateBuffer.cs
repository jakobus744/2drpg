using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace RPG2d.Entity;

public class NetworkStateBuffer<T> where T : struct
{
    private const int BufferSize = 128;
    private readonly T[] _history = new T[BufferSize];
    
    private readonly List<(FieldInfo Field, float Tolerance)> _networkedFields = new();
    
    public NetworkStateBuffer()
    {
        // Einmalig mit Reflection die Typen holen und Cachen
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var field in fields)
        {
            var attribute = field.GetCustomAttribute<NetVarAttribute>();
            if (attribute != null)
            {
                _networkedFields.Add((field, attribute.Tolerance));
            }
        }
    }
    
    public T Get(uint tick) => _history[tick % BufferSize];
    public void Set(uint tick, T state) => _history[tick % BufferSize] = state;
    
    public bool IsDesynced(T serverState, T predictedState)
    {
        foreach (var (field, tolerance) in _networkedFields)
        {
            var serverVal = field.GetValue(serverState);
            var predictedVal = field.GetValue(predictedState);

            switch (serverVal)
            {
                case Vector2 sVec when predictedVal is Vector2 pVec && sVec.DistanceSquaredTo(pVec) > tolerance * tolerance:
                case float sFlt when predictedVal is float pFlt && Math.Abs(sFlt - pFlt) > tolerance:
                case int sInt when predictedVal is int pInt  && Math.Abs(sInt - pInt) > tolerance:
                case bool sBool when  predictedVal is bool pBool && sBool != pBool:
                    return true;
            }
        }

        return false;
    }
}