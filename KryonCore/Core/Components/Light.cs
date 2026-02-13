using KrayonCore.GraphicsData;
using OpenTK.Mathematics;
using System;

namespace KrayonCore.Components
{
    public enum LightType
    {
        Directional,
        Point,
        Spot
    }

    public class Light : Component
    {
        private LightType _lightType = LightType.Directional;

        [ToStorage]
        public LightType Type
        {
            get => _lightType;
            set
            {
                if (_lightType != value)
                {
                    LightType oldType = _lightType;
                    _lightType = value;
                    RecreateLight(oldType);
                }
            }
        }

        [ToStorage]
        public Vector3 Color { get; set; } = Vector3.One;

        [ToStorage, Range(0.0f, 1000.0f)]
        public float Intensity { get; set; } = 1.0f;

        [ToStorage, Range(0.0f, 5.0f)]
        public float Constant { get; set; } = 1.0f;

        [ToStorage, Range(0.0f, 1.0f)]
        public float Linear { get; set; } = 0.09f;

        [ToStorage, Range(0.0f, 1.0f)]
        public float Quadratic { get; set; } = 0.032f;

        [ToStorage, Range(0.0f, 100.0f)]
        public float InnerCutOffDegrees { get; set; } = 12.5f;

        [ToStorage, Range(0.0f, 100.0f)]
        public float OuterCutOffDegrees { get; set; } = 17.5f;

        private LightingSystem.Light _currentLight;
        private bool _addedToManager = false;
        private Transform _transform;

        public override void Awake()
        {
            base.Awake();
            _transform = GetComponent<Transform>();

            if (_transform == null)
            {
                Console.WriteLine("[Light] Warning: No Transform component found!");
            }

            CreateLight();
        }

        public override void Start()
        {
            base.Start();
            AddLightToManager();
        }

        public override void OnWillRenderObject()
        {
            if (_currentLight != null)
            {
                UpdateLightProperties();
            }
        }

        public override void OnDestroy()
        {
            RemoveLightFromManager();
            base.OnDestroy();
        }

        private void CreateLight()
        {
            Vector3 position = _transform != null ? _transform.Position : Vector3.Zero;
            Vector3 direction = _transform != null ? _transform.Forward : new Vector3(0, -1, 0);

            switch (_lightType)
            {
                case LightType.Directional:
                    _currentLight = new LightingSystem.DirectionalLight
                    {
                        Direction = direction,
                        Color = Color,
                        Intensity = Intensity,
                        Enabled = Enabled
                    };
                    break;

                case LightType.Point:
                    _currentLight = new LightingSystem.PointLight
                    {
                        Position = position,
                        Color = Color,
                        Intensity = Intensity,
                        Constant = Constant,
                        Linear = Linear,
                        Quadratic = Quadratic,
                        Enabled = Enabled
                    };
                    break;

                case LightType.Spot:
                    _currentLight = new LightingSystem.SpotLight
                    {
                        Position = position,
                        Direction = direction,
                        Color = Color,
                        Intensity = Intensity,
                        InnerCutOff = MathHelper.DegreesToRadians(InnerCutOffDegrees),
                        OuterCutOff = MathHelper.DegreesToRadians(OuterCutOffDegrees),
                        Constant = Constant,
                        Linear = Linear,
                        Quadratic = Quadratic,
                        Enabled = Enabled
                    };
                    break;
            }
        }

        private void RecreateLight(LightType oldType)
        {
            bool wasEnabled = _addedToManager;

            RemoveLightFromManagerByType(oldType);
            _currentLight = null;
            _addedToManager = false;

            CreateLight();

            if (wasEnabled && _currentLight != null)
            {
                AddLightToManager();
            }
        }

        private void AddLightToManager()
        {
            if (_currentLight == null || _addedToManager)
                return;

            var lightManager = GraphicsEngine.Instance.GetSceneRenderer().GetLightManager();
            if (lightManager == null)
            {
                Console.WriteLine("[Light] Warning: LightManager not available!");
                return;
            }

            bool success = false;

            switch (_lightType)
            {
                case LightType.Directional:
                    success = lightManager.AddDirectionalLight(_currentLight as LightingSystem.DirectionalLight);
                    break;

                case LightType.Point:
                    success = lightManager.AddPointLight(_currentLight as LightingSystem.PointLight);
                    break;

                case LightType.Spot:
                    success = lightManager.AddSpotLight(_currentLight as LightingSystem.SpotLight);
                    break;
            }

            if (success)
            {
                _addedToManager = true;
                Console.WriteLine($"[Light] Successfully added {_lightType} light to manager");
            }
            else
            {
                Console.WriteLine($"[Light] No se pudo agregar luz de tipo {_lightType}. Límite alcanzado.");
            }
        }

        private void RemoveLightFromManager()
        {
            if (_currentLight == null || !_addedToManager)
                return;

            var lightManager = GraphicsEngine.Instance.GetSceneRenderer().GetLightManager();
            if (lightManager == null)
                return;

            bool removed = false;

            switch (_lightType)
            {
                case LightType.Directional:
                    removed = lightManager.RemoveDirectionalLight(_currentLight as LightingSystem.DirectionalLight);
                    break;

                case LightType.Point:
                    removed = lightManager.RemovePointLight(_currentLight as LightingSystem.PointLight);
                    break;

                case LightType.Spot:
                    removed = lightManager.RemoveSpotLight(_currentLight as LightingSystem.SpotLight);
                    break;
            }

            if (removed)
            {
                _addedToManager = false;
                Console.WriteLine($"[Light] Successfully removed {_lightType} light from manager");
            }
            else
            {
                Console.WriteLine($"[Light] Warning: No se pudo remover luz de tipo {_lightType} del manager.");
            }
        }

        private void RemoveLightFromManagerByType(LightType type)
        {
            if (_currentLight == null || !_addedToManager)
                return;

            var lightManager = GraphicsEngine.Instance.GetSceneRenderer().GetLightManager();
            if (lightManager == null)
                return;

            bool removed = false;

            switch (type)
            {
                case LightType.Directional:
                    removed = lightManager.RemoveDirectionalLight(_currentLight as LightingSystem.DirectionalLight);
                    break;

                case LightType.Point:
                    removed = lightManager.RemovePointLight(_currentLight as LightingSystem.PointLight);
                    break;

                case LightType.Spot:
                    removed = lightManager.RemoveSpotLight(_currentLight as LightingSystem.SpotLight);
                    break;
            }

            if (removed)
            {
                _addedToManager = false;
                Console.WriteLine($"[Light] Successfully removed old {type} light from manager");
            }
            else
            {
                Console.WriteLine($"[Light] Warning: No se pudo remover luz de tipo {type} del manager.");
            }
        }

        private void UpdateLightProperties()
        {
            if (_currentLight == null)
                return;

            _currentLight.Color = Color;
            _currentLight.Intensity = Intensity;
            _currentLight.Enabled = Enabled;

            Vector3 position = _transform != null ? _transform.Position : Vector3.Zero;
            Vector3 direction = _transform != null ? _transform.Forward : new Vector3(0, -1, 0);

            switch (_lightType)
            {
                case LightType.Directional:
                    var dirLight = _currentLight as LightingSystem.DirectionalLight;
                    if (dirLight != null)
                    {
                        dirLight.Direction = direction;
                    }
                    break;

                case LightType.Point:
                    var pointLight = _currentLight as LightingSystem.PointLight;
                    if (pointLight != null)
                    {
                        pointLight.Position = position;
                        pointLight.Constant = Constant;
                        pointLight.Linear = Linear;
                        pointLight.Quadratic = Quadratic;
                    }
                    break;

                case LightType.Spot:
                    var spotLight = _currentLight as LightingSystem.SpotLight;
                    if (spotLight != null)
                    {
                        spotLight.Position = position;
                        spotLight.Direction = direction;
                        spotLight.InnerCutOff = MathHelper.DegreesToRadians(InnerCutOffDegrees);
                        spotLight.OuterCutOff = MathHelper.DegreesToRadians(OuterCutOffDegrees);
                        spotLight.Constant = Constant;
                        spotLight.Linear = Linear;
                        spotLight.Quadratic = Quadratic;
                    }
                    break;
            }
        }

        public LightingSystem.Light GetLight() => _currentLight;

        public Vector3 GetPosition()
        {
            return _transform != null ? _transform.Position : Vector3.Zero;
        }

        public Vector3 GetDirection()
        {
            return _transform != null ? _transform.Forward : new Vector3(0, -1, 0);
        }
    }
}