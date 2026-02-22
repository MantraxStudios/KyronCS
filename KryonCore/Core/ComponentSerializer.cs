using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public static class ComponentSerializer
    {
        public static ComponentData Serialize(Component component)
        {
            var data = new ComponentData
            {
                TypeName = component.GetType().AssemblyQualifiedName,
                ComponentId = component.Id,
                Enabled = component.Enabled,
                SerializedFields = new Dictionary<string, object>()
            };

            var fields = component.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<ToStorageAttribute>() != null);

            foreach (var field in fields)
            {
                var value = field.GetValue(component);
                data.SerializedFields[field.Name] = SerializeValue(value);
            }

            var properties = component.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ToStorageAttribute>() != null && p.CanRead && p.CanWrite);

            foreach (var property in properties)
            {
                var value = property.GetValue(component);
                data.SerializedFields[property.Name] = SerializeValue(value);
            }

            return data;
        }

        public static void Deserialize(Component component, ComponentData data)
        {
            component.Enabled = data.Enabled;

            foreach (var kvp in data.SerializedFields)
            {
                var field = component.GetType()
                    .GetField(kvp.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (field != null && field.GetCustomAttribute<ToStorageAttribute>() != null)
                {
                    try
                    {
                        var value = DeserializeValue(kvp.Value, field.FieldType);
                        field.SetValue(component, value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializando campo {kvp.Key}: {ex.Message}");
                    }
                    continue;
                }

                var property = component.GetType()
                    .GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (property != null && property.GetCustomAttribute<ToStorageAttribute>() != null && property.CanWrite)
                {
                    try
                    {
                        var value = DeserializeValue(kvp.Value, property.PropertyType);
                        property.SetValue(component, value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializando propiedad {kvp.Key}: {ex.Message}");
                    }
                }
            }
        }

        private static object SerializeValue(object value)
        {
            if (value == null) return null;

            var type = value.GetType();

            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return value;

            if (type.IsEnum)
                return new Dictionary<string, object>
                {
                    ["__type"] = "Enum",
                    ["enumType"] = type.AssemblyQualifiedName,
                    ["value"] = value.ToString()
                };

            if (type == typeof(Vector3))
            {
                var v = (Vector3)value;
                return new Dictionary<string, object> { ["__type"] = "Vector3", ["x"] = v.X, ["y"] = v.Y, ["z"] = v.Z };
            }

            if (type == typeof(Vector2))
            {
                var v = (Vector2)value;
                return new Dictionary<string, object> { ["__type"] = "Vector2", ["x"] = v.X, ["y"] = v.Y };
            }

            if (type == typeof(Vector4))
            {
                var v = (Vector4)value;
                return new Dictionary<string, object> { ["__type"] = "Vector4", ["x"] = v.X, ["y"] = v.Y, ["z"] = v.Z, ["w"] = v.W };
            }

            if (type == typeof(Quaternion))
            {
                var q = (Quaternion)value;
                return new Dictionary<string, object> { ["__type"] = "Quaternion", ["x"] = q.X, ["y"] = q.Y, ["z"] = q.Z, ["w"] = q.W };
            }

            if (type == typeof(Color4))
            {
                var c = (Color4)value;
                return new Dictionary<string, object> { ["__type"] = "Color4", ["r"] = c.R, ["g"] = c.G, ["b"] = c.B, ["a"] = c.A };
            }

            // Dictionary<string, object> — usado por ScriptVariableValues
            if (type == typeof(Dictionary<string, object>))
            {
                var dict = (Dictionary<string, object>)value;
                var entries = new Dictionary<string, object>();
                foreach (var kvp in dict)
                    entries[kvp.Key] = SerializeValue(kvp.Value);
                return new Dictionary<string, object>
                {
                    ["__type"] = "StringObjectDict",
                    ["entries"] = entries
                };
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)value;
                var elementType = type.GetGenericArguments()[0];
                var serializedList = new List<object>();
                foreach (var item in list)
                    serializedList.Add(SerializeValue(item));
                return new Dictionary<string, object>
                {
                    ["__type"] = "List",
                    ["elementType"] = elementType.AssemblyQualifiedName,
                    ["values"] = serializedList
                };
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var dict = (IDictionary)value;
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                var entries = new List<object>();
                foreach (DictionaryEntry entry in dict)
                    entries.Add(new Dictionary<string, object>
                    {
                        ["key"] = SerializeValue(entry.Key),
                        ["value"] = SerializeValue(entry.Value)
                    });
                return new Dictionary<string, object>
                {
                    ["__type"] = "Dictionary",
                    ["keyType"] = keyType.AssemblyQualifiedName,
                    ["valueType"] = valueType.AssemblyQualifiedName,
                    ["entries"] = entries
                };
            }

            if (type.IsArray)
            {
                var array = (Array)value;
                var elementType = type.GetElementType();
                var list = new List<object>();
                foreach (var item in array)
                    list.Add(SerializeValue(item));
                return new Dictionary<string, object>
                {
                    ["__type"] = "Array",
                    ["elementType"] = elementType.AssemblyQualifiedName,
                    ["values"] = list
                };
            }

            if (type.IsClass)
            {
                var fieldsDict = new Dictionary<string, object>();
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    fieldsDict[f.Name] = SerializeValue(f.GetValue(value));
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite))
                    fieldsDict[p.Name] = SerializeValue(p.GetValue(value));
                return new Dictionary<string, object>
                {
                    ["__type"] = "CustomObject",
                    ["objectType"] = type.AssemblyQualifiedName,
                    ["fields"] = fieldsDict
                };
            }

            return value.ToString();
        }

        private static object DeserializeValue(object value, Type targetType)
        {
            if (value == null) return null;

            if (targetType.IsInstanceOfType(value)) return value;

            if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal))
            {
                if (value is JsonElement je) return ConvertJsonElement(je, targetType);
                return Convert.ChangeType(value, targetType);
            }

            if (targetType.IsEnum)
            {
                if (value is JsonElement je)
                {
                    var r = DeserializeFromJsonElement(je, targetType);
                    if (r != null) return r;
                }
                if (value is Dictionary<string, object> ed && ed.ContainsKey("__type") && ed["__type"].ToString() == "Enum")
                    return Enum.Parse(targetType, ed["value"].ToString());
                if (value is string sv) return Enum.Parse(targetType, sv);
            }

            if (value is JsonElement element)
                return DeserializeFromJsonElement(element, targetType);

            if (value is Dictionary<string, object> dict && dict.ContainsKey("__type"))
            {
                var typeName = dict["__type"].ToString();
                switch (typeName)
                {
                    case "StringObjectDict":
                        {
                            var result = new Dictionary<string, object>();
                            if (dict["entries"] is Dictionary<string, object> entries)
                                foreach (var kvp in entries)
                                    result[kvp.Key] = DeserializePrimitive(kvp.Value);
                            return result;
                        }
                    case "Vector3":
                        return new Vector3(Convert.ToSingle(dict["x"]), Convert.ToSingle(dict["y"]), Convert.ToSingle(dict["z"]));
                    case "Vector2":
                        return new Vector2(Convert.ToSingle(dict["x"]), Convert.ToSingle(dict["y"]));
                    case "Vector4":
                        return new Vector4(Convert.ToSingle(dict["x"]), Convert.ToSingle(dict["y"]), Convert.ToSingle(dict["z"]), Convert.ToSingle(dict["w"]));
                    case "Quaternion":
                        return new Quaternion(Convert.ToSingle(dict["x"]), Convert.ToSingle(dict["y"]), Convert.ToSingle(dict["z"]), Convert.ToSingle(dict["w"]));
                    case "Color4":
                        return new Color4(Convert.ToSingle(dict["r"]), Convert.ToSingle(dict["g"]), Convert.ToSingle(dict["b"]), Convert.ToSingle(dict["a"]));
                    case "Enum":
                        {
                            var enumType = Type.GetType(dict["enumType"].ToString());
                            return Enum.Parse(enumType, dict["value"].ToString());
                        }
                    case "List":
                        {
                            var elementType = Type.GetType(dict["elementType"].ToString());
                            var listType = typeof(List<>).MakeGenericType(elementType);
                            var list = (IList)Activator.CreateInstance(listType);
                            if (dict["values"] is List<object> vals)
                                foreach (var item in vals)
                                    list.Add(DeserializeValue(item, elementType));
                            return list;
                        }
                    case "Dictionary":
                        {
                            var keyType = Type.GetType(dict["keyType"].ToString());
                            var valueType = Type.GetType(dict["valueType"].ToString());
                            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                            var dictionary = (IDictionary)Activator.CreateInstance(dictType);
                            if (dict["entries"] is List<object> entries)
                                foreach (var entryObj in entries)
                                    if (entryObj is Dictionary<string, object> ed)
                                        dictionary.Add(DeserializeValue(ed["key"], keyType), DeserializeValue(ed["value"], valueType));
                            return dictionary;
                        }
                    case "Array":
                        {
                            var elementType = Type.GetType(dict["elementType"].ToString());
                            if (dict["values"] is List<object> vals)
                            {
                                var array = Array.CreateInstance(elementType, vals.Count);
                                for (int i = 0; i < vals.Count; i++)
                                    array.SetValue(DeserializeValue(vals[i], elementType), i);
                                return array;
                            }
                            break;
                        }
                    case "CustomObject":
                        {
                            var objectType = Type.GetType(dict["objectType"].ToString());
                            if (objectType == null) { Console.WriteLine($"Tipo no encontrado: {dict["objectType"]}"); return null; }
                            var instance = Activator.CreateInstance(objectType);
                            if (dict["fields"] is Dictionary<string, object> fields)
                                foreach (var kvp in fields)
                                {
                                    var f = objectType.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                                    if (f != null) { f.SetValue(instance, DeserializeValue(kvp.Value, f.FieldType)); continue; }
                                    var p = objectType.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                                    if (p != null && p.CanWrite) p.SetValue(instance, DeserializeValue(kvp.Value, p.PropertyType));
                                }
                            return instance;
                        }
                }
            }

            try { return Convert.ChangeType(value, targetType); }
            catch { Console.WriteLine($"No se pudo deserializar {value.GetType()} a {targetType}"); return null; }
        }

        private static object DeserializePrimitive(object value)
        {
            if (value is JsonElement je)
            {
                return je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString(),
                    JsonValueKind.True => (object)true,
                    JsonValueKind.False => (object)false,
                    JsonValueKind.Number => je.TryGetInt32(out int i) ? (object)i :
                                           je.TryGetSingle(out float f) ? (object)f :
                                           (object)je.GetDouble(),
                    JsonValueKind.Object => DeserializeFromJsonElement(je, typeof(object)),
                    _ => je.ToString()
                };
            }
            return value;
        }

        private static object ConvertJsonElement(JsonElement element, Type targetType)
        {
            if (targetType == typeof(string)) return element.GetString();
            if (targetType == typeof(int)) return element.GetInt32();
            if (targetType == typeof(long)) return element.GetInt64();
            if (targetType == typeof(float)) return element.GetSingle();
            if (targetType == typeof(double)) return element.GetDouble();
            if (targetType == typeof(bool)) return element.GetBoolean();
            if (targetType == typeof(byte)) return element.GetByte();
            if (targetType == typeof(short)) return element.GetInt16();
            if (targetType == typeof(decimal)) return element.GetDecimal();
            if (targetType == typeof(uint)) return element.GetUInt32();
            if (targetType == typeof(ulong)) return element.GetUInt64();
            if (targetType == typeof(ushort)) return element.GetUInt16();
            return element.ToString();
        }

        private static object DeserializeFromJsonElement(JsonElement element, Type targetType)
        {
            if (targetType.IsEnum)
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "Enum")
                    return Enum.Parse(targetType, element.GetProperty("value").GetString());
                if (element.ValueKind == JsonValueKind.String)
                    return Enum.Parse(targetType, element.GetString());
            }

            if (targetType == typeof(Vector3))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "Vector3")
                    return new Vector3(element.GetProperty("x").GetSingle(), element.GetProperty("y").GetSingle(), element.GetProperty("z").GetSingle());
            }

            if (targetType == typeof(Vector2))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "Vector2")
                    return new Vector2(element.GetProperty("x").GetSingle(), element.GetProperty("y").GetSingle());
            }

            if (targetType == typeof(Vector4))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "Vector4")
                    return new Vector4(element.GetProperty("x").GetSingle(), element.GetProperty("y").GetSingle(), element.GetProperty("z").GetSingle(), element.GetProperty("w").GetSingle());
            }

            if (targetType == typeof(Quaternion))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "Quaternion")
                    return new Quaternion(element.GetProperty("x").GetSingle(), element.GetProperty("y").GetSingle(), element.GetProperty("z").GetSingle(), element.GetProperty("w").GetSingle());
            }

            if (targetType == typeof(Color4))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "Color4")
                    return new Color4(element.GetProperty("r").GetSingle(), element.GetProperty("g").GetSingle(), element.GetProperty("b").GetSingle(), element.GetProperty("a").GetSingle());
            }

            // Dictionary<string, object> — ScriptVariableValues
            if (targetType == typeof(Dictionary<string, object>) || targetType == typeof(object))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "StringObjectDict")
                {
                    var result = new Dictionary<string, object>();
                    var entries = element.GetProperty("entries");
                    foreach (var prop in entries.EnumerateObject())
                        result[prop.Name] = DeserializePrimitiveFromJsonElement(prop.Value);
                    return result;
                }
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "List")
                {
                    var elementType = Type.GetType(element.GetProperty("elementType").GetString());
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = (IList)Activator.CreateInstance(listType);
                    foreach (var item in element.GetProperty("values").EnumerateArray())
                        list.Add(DeserializeValue(item, elementType));
                    return list;
                }
                if (element.ValueKind == JsonValueKind.Array)
                {
                    var elementType = targetType.GetGenericArguments()[0];
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = (IList)Activator.CreateInstance(listType);
                    foreach (var item in element.EnumerateArray())
                        list.Add(DeserializeValue(item, elementType));
                    return list;
                }
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "Dictionary")
                {
                    var keyType = Type.GetType(element.GetProperty("keyType").GetString());
                    var valueType = Type.GetType(element.GetProperty("valueType").GetString());
                    var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                    var dictionary = (IDictionary)Activator.CreateInstance(dictType);
                    foreach (var entryEl in element.GetProperty("entries").EnumerateArray())
                        dictionary.Add(DeserializeValue(entryEl.GetProperty("key"), keyType), DeserializeValue(entryEl.GetProperty("value"), valueType));
                    return dictionary;
                }
            }

            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var te) && te.GetString() == "Array")
                {
                    elementType = Type.GetType(element.GetProperty("elementType").GetString());
                    var vals = element.GetProperty("values");
                    var array = Array.CreateInstance(elementType, vals.GetArrayLength());
                    int idx = 0;
                    foreach (var item in vals.EnumerateArray())
                        array.SetValue(DeserializeValue(item, elementType), idx++);
                    return array;
                }
                if (element.ValueKind == JsonValueKind.Array)
                {
                    var array = Array.CreateInstance(elementType, element.GetArrayLength());
                    int idx = 0;
                    foreach (var item in element.EnumerateArray())
                        array.SetValue(DeserializeValue(item, elementType), idx++);
                    return array;
                }
            }

            if (targetType.IsClass && element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("__type", out var te) && te.GetString() == "CustomObject")
                {
                    var objectType = Type.GetType(element.GetProperty("objectType").GetString());
                    if (objectType == null) { Console.WriteLine($"Tipo no encontrado: {element.GetProperty("objectType").GetString()}"); return null; }
                    var instance = Activator.CreateInstance(objectType);
                    foreach (var fieldProp in element.GetProperty("fields").EnumerateObject())
                    {
                        var f = objectType.GetField(fieldProp.Name, BindingFlags.Public | BindingFlags.Instance);
                        if (f != null) { f.SetValue(instance, DeserializeValue(fieldProp.Value, f.FieldType)); continue; }
                        var p = objectType.GetProperty(fieldProp.Name, BindingFlags.Public | BindingFlags.Instance);
                        if (p != null && p.CanWrite) p.SetValue(instance, DeserializeValue(fieldProp.Value, p.PropertyType));
                    }
                    return instance;
                }
            }

            if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal))
                return ConvertJsonElement(element, targetType);

            return null;
        }

        private static object DeserializePrimitiveFromJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => (object)true,
                JsonValueKind.False => (object)false,
                JsonValueKind.Number => element.TryGetInt32(out int i) ? (object)i :
                                       element.TryGetSingle(out float f) ? (object)f :
                                       (object)element.GetDouble(),
                JsonValueKind.Object => DeserializeFromJsonElement(element, typeof(object)),
                _ => element.ToString()
            };
        }
    }
}