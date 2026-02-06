using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace LightingSystem
{
    // ==================== CLASE BASE DE LUZ ====================
    public abstract class Light
    {
        public Vector3 Color { get; set; }
        public float Intensity { get; set; }
        public bool Enabled { get; set; }

        protected Light()
        {
            Color = Vector3.One;
            Intensity = 1.0f;
            Enabled = true;
        }

        public abstract void ApplyToShader(int shaderProgram, string uniformPrefix);
    }

    // ==================== LUZ DIRECCIONAL ====================
    public class DirectionalLight : Light
    {
        public Vector3 Direction { get; set; }

        public DirectionalLight()
        {
            Direction = new Vector3(0, -1, 0);
        }

        public override void ApplyToShader(int shaderProgram, string uniformPrefix)
        {
            GL.Uniform3(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.direction"), Direction);
            GL.Uniform3(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.color"), Color);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.intensity"), Intensity);
        }
    }

    // ==================== LUZ PUNTUAL ====================
    public class PointLight : Light
    {
        public Vector3 Position { get; set; }

        // Atenuación
        public float Constant { get; set; }
        public float Linear { get; set; }
        public float Quadratic { get; set; }

        public PointLight()
        {
            Position = Vector3.Zero;
            Constant = 1.0f;
            Linear = 0.09f;
            Quadratic = 0.032f;
        }

        public override void ApplyToShader(int shaderProgram, string uniformPrefix)
        {
            GL.Uniform3(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.position"), Position);
            GL.Uniform3(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.color"), Color);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.intensity"), Intensity);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.constant"), Constant);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.linear"), Linear);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.quadratic"), Quadratic);
        }
    }

    // ==================== SPOTLIGHT ====================
    public class SpotLight : Light
    {
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }

        // Ángulos del cono de luz (en radianes)
        public float InnerCutOff { get; set; }
        public float OuterCutOff { get; set; }

        // Atenuación
        public float Constant { get; set; }
        public float Linear { get; set; }
        public float Quadratic { get; set; }

        public SpotLight()
        {
            Position = Vector3.Zero;
            Direction = new Vector3(0, -1, 0);
            InnerCutOff = MathHelper.DegreesToRadians(12.5f);
            OuterCutOff = MathHelper.DegreesToRadians(17.5f);
            Constant = 1.0f;
            Linear = 0.09f;
            Quadratic = 0.032f;
        }

        public override void ApplyToShader(int shaderProgram, string uniformPrefix)
        {
            GL.Uniform3(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.position"), Position);
            GL.Uniform3(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.direction"), Direction);
            GL.Uniform3(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.color"), Color);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.intensity"), Intensity);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.innerCutOff"), MathF.Cos(InnerCutOff));
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.outerCutOff"), MathF.Cos(OuterCutOff));
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.constant"), Constant);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.linear"), Linear);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, $"{uniformPrefix}.quadratic"), Quadratic);
        }
    }

    // ==================== LIGHT MANAGER ====================
    public class LightManager
    {
        // Máximo de 32 luces por defecto para cada tipo
        private const int MAX_DIRECTIONAL_LIGHTS = 32;
        private const int MAX_POINT_LIGHTS = 32;
        private const int MAX_SPOT_LIGHTS = 32;

        private List<DirectionalLight> directionalLights;
        private List<PointLight> pointLights;
        private List<SpotLight> spotLights;

        public LightManager()
        {
            directionalLights = new List<DirectionalLight>();
            pointLights = new List<PointLight>();
            spotLights = new List<SpotLight>();
        }

        // ========== AGREGAR LUCES ==========

        public bool AddDirectionalLight(DirectionalLight light)
        {
            if (directionalLights.Count < MAX_DIRECTIONAL_LIGHTS)
            {
                directionalLights.Add(light);
                return true;
            }
            return false;
        }

        public bool AddPointLight(PointLight light)
        {
            if (pointLights.Count < MAX_POINT_LIGHTS)
            {
                pointLights.Add(light);
                return true;
            }
            return false;
        }

        public bool AddSpotLight(SpotLight light)
        {
            if (spotLights.Count < MAX_SPOT_LIGHTS)
            {
                spotLights.Add(light);
                return true;
            }
            return false;
        }

        // ========== ELIMINAR LUCES ==========

        public bool RemoveDirectionalLight(DirectionalLight light)
        {
            return directionalLights.Remove(light);
        }

        public bool RemovePointLight(PointLight light)
        {
            return pointLights.Remove(light);
        }

        public bool RemoveSpotLight(SpotLight light)
        {
            return spotLights.Remove(light);
        }

        // ========== ELIMINAR POR ÍNDICE ==========

        public void RemoveDirectionalLightAt(int index)
        {
            if (index >= 0 && index < directionalLights.Count)
                directionalLights.RemoveAt(index);
        }

        public void RemovePointLightAt(int index)
        {
            if (index >= 0 && index < pointLights.Count)
                pointLights.RemoveAt(index);
        }

        public void RemoveSpotLightAt(int index)
        {
            if (index >= 0 && index < spotLights.Count)
                spotLights.RemoveAt(index);
        }

        // ========== LIMPIAR TODAS ==========

        public void Clear()
        {
            directionalLights.Clear();
            pointLights.Clear();
            spotLights.Clear();
        }

        public void ClearDirectionalLights()
        {
            directionalLights.Clear();
        }

        public void ClearPointLights()
        {
            pointLights.Clear();
        }

        public void ClearSpotLights()
        {
            spotLights.Clear();
        }

        // ========== OBTENER LUCES ==========

        public IReadOnlyList<DirectionalLight> GetDirectionalLights() => directionalLights;
        public IReadOnlyList<PointLight> GetPointLights() => pointLights;
        public IReadOnlyList<SpotLight> GetSpotLights() => spotLights;

        public DirectionalLight GetDirectionalLight(int index)
        {
            if (index >= 0 && index < directionalLights.Count)
                return directionalLights[index];
            return null;
        }

        public PointLight GetPointLight(int index)
        {
            if (index >= 0 && index < pointLights.Count)
                return pointLights[index];
            return null;
        }

        public SpotLight GetSpotLight(int index)
        {
            if (index >= 0 && index < spotLights.Count)
                return spotLights[index];
            return null;
        }

        // ========== MÉTODO PRINCIPAL: APLICAR AL SHADER ==========

        public void ApplyLightsToShader(int shaderProgram)
        {
            int activeDirLights = 0;
            int activePointLights = 0;
            int activeSpotLights = 0;

            // Aplicar luces direccionales
            for (int i = 0; i < directionalLights.Count; i++)
            {
                if (directionalLights[i].Enabled)
                {
                    directionalLights[i].ApplyToShader(shaderProgram, $"dirLights[{activeDirLights}]");
                    activeDirLights++;
                }
            }

            // Aplicar luces puntuales
            for (int i = 0; i < pointLights.Count; i++)
            {
                if (pointLights[i].Enabled)
                {
                    pointLights[i].ApplyToShader(shaderProgram, $"pointLights[{activePointLights}]");
                    activePointLights++;
                }
            }

            // Aplicar spotlights
            for (int i = 0; i < spotLights.Count; i++)
            {
                if (spotLights[i].Enabled)
                {
                    spotLights[i].ApplyToShader(shaderProgram, $"spotLights[{activeSpotLights}]");
                    activeSpotLights++;
                }
            }

            // Enviar número de luces activas al shader
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, "numDirLights"), activeDirLights);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, "numPointLights"), activePointLights);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, "numSpotLights"), activeSpotLights);
        }

        // ========== CONTADORES ==========

        public int GetDirectionalLightCount() => directionalLights.Count;
        public int GetPointLightCount() => pointLights.Count;
        public int GetSpotLightCount() => spotLights.Count;

        public int GetMaxDirectionalLights() => MAX_DIRECTIONAL_LIGHTS;
        public int GetMaxPointLights() => MAX_POINT_LIGHTS;
        public int GetMaxSpotLights() => MAX_SPOT_LIGHTS;

        // ========== UTILIDADES ==========

        public void EnableAllLights()
        {
            foreach (var light in directionalLights) light.Enabled = true;
            foreach (var light in pointLights) light.Enabled = true;
            foreach (var light in spotLights) light.Enabled = true;
        }

        public void DisableAllLights()
        {
            foreach (var light in directionalLights) light.Enabled = false;
            foreach (var light in pointLights) light.Enabled = false;
            foreach (var light in spotLights) light.Enabled = false;
        }

        public int GetTotalLightCount()
        {
            return directionalLights.Count + pointLights.Count + spotLights.Count;
        }

        public int GetActiveLightCount()
        {
            int count = 0;
            foreach (var light in directionalLights) if (light.Enabled) count++;
            foreach (var light in pointLights) if (light.Enabled) count++;
            foreach (var light in spotLights) if (light.Enabled) count++;
            return count;
        }
    }
}
