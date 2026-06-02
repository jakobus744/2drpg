using System;
using System.Collections.Generic;
using System.IO;
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
                case uint sUint when predictedVal is uint pUint && sUint != pUint:
                case bool sBool when  predictedVal is bool pBool && sBool != pBool:
                case byte sByte when predictedVal is byte pByte && sByte != pByte:
                case long sLong when predictedVal is long pLong && Math.Abs(sLong - pLong) > tolerance:
                case double sDouble when predictedVal is double pDouble && Math.Abs(sDouble - pDouble) > tolerance:
                case short sShort when predictedVal is short pShort && Math.Abs(sShort - pShort) > tolerance:
                case string sStr when predictedVal is string pStr && sStr != pStr:
                    return true;
            }
        }

        return false;
    }

    public byte[] ToBytes(T state)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        foreach (var (field, _) in _networkedFields)
        {
            var val = field.GetValue(state);
            switch (val)
            {
                case Vector2 v: writer.Write(v.X); writer.Write(v.Y); break;
                case float f:   writer.Write(f); break;
                case int i:     writer.Write(i); break;
                case uint ui:   writer.Write(ui); break;
                case bool b:    writer.Write(b); break;
                case byte by:   writer.Write(by); break;
                case long l:    writer.Write(l); break;
                case double d:  writer.Write(d); break;
                case short s:   writer.Write(s); break;
                case string s:  writer.Write(s ?? ""); break;
                default:
                    throw new NotSupportedException(
                        $"Type {val?.GetType()} not supported for NetVar serialization");
            }
        }

        return stream.ToArray();
    }

    public T FromBytes(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        object boxed = default(T);
        foreach (var (field, _) in _networkedFields)
        {
            object val = field.FieldType switch
            {
                _ when field.FieldType == typeof(Vector2) => new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                _ when field.FieldType == typeof(float)   => reader.ReadSingle(),
                _ when field.FieldType == typeof(int)     => reader.ReadInt32(),
                _ when field.FieldType == typeof(uint)    => reader.ReadUInt32(),
                _ when field.FieldType == typeof(bool)    => reader.ReadBoolean(),
                _ when field.FieldType == typeof(byte)    => reader.ReadByte(),
                _ when field.FieldType == typeof(long)    => reader.ReadInt64(),
                _ when field.FieldType == typeof(double)  => reader.ReadDouble(),
                _ when field.FieldType == typeof(short)   => reader.ReadInt16(),
                _ when field.FieldType == typeof(string) => reader.ReadString(),
                _ => throw new NotSupportedException(
                    $"Type {field.FieldType} not supported for NetVar deserialization")
            };
            field.SetValue(boxed, val);
        }

        return (T)boxed;
    }
}