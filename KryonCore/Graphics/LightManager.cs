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
        public float InnerCutOff { get; set; }
        public float OuterCutOff { get; set; }
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
        private const int MAX_DIRECTIONAL_LIGHTS = 32;
        private const int MAX_POINT_LIGHTS = 32;
        private const int MAX_SPOT_LIGHTS = 32;

        private List<DirectionalLight> directionalLights = new();
        private List<PointLight> pointLights = new();
        private List<SpotLight> spotLights = new();

        // ── Add ──────────────────────────────────────────────────────────────
        public bool AddDirectionalLight(DirectionalLight light)
        {
            if (directionalLights.Count < MAX_DIRECTIONAL_LIGHTS) { directionalLights.Add(light); return true; }
            return false;
        }
        public bool AddPointLight(PointLight light)
        {
            if (pointLights.Count < MAX_POINT_LIGHTS) { pointLights.Add(light); return true; }
            return false;
        }
        public bool AddSpotLight(SpotLight light)
        {
            if (spotLights.Count < MAX_SPOT_LIGHTS) { spotLights.Add(light); return true; }
            return false;
        }

        // ── Remove ───────────────────────────────────────────────────────────
        public bool RemoveDirectionalLight(DirectionalLight light) => directionalLights.Remove(light);
        public bool RemovePointLight(PointLight light) => pointLights.Remove(light);
        public bool RemoveSpotLight(SpotLight light) => spotLights.Remove(light);

        public void RemoveDirectionalLightAt(int i) { if (i >= 0 && i < directionalLights.Count) directionalLights.RemoveAt(i); }
        public void RemovePointLightAt(int i) { if (i >= 0 && i < pointLights.Count) pointLights.RemoveAt(i); }
        public void RemoveSpotLightAt(int i) { if (i >= 0 && i < spotLights.Count) spotLights.RemoveAt(i); }

        // ── Clear ────────────────────────────────────────────────────────────
        public void Clear() { directionalLights.Clear(); pointLights.Clear(); spotLights.Clear(); }
        public void ClearDirectionalLights() => directionalLights.Clear();
        public void ClearPointLights() => pointLights.Clear();
        public void ClearSpotLights() => spotLights.Clear();

        // ── Get (read-only lists) ─────────────────────────────────────────────
        public IReadOnlyList<DirectionalLight> GetDirectionalLights() => directionalLights;
        public IReadOnlyList<PointLight> GetPointLights() => pointLights;
        public IReadOnlyList<SpotLight> GetSpotLights() => spotLights;

        public DirectionalLight? GetDirectionalLight(int i) => (i >= 0 && i < directionalLights.Count) ? directionalLights[i] : null;
        public PointLight? GetPointLight(int i) => (i >= 0 && i < pointLights.Count) ? pointLights[i] : null;
        public SpotLight? GetSpotLight(int i) => (i >= 0 && i < spotLights.Count) ? spotLights[i] : null;

        // ── Apply to shader ───────────────────────────────────────────────────
        public void ApplyLightsToShader(int shaderProgram)
        {
            int activeDirLights = 0, activePointLights = 0, activeSpotLights = 0;

            for (int i = 0; i < directionalLights.Count; i++)
                if (directionalLights[i].Enabled)
                    directionalLights[i].ApplyToShader(shaderProgram, $"dirLights[{activeDirLights++}]");

            foreach (var light in pointLights)
                if (light.Enabled)
                    light.ApplyToShader(shaderProgram, $"pointLights[{activePointLights++}]");

            foreach (var light in spotLights)
                if (light.Enabled)
                    light.ApplyToShader(shaderProgram, $"spotLights[{activeSpotLights++}]");

            GL.Uniform1(GL.GetUniformLocation(shaderProgram, "numDirLights"), activeDirLights);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, "numPointLights"), activePointLights);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, "numSpotLights"), activeSpotLights);
        }

        // ── Counters / utils ──────────────────────────────────────────────────
        public int GetDirectionalLightCount() => directionalLights.Count;
        public int GetPointLightCount() => pointLights.Count;
        public int GetSpotLightCount() => spotLights.Count;
        public int GetMaxDirectionalLights() => MAX_DIRECTIONAL_LIGHTS;
        public int GetMaxPointLights() => MAX_POINT_LIGHTS;
        public int GetMaxSpotLights() => MAX_SPOT_LIGHTS;

        public void EnableAllLights()
        {
            foreach (var l in directionalLights) l.Enabled = true;
            foreach (var l in pointLights) l.Enabled = true;
            foreach (var l in spotLights) l.Enabled = true;
        }

        public void DisableAllLights()
        {
            foreach (var l in directionalLights) l.Enabled = false;
            foreach (var l in pointLights) l.Enabled = false;
            foreach (var l in spotLights) l.Enabled = false;
        }

        public int GetTotalLightCount() => directionalLights.Count + pointLights.Count + spotLights.Count;

        public int GetActiveLightCount()
        {
            int c = 0;
            foreach (var l in directionalLights) if (l.Enabled) c++;
            foreach (var l in pointLights) if (l.Enabled) c++;
            foreach (var l in spotLights) if (l.Enabled) c++;
            return c;
        }
    }
}