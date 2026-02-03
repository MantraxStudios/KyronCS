using ImGuiNET;
using KrayonCore;
using System;
using System.Numerics;
using System.Reflection;

namespace KrayonEditor.UI
{
    public class InspectorUI : UIBehaviour
    {
        public GameObject? SelectedObject { get; set; }

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.Begin("Inspector", ref _isVisible);

            if (SelectedObject != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.9f, 0.9f, 1.0f));
                string name = SelectedObject.Name;
                if (ImGui.InputText("Name", ref name, 256))
                {
                    SelectedObject.Name = name;
                }
                ImGui.PopStyleColor();
                ImGui.Separator();

                DrawTransformComponent();
                DrawComponents();

                ImGui.Separator();

                DrawAddComponentButton();
            }
            else
            {
                ImGui.TextDisabled("No object selected");
            }

            ImGui.End();
        }

        private void DrawTransformComponent()
        {
            ImGui.PushID("Transform");

            if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var transform = SelectedObject!.Transform;

                Vector3 position = new Vector3(transform.X, transform.Y, transform.Z);
                if (ImGui.InputFloat3("Position", ref position))
                {
                    transform.SetPosition(position.X, position.Y, position.Z);
                }

                Vector3 rotation = new Vector3(transform.RotationX, transform.RotationY, transform.RotationZ);
                if (ImGui.InputFloat3("Rotation", ref rotation))
                {
                    transform.SetRotation(rotation.X, rotation.Y, rotation.Z);
                }

                Vector3 scale = new Vector3(transform.ScaleX, transform.ScaleY, transform.ScaleZ);
                if (ImGui.InputFloat3("Scale", ref scale))
                {
                    transform.SetScale(scale.X, scale.Y, scale.Z);
                }
            }

            ImGui.PopID();
        }

        private void DrawComponents()
        {
            var components = SelectedObject!.GetAllComponents();
            int componentIndex = 0;

            foreach (var component in components)
            {
                if (component.GetType().Name == "Transform")
                {
                    componentIndex++;
                    continue;
                }

                ImGui.PushID($"Component_{componentIndex}");
                DrawComponentWithReflection(component);
                ImGui.PopID();
                componentIndex++;
            }
        }

        private void DrawComponentWithReflection(object component)
        {
            Type componentType = component.GetType();
            string componentName = componentType.Name;

            PropertyInfo? enabledProperty = componentType.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
            bool hasEnabled = enabledProperty != null && enabledProperty.PropertyType == typeof(bool);

            bool isEnabled = true;
            if (hasEnabled)
            {
                isEnabled = (bool)enabledProperty!.GetValue(component)!;
            }

            if (hasEnabled)
            {
                bool enabled = isEnabled;
                if (ImGui.Checkbox("##enabled", ref enabled))
                {
                    enabledProperty!.SetValue(component, enabled);
                }
                ImGui.SameLine();
            }

            if (ImGui.CollapsingHeader(componentName, ImGuiTreeNodeFlags.DefaultOpen))
            {
                PropertyInfo[] properties = componentType.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance
                );

                int propertyIndex = 0;
                foreach (var property in properties)
                {
                    if (!property.CanRead || !property.CanWrite)
                        continue;

                    if (property.Name == "Enabled" && property.PropertyType == typeof(bool))
                        continue;

                    ImGui.PushID($"Property_{propertyIndex}");
                    DrawProperty(component, property);
                    ImGui.PopID();
                    propertyIndex++;
                }

                FieldInfo[] fields = componentType.GetFields(
                    BindingFlags.Public | BindingFlags.Instance
                );

                int fieldIndex = 0;
                foreach (var field in fields)
                {
                    ImGui.PushID($"Field_{fieldIndex}");
                    DrawField(component, field);
                    ImGui.PopID();
                    fieldIndex++;
                }
            }
        }

        private void DrawProperty(object component, PropertyInfo property)
        {
            Type propertyType = property.PropertyType;
            object? value = property.GetValue(component);

            if (propertyType.IsArray)
            {
                DrawArrayProperty(component, property, value);
                return;
            }

            if (value == null)
            {
                ImGui.Text($"{property.Name}: null");
                return;
            }

            if (propertyType == typeof(bool))
            {
                bool boolValue = (bool)value;
                if (ImGui.Checkbox(property.Name, ref boolValue))
                {
                    property.SetValue(component, boolValue);
                }
            }
            else if (propertyType == typeof(float))
            {
                float floatValue = (float)value;
                if (ImGui.InputFloat(property.Name, ref floatValue))
                {
                    property.SetValue(component, floatValue);
                }
            }
            else if (propertyType == typeof(int))
            {
                int intValue = (int)value;
                if (ImGui.InputInt(property.Name, ref intValue))
                {
                    property.SetValue(component, intValue);
                }
            }
            else if (propertyType == typeof(string))
            {
                string stringValue = (string)value;
                if (ImGui.InputText(property.Name, ref stringValue, 256))
                {
                    property.SetValue(component, stringValue);
                }
            }
            else if (propertyType == typeof(Vector2))
            {
                Vector2 vec2Value = (Vector2)value;
                if (ImGui.InputFloat2(property.Name, ref vec2Value))
                {
                    property.SetValue(component, vec2Value);
                }
            }
            else if (propertyType == typeof(Vector3))
            {
                Vector3 vec3Value = (Vector3)value;
                if (ImGui.InputFloat3(property.Name, ref vec3Value))
                {
                    property.SetValue(component, vec3Value);
                }
            }
            else if (propertyType == typeof(Vector4))
            {
                Vector4 vec4Value = (Vector4)value;
                if (ImGui.InputFloat4(property.Name, ref vec4Value))
                {
                    property.SetValue(component, vec4Value);
                }
            }
            else if (propertyType == typeof(Quaternion))
            {
                Quaternion quatValue = (Quaternion)value;
                Vector4 vec4 = new Vector4(quatValue.X, quatValue.Y, quatValue.Z, quatValue.W);
                if (ImGui.InputFloat4(property.Name, ref vec4))
                {
                    property.SetValue(component, new Quaternion(vec4.X, vec4.Y, vec4.Z, vec4.W));
                }
            }
            else if (propertyType == typeof(GameObject))
            {
                GameObject gameObjectValue = (GameObject)value;
                string displayName = gameObjectValue != null ? gameObjectValue.Name : "None";
                
                ImGui.Text($"{property.Name}:");
                ImGui.SameLine();
                
                if (ImGui.Button($"{displayName}###{property.Name}"))
                {
                    ImGui.OpenPopup($"SelectGameObject_{property.Name}");
                }
                
                if (ImGui.BeginPopup($"SelectGameObject_{property.Name}"))
                {
                    if (ImGui.MenuItem("None"))
                    {
                        property.SetValue(component, null);
                    }
                    
                    ImGui.Separator();
                    
                    var allObjects = SceneManager.ActiveScene?.GetAllGameObjects() ?? new System.Collections.Generic.List<GameObject>();
                    int objIndex = 0;
                    foreach (var obj in allObjects)
                    {
                        ImGui.PushID($"GO_{objIndex}");
                        if (ImGui.MenuItem(obj.Name))
                        {
                            property.SetValue(component, obj);
                        }
                        ImGui.PopID();
                        objIndex++;
                    }
                    
                    ImGui.EndPopup();
                }
            }
            else if (propertyType == typeof(KrayonCore.Material))
            {
                KrayonCore.Material materialValue = (KrayonCore.Material)value;
                string displayName = materialValue != null ? materialValue.Name : "None";
                
                ImGui.Text($"{property.Name}:");
                ImGui.SameLine();
                
                if (ImGui.Button($"{displayName}###{property.Name}"))
                {
                    ImGui.OpenPopup($"SelectMaterial_{property.Name}");
                }
                
                if (ImGui.BeginPopup($"SelectMaterial_{property.Name}"))
                {
                    if (ImGui.MenuItem("None"))
                    {
                        property.SetValue(component, null);
                    }
                    
                    ImGui.Separator();
                    
                    var allMaterials = GraphicsEngine.Instance.Materials.GetAll();
                    int matIndex = 0;
                    foreach (var mat in allMaterials)
                    {
                        ImGui.PushID($"Mat_{matIndex}");
                        if (ImGui.MenuItem(mat.Name))
                        {
                            property.SetValue(component, mat);
                        }
                        ImGui.PopID();
                        matIndex++;
                    }
                    
                    ImGui.EndPopup();
                }
            }
            else
            {
                ImGui.Text($"{property.Name}: {value}");
            }
        }

        private void DrawArrayProperty(object component, PropertyInfo property, object? value)
        {
            Type elementType = property.PropertyType.GetElementType()!;
            Array? array = value as Array;

            if (ImGui.TreeNode($"{property.Name} (Array)"))
            {
                int size = array?.Length ?? 0;
                if (ImGui.InputInt("Size", ref size))
                {
                    if (size < 0) size = 0;
                    Array newArray = Array.CreateInstance(elementType, size);
                    if (array != null)
                    {
                        int copyLength = Math.Min(array.Length, size);
                        Array.Copy(array, newArray, copyLength);
                    }
                    property.SetValue(component, newArray);
                    array = newArray;
                }

                if (array != null)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        ImGui.PushID($"Element_{i}");
                        DrawArrayElement(array, i, elementType);
                        ImGui.PopID();
                    }
                }

                ImGui.TreePop();
            }
        }

        private void DrawArrayElement(Array array, int index, Type elementType)
        {
            object? value = array.GetValue(index);

            if (elementType == typeof(string))
            {
                string stringValue = (string?)value ?? "";
                if (ImGui.InputText($"[{index}]", ref stringValue, 256))
                {
                    array.SetValue(stringValue, index);
                }
            }
            else if (elementType == typeof(int))
            {
                int intValue = value != null ? (int)value : 0;
                if (ImGui.InputInt($"[{index}]", ref intValue))
                {
                    array.SetValue(intValue, index);
                }
            }
            else if (elementType == typeof(float))
            {
                float floatValue = value != null ? (float)value : 0f;
                if (ImGui.InputFloat($"[{index}]", ref floatValue))
                {
                    array.SetValue(floatValue, index);
                }
            }
            else if (elementType == typeof(bool))
            {
                bool boolValue = value != null && (bool)value;
                if (ImGui.Checkbox($"[{index}]", ref boolValue))
                {
                    array.SetValue(boolValue, index);
                }
            }
            else if (elementType == typeof(Vector2))
            {
                Vector2 vec2Value = value != null ? (Vector2)value : Vector2.Zero;
                if (ImGui.InputFloat2($"[{index}]", ref vec2Value))
                {
                    array.SetValue(vec2Value, index);
                }
            }
            else if (elementType == typeof(Vector3))
            {
                Vector3 vec3Value = value != null ? (Vector3)value : Vector3.Zero;
                if (ImGui.InputFloat3($"[{index}]", ref vec3Value))
                {
                    array.SetValue(vec3Value, index);
                }
            }
            else if (elementType == typeof(Vector4))
            {
                Vector4 vec4Value = value != null ? (Vector4)value : Vector4.Zero;
                if (ImGui.InputFloat4($"[{index}]", ref vec4Value))
                {
                    array.SetValue(vec4Value, index);
                }
            }
            else if (elementType == typeof(GameObject))
            {
                GameObject? gameObjectValue = value as GameObject;
                string displayName = gameObjectValue != null ? gameObjectValue.Name : "None";
                
                ImGui.Text($"[{index}]:");
                ImGui.SameLine();
                
                if (ImGui.Button($"{displayName}###{index}"))
                {
                    ImGui.OpenPopup($"SelectGameObject_{index}");
                }
                
                if (ImGui.BeginPopup($"SelectGameObject_{index}"))
                {
                    if (ImGui.MenuItem("None"))
                    {
                        array.SetValue(null, index);
                    }
                    
                    ImGui.Separator();
                    
                    var allObjects = SceneManager.ActiveScene?.GetAllGameObjects() ?? new System.Collections.Generic.List<GameObject>();
                    int objIndex = 0;
                    foreach (var obj in allObjects)
                    {
                        ImGui.PushID($"GO_{objIndex}");
                        if (ImGui.MenuItem(obj.Name))
                        {
                            array.SetValue(obj, index);
                        }
                        ImGui.PopID();
                        objIndex++;
                    }
                    
                    ImGui.EndPopup();
                }
            }
            else if (elementType == typeof(KrayonCore.Material))
            {
                KrayonCore.Material? materialValue = value as KrayonCore.Material;
                string displayName = materialValue != null ? materialValue.Name : "None";
                
                ImGui.Text($"[{index}]:");
                ImGui.SameLine();
                
                if (ImGui.Button($"{displayName}###{index}"))
                {
                    ImGui.OpenPopup($"SelectMaterial_{index}");
                }
                
                if (ImGui.BeginPopup($"SelectMaterial_{index}"))
                {
                    if (ImGui.MenuItem("None"))
                    {
                        array.SetValue(null, index);
                    }
                    
                    ImGui.Separator();
                    
                    var allMaterials = GraphicsEngine.Instance.Materials.GetAll();
                    int matIndex = 0;
                    foreach (var mat in allMaterials)
                    {
                        ImGui.PushID($"Mat_{matIndex}");
                        if (ImGui.MenuItem(mat.Name))
                        {
                            array.SetValue(mat, index);
                        }
                        ImGui.PopID();
                        matIndex++;
                    }
                    
                    ImGui.EndPopup();
                }
            }
            else
            {
                ImGui.Text($"[{index}]: {value?.ToString() ?? "null"}");
            }
        }

        private void DrawField(object component, FieldInfo field)
        {
            Type fieldType = field.FieldType;
            object? value = field.GetValue(component);

            if (fieldType.IsArray)
            {
                DrawArrayField(component, field, value);
                return;
            }

            if (value == null)
            {
                ImGui.Text($"{field.Name}: null");
                return;
            }

            if (fieldType == typeof(bool))
            {
                bool boolValue = (bool)value;
                if (ImGui.Checkbox(field.Name, ref boolValue))
                {
                    field.SetValue(component, boolValue);
                }
            }
            else if (fieldType == typeof(float))
            {
                float floatValue = (float)value;
                if (ImGui.InputFloat(field.Name, ref floatValue))
                {
                    field.SetValue(component, floatValue);
                }
            }
            else if (fieldType == typeof(int))
            {
                int intValue = (int)value;
                if (ImGui.InputInt(field.Name, ref intValue))
                {
                    field.SetValue(component, intValue);
                }
            }
            else if (fieldType == typeof(string))
            {
                string stringValue = (string)value;
                if (ImGui.InputText(field.Name, ref stringValue, 256))
                {
                    field.SetValue(component, stringValue);
                }
            }
            else if (fieldType == typeof(Vector2))
            {
                Vector2 vec2Value = (Vector2)value;
                if (ImGui.InputFloat2(field.Name, ref vec2Value))
                {
                    field.SetValue(component, vec2Value);
                }
            }
            else if (fieldType == typeof(Vector3))
            {
                Vector3 vec3Value = (Vector3)value;
                if (ImGui.InputFloat3(field.Name, ref vec3Value))
                {
                    field.SetValue(component, vec3Value);
                }
            }
            else if (fieldType == typeof(Vector4))
            {
                Vector4 vec4Value = (Vector4)value;
                if (ImGui.InputFloat4(field.Name, ref vec4Value))
                {
                    field.SetValue(component, vec4Value);
                }
            }
            else if (fieldType == typeof(Quaternion))
            {
                Quaternion quatValue = (Quaternion)value;
                Vector4 vec4 = new Vector4(quatValue.X, quatValue.Y, quatValue.Z, quatValue.W);
                if (ImGui.InputFloat4(field.Name, ref vec4))
                {
                    field.SetValue(component, new Quaternion(vec4.X, vec4.Y, vec4.Z, vec4.W));
                }
            }
            else if (fieldType == typeof(GameObject))
            {
                GameObject gameObjectValue = (GameObject)value;
                string displayName = gameObjectValue != null ? gameObjectValue.Name : "None";
                
                ImGui.Text($"{field.Name}:");
                ImGui.SameLine();
                
                if (ImGui.Button($"{displayName}###{field.Name}"))
                {
                    ImGui.OpenPopup($"SelectGameObject_{field.Name}");
                }
                
                if (ImGui.BeginPopup($"SelectGameObject_{field.Name}"))
                {
                    if (ImGui.MenuItem("None"))
                    {
                        field.SetValue(component, null);
                    }
                    
                    ImGui.Separator();
                    
                    var allObjects = SceneManager.ActiveScene?.GetAllGameObjects() ?? new System.Collections.Generic.List<GameObject>();
                    int objIndex = 0;
                    foreach (var obj in allObjects)
                    {
                        ImGui.PushID($"GO_{objIndex}");
                        if (ImGui.MenuItem(obj.Name))
                        {
                            field.SetValue(component, obj);
                        }
                        ImGui.PopID();
                        objIndex++;
                    }
                    
                    ImGui.EndPopup();
                }
            }
            else if (fieldType == typeof(KrayonCore.Material))
            {
                KrayonCore.Material materialValue = (KrayonCore.Material)value;
                string displayName = materialValue != null ? materialValue.Name : "None";
                
                ImGui.Text($"{field.Name}:");
                ImGui.SameLine();
                
                if (ImGui.Button($"{displayName}###{field.Name}"))
                {
                    ImGui.OpenPopup($"SelectMaterial_{field.Name}");
                }
                
                if (ImGui.BeginPopup($"SelectMaterial_{field.Name}"))
                {
                    if (ImGui.MenuItem("None"))
                    {
                        field.SetValue(component, null);
                    }
                    
                    ImGui.Separator();
                    
                    var allMaterials = GraphicsEngine.Instance.Materials.GetAll();
                    int matIndex = 0;
                    foreach (var mat in allMaterials)
                    {
                        ImGui.PushID($"Mat_{matIndex}");
                        if (ImGui.MenuItem(mat.Name))
                        {
                            field.SetValue(component, mat);
                        }
                        ImGui.PopID();
                        matIndex++;
                    }
                    
                    ImGui.EndPopup();
                }
            }
            else
            {
                ImGui.Text($"{field.Name}: {value}");
            }
        }

        private void DrawArrayField(object component, FieldInfo field, object? value)
        {
            Type elementType = field.FieldType.GetElementType()!;
            Array? array = value as Array;

            if (ImGui.TreeNode($"{field.Name} (Array)"))
            {
                int size = array?.Length ?? 0;
                if (ImGui.InputInt("Size", ref size))
                {
                    if (size < 0) size = 0;
                    Array newArray = Array.CreateInstance(elementType, size);
                    if (array != null)
                    {
                        int copyLength = Math.Min(array.Length, size);
                        Array.Copy(array, newArray, copyLength);
                    }
                    field.SetValue(component, newArray);
                    array = newArray;
                }

                if (array != null)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        ImGui.PushID($"Element_{i}");
                        DrawArrayElement(array, i, elementType);
                        ImGui.PopID();
                    }
                }

                ImGui.TreePop();
            }
        }

        private void DrawAddComponentButton()
        {
            ImGui.PushID("AddComponent");

            if (ImGui.Button("Add Component", new Vector2(-1, 30)))
            {
                ImGui.OpenPopup("AddComponentPopup");
            }

            if (ImGui.BeginPopup("AddComponentPopup"))
            {
                int componentIndex = 0;
                foreach (var componentType in ComponentRegistry.Components)
                {
                    ImGui.PushID($"ComponentType_{componentIndex}");
                    if (ImGui.MenuItem(componentType.Name))
                    {
                        SelectedObject!.AddComponent(componentType);
                        EngineEditor.LogMessage($"Added {componentType.Name} to {SelectedObject.Name}");
                    }
                    ImGui.PopID();
                    componentIndex++;
                }

                ImGui.EndPopup();
            }

            ImGui.PopID();
        }
    }
}