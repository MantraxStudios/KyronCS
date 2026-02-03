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
        /// <summary>
        /// Serializa un componente extrayendo solo las propiedades y campos marcados con [ToStorage]
        /// </summary>
        public static ComponentData Serialize(Component component)
        {
            var data = new ComponentData
            {
                TypeName = component.GetType().AssemblyQualifiedName,
                ComponentId = component.Id,
                Enabled = component.Enabled,
                SerializedFields = new Dictionary<string, object>()
            };

            // Obtener todos los campos marcados con ToStorage
            var fields = component.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<ToStorageAttribute>() != null);

            foreach (var field in fields)
            {
                var value = field.GetValue(component);
                data.SerializedFields[field.Name] = SerializeValue(value);
            }

            // Obtener todas las propiedades marcadas con ToStorage
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

        /// <summary>
        /// Deserializa datos en un componente existente
        /// </summary>
        public static void Deserialize(Component component, ComponentData data)
        {
            component.Enabled = data.Enabled;

            foreach (var kvp in data.SerializedFields)
            {
                // Intentar establecer campo
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

                // Intentar establecer propiedad
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

        /// <summary>
        /// Serializa un valor a un formato que pueda guardarse en JSON
        /// </summary>
        private static object SerializeValue(object value)
        {
            if (value == null)
                return null;

            var type = value.GetType();

            // Tipos primitivos y strings
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return value;

            // Enums
            if (type.IsEnum)
            {
                return new Dictionary<string, object>
                {
                    ["__type"] = "Enum",
                    ["enumType"] = type.AssemblyQualifiedName,
                    ["value"] = value.ToString()
                };
            }

            // Vector3
            if (type == typeof(Vector3))
            {
                var v = (Vector3)value;
                return new Dictionary<string, object>
                {
                    ["__type"] = "Vector3",
                    ["x"] = v.X,
                    ["y"] = v.Y,
                    ["z"] = v.Z
                };
            }

            // Vector2
            if (type == typeof(Vector2))
            {
                var v = (Vector2)value;
                return new Dictionary<string, object>
                {
                    ["__type"] = "Vector2",
                    ["x"] = v.X,
                    ["y"] = v.Y
                };
            }

            // Vector4
            if (type == typeof(Vector4))
            {
                var v = (Vector4)value;
                return new Dictionary<string, object>
                {
                    ["__type"] = "Vector4",
                    ["x"] = v.X,
                    ["y"] = v.Y,
                    ["z"] = v.Z,
                    ["w"] = v.W
                };
            }

            // Quaternion
            if (type == typeof(Quaternion))
            {
                var q = (Quaternion)value;
                return new Dictionary<string, object>
                {
                    ["__type"] = "Quaternion",
                    ["x"] = q.X,
                    ["y"] = q.Y,
                    ["z"] = q.Z,
                    ["w"] = q.W
                };
            }

            // Color4
            if (type == typeof(Color4))
            {
                var c = (Color4)value;
                return new Dictionary<string, object>
                {
                    ["__type"] = "Color4",
                    ["r"] = c.R,
                    ["g"] = c.G,
                    ["b"] = c.B,
                    ["a"] = c.A
                };
            }

            // Listas genéricas (List<T>)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)value;
                var elementType = type.GetGenericArguments()[0];
                var serializedList = new List<object>();

                foreach (var item in list)
                {
                    serializedList.Add(SerializeValue(item));
                }

                return new Dictionary<string, object>
                {
                    ["__type"] = "List",
                    ["elementType"] = elementType.AssemblyQualifiedName,
                    ["values"] = serializedList
                };
            }

            // Diccionarios genéricos (Dictionary<K,V>)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var dict = (IDictionary)value;
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                var serializedDict = new List<object>();

                foreach (DictionaryEntry entry in dict)
                {
                    serializedDict.Add(new Dictionary<string, object>
                    {
                        ["key"] = SerializeValue(entry.Key),
                        ["value"] = SerializeValue(entry.Value)
                    });
                }

                return new Dictionary<string, object>
                {
                    ["__type"] = "Dictionary",
                    ["keyType"] = keyType.AssemblyQualifiedName,
                    ["valueType"] = valueType.AssemblyQualifiedName,
                    ["entries"] = serializedDict
                };
            }

            // Arrays (de cualquier tipo)
            if (type.IsArray)
            {
                var array = (Array)value;
                var elementType = type.GetElementType();
                var list = new List<object>();

                foreach (var item in array)
                {
                    list.Add(SerializeValue(item));
                }

                return new Dictionary<string, object>
                {
                    ["__type"] = "Array",
                    ["elementType"] = elementType.AssemblyQualifiedName,
                    ["values"] = list
                };
            }

            // Objetos personalizados (serializar sus campos públicos)
            if (type.IsClass)
            {
                var objData = new Dictionary<string, object>
                {
                    ["__type"] = "CustomObject",
                    ["objectType"] = type.AssemblyQualifiedName,
                    ["fields"] = new Dictionary<string, object>()
                };

                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                var fieldsDict = (Dictionary<string, object>)objData["fields"];

                foreach (var field in fields)
                {
                    var fieldValue = field.GetValue(value);
                    fieldsDict[field.Name] = SerializeValue(fieldValue);
                }

                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite);

                foreach (var property in properties)
                {
                    var propValue = property.GetValue(value);
                    fieldsDict[property.Name] = SerializeValue(propValue);
                }

                return objData;
            }

            // Para otros tipos, intentar convertir a string
            return value.ToString();
        }

        /// <summary>
        /// Deserializa un valor desde el formato guardado
        /// </summary>
        private static object DeserializeValue(object value, Type targetType)
        {
            if (value == null)
                return null;

            // Si el valor ya es del tipo correcto
            if (targetType.IsInstanceOfType(value))
                return value;

            // Tipos primitivos y conversiones básicas
            if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal))
            {
                // Si es un JsonElement, obtener el valor
                if (value is JsonElement jsonElement)
                {
                    return ConvertJsonElement(jsonElement, targetType);
                }
                return Convert.ChangeType(value, targetType);
            }

            // Enums
            if (targetType.IsEnum)
            {
                if (value is JsonElement jsonElement)
                {
                    value = DeserializeFromJsonElement(jsonElement, targetType);
                    if (value != null) return value;
                }

                if (value is Dictionary<string, object> enumDict && enumDict.ContainsKey("__type") && enumDict["__type"].ToString() == "Enum")
                {
                    var enumValue = enumDict["value"].ToString();
                    return Enum.Parse(targetType, enumValue);
                }

                // Si es un string directo
                if (value is string strValue)
                {
                    return Enum.Parse(targetType, strValue);
                }
            }

            // Manejar JsonElement para tipos complejos
            if (value is JsonElement element)
            {
                return DeserializeFromJsonElement(element, targetType);
            }

            // Si el valor es un diccionario (tipo complejo serializado)
            if (value is Dictionary<string, object> dict && dict.ContainsKey("__type"))
            {
                var typeName = dict["__type"].ToString();

                switch (typeName)
                {
                    case "Vector3":
                        return new Vector3(
                            Convert.ToSingle(dict["x"]),
                            Convert.ToSingle(dict["y"]),
                            Convert.ToSingle(dict["z"])
                        );

                    case "Vector2":
                        return new Vector2(
                            Convert.ToSingle(dict["x"]),
                            Convert.ToSingle(dict["y"])
                        );

                    case "Vector4":
                        return new Vector4(
                            Convert.ToSingle(dict["x"]),
                            Convert.ToSingle(dict["y"]),
                            Convert.ToSingle(dict["z"]),
                            Convert.ToSingle(dict["w"])
                        );

                    case "Quaternion":
                        return new Quaternion(
                            Convert.ToSingle(dict["x"]),
                            Convert.ToSingle(dict["y"]),
                            Convert.ToSingle(dict["z"]),
                            Convert.ToSingle(dict["w"])
                        );

                    case "Color4":
                        return new Color4(
                            Convert.ToSingle(dict["r"]),
                            Convert.ToSingle(dict["g"]),
                            Convert.ToSingle(dict["b"]),
                            Convert.ToSingle(dict["a"])
                        );

                    case "List":
                        {
                            var elementTypeName = dict["elementType"].ToString();
                            var elementType = Type.GetType(elementTypeName);
                            var listType = typeof(List<>).MakeGenericType(elementType);
                            var list = (IList)Activator.CreateInstance(listType);

                            if (dict["values"] is List<object> valuesList)
                            {
                                foreach (var item in valuesList)
                                {
                                    list.Add(DeserializeValue(item, elementType));
                                }
                            }

                            return list;
                        }

                    case "Dictionary":
                        {
                            var keyTypeName = dict["keyType"].ToString();
                            var valueTypeName = dict["valueType"].ToString();
                            var keyType = Type.GetType(keyTypeName);
                            var valueType = Type.GetType(valueTypeName);
                            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                            var dictionary = (IDictionary)Activator.CreateInstance(dictType);

                            if (dict["entries"] is List<object> entriesList)
                            {
                                foreach (var entryObj in entriesList)
                                {
                                    if (entryObj is Dictionary<string, object> entryDict)
                                    {
                                        var key = DeserializeValue(entryDict["key"], keyType);
                                        var val = DeserializeValue(entryDict["value"], valueType);
                                        dictionary.Add(key, val);
                                    }
                                }
                            }

                            return dictionary;
                        }

                    case "Array":
                        {
                            var elementTypeName = dict["elementType"].ToString();
                            var elementType = Type.GetType(elementTypeName);

                            if (dict["values"] is List<object> valuesList)
                            {
                                var array = Array.CreateInstance(elementType, valuesList.Count);
                                for (int i = 0; i < valuesList.Count; i++)
                                {
                                    array.SetValue(DeserializeValue(valuesList[i], elementType), i);
                                }
                                return array;
                            }
                        }
                        break;

                    case "CustomObject":
                        {
                            var objectTypeName = dict["objectType"].ToString();
                            var objectType = Type.GetType(objectTypeName);

                            if (objectType == null)
                            {
                                Console.WriteLine($"No se pudo encontrar el tipo: {objectTypeName}");
                                return null;
                            }

                            var instance = Activator.CreateInstance(objectType);

                            if (dict["fields"] is Dictionary<string, object> fieldsDict)
                            {
                                foreach (var kvp in fieldsDict)
                                {
                                    // Intentar campo
                                    var field = objectType.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                                    if (field != null)
                                    {
                                        var fieldValue = DeserializeValue(kvp.Value, field.FieldType);
                                        field.SetValue(instance, fieldValue);
                                        continue;
                                    }

                                    // Intentar propiedad
                                    var property = objectType.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                                    if (property != null && property.CanWrite)
                                    {
                                        var propValue = DeserializeValue(kvp.Value, property.PropertyType);
                                        property.SetValue(instance, propValue);
                                    }
                                }
                            }

                            return instance;
                        }
                }
            }

            // Intentar conversión directa
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                Console.WriteLine($"No se pudo deserializar valor de tipo {value.GetType()} a {targetType}");
                return null;
            }
        }

        /// <summary>
        /// Convierte un JsonElement a un tipo primitivo
        /// </summary>
        private static object ConvertJsonElement(JsonElement element, Type targetType)
        {
            if (targetType == typeof(string))
                return element.GetString();
            if (targetType == typeof(int))
                return element.GetInt32();
            if (targetType == typeof(long))
                return element.GetInt64();
            if (targetType == typeof(float))
                return element.GetSingle();
            if (targetType == typeof(double))
                return element.GetDouble();
            if (targetType == typeof(bool))
                return element.GetBoolean();
            if (targetType == typeof(byte))
                return element.GetByte();
            if (targetType == typeof(short))
                return element.GetInt16();
            if (targetType == typeof(decimal))
                return element.GetDecimal();
            if (targetType == typeof(uint))
                return element.GetUInt32();
            if (targetType == typeof(ulong))
                return element.GetUInt64();
            if (targetType == typeof(ushort))
                return element.GetUInt16();

            return element.ToString();
        }

        /// <summary>
        /// Deserializa tipos complejos desde JsonElement
        /// </summary>
        private static object DeserializeFromJsonElement(JsonElement element, Type targetType)
        {
            // Enums
            if (targetType.IsEnum)
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "Enum")
                {
                    var enumValue = element.GetProperty("value").GetString();
                    return Enum.Parse(targetType, enumValue);
                }
                else if (element.ValueKind == JsonValueKind.String)
                {
                    return Enum.Parse(targetType, element.GetString());
                }
            }

            // Vector3
            if (targetType == typeof(Vector3))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "Vector3")
                {
                    return new Vector3(
                        element.GetProperty("x").GetSingle(),
                        element.GetProperty("y").GetSingle(),
                        element.GetProperty("z").GetSingle()
                    );
                }
            }

            // Vector2
            if (targetType == typeof(Vector2))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "Vector2")
                {
                    return new Vector2(
                        element.GetProperty("x").GetSingle(),
                        element.GetProperty("y").GetSingle()
                    );
                }
            }

            // Vector4
            if (targetType == typeof(Vector4))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "Vector4")
                {
                    return new Vector4(
                        element.GetProperty("x").GetSingle(),
                        element.GetProperty("y").GetSingle(),
                        element.GetProperty("z").GetSingle(),
                        element.GetProperty("w").GetSingle()
                    );
                }
            }

            // Quaternion
            if (targetType == typeof(Quaternion))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "Quaternion")
                {
                    return new Quaternion(
                        element.GetProperty("x").GetSingle(),
                        element.GetProperty("y").GetSingle(),
                        element.GetProperty("z").GetSingle(),
                        element.GetProperty("w").GetSingle()
                    );
                }
            }

            // Color4
            if (targetType == typeof(Color4))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "Color4")
                {
                    return new Color4(
                        element.GetProperty("r").GetSingle(),
                        element.GetProperty("g").GetSingle(),
                        element.GetProperty("b").GetSingle(),
                        element.GetProperty("a").GetSingle()
                    );
                }
            }

            // Lists
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "List")
                {
                    var elementTypeName = element.GetProperty("elementType").GetString();
                    var elementType = Type.GetType(elementTypeName);
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = (IList)Activator.CreateInstance(listType);

                    var valuesArray = element.GetProperty("values");
                    foreach (var item in valuesArray.EnumerateArray())
                    {
                        list.Add(DeserializeValue(item, elementType));
                    }

                    return list;
                }
                // Array directo sin wrapper
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    var elementType = targetType.GetGenericArguments()[0];
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = (IList)Activator.CreateInstance(listType);

                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(DeserializeValue(item, elementType));
                    }

                    return list;
                }
            }

            // Dictionaries
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "Dictionary")
                {
                    var keyTypeName = element.GetProperty("keyType").GetString();
                    var valueTypeName = element.GetProperty("valueType").GetString();
                    var keyType = Type.GetType(keyTypeName);
                    var valueType = Type.GetType(valueTypeName);
                    var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                    var dictionary = (IDictionary)Activator.CreateInstance(dictType);

                    var entriesArray = element.GetProperty("entries");
                    foreach (var entryEl in entriesArray.EnumerateArray())
                    {
                        var key = DeserializeValue(entryEl.GetProperty("key"), keyType);
                        var val = DeserializeValue(entryEl.GetProperty("value"), valueType);
                        dictionary.Add(key, val);
                    }

                    return dictionary;
                }
            }

            // Arrays
            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();

                // Array con wrapper __type
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "Array")
                {
                    var elementTypeName = element.GetProperty("elementType").GetString();
                    elementType = Type.GetType(elementTypeName);
                    var valuesArray = element.GetProperty("values");

                    var array = Array.CreateInstance(elementType, valuesArray.GetArrayLength());
                    int index = 0;
                    foreach (var item in valuesArray.EnumerateArray())
                    {
                        array.SetValue(DeserializeValue(item, elementType), index++);
                    }
                    return array;
                }
                // Array directo sin wrapper
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    var array = Array.CreateInstance(elementType, element.GetArrayLength());
                    int index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        array.SetValue(DeserializeValue(item, elementType), index++);
                    }
                    return array;
                }
            }

            // Custom Objects
            if (targetType.IsClass && element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("__type", out var typeEl) &&
                    typeEl.GetString() == "CustomObject")
                {
                    var objectTypeName = element.GetProperty("objectType").GetString();
                    var objectType = Type.GetType(objectTypeName);

                    if (objectType == null)
                    {
                        Console.WriteLine($"No se pudo encontrar el tipo: {objectTypeName}");
                        return null;
                    }

                    var instance = Activator.CreateInstance(objectType);
                    var fieldsElement = element.GetProperty("fields");

                    foreach (var fieldProp in fieldsElement.EnumerateObject())
                    {
                        // Intentar campo
                        var field = objectType.GetField(fieldProp.Name, BindingFlags.Public | BindingFlags.Instance);
                        if (field != null)
                        {
                            var fieldValue = DeserializeValue(fieldProp.Value, field.FieldType);
                            field.SetValue(instance, fieldValue);
                            continue;
                        }

                        // Intentar propiedad
                        var property = objectType.GetProperty(fieldProp.Name, BindingFlags.Public | BindingFlags.Instance);
                        if (property != null && property.CanWrite)
                        {
                            var propValue = DeserializeValue(fieldProp.Value, property.PropertyType);
                            property.SetValue(instance, propValue);
                        }
                    }

                    return instance;
                }
            }

            // Para tipos primitivos en JsonElement
            if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal))
            {
                return ConvertJsonElement(element, targetType);
            }

            return null;
        }
    }
}