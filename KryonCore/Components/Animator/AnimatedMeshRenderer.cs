using KrayonCore.Core.Attributes;
using KrayonCore.GraphicsData;
using OpenTK.Graphics.OpenGL4;
using System;

namespace KrayonCore.Animation
{
    public class AnimatedMeshRenderer : Component, IRenderable
    {
        [NoSerializeToInspector] public RenderableType RenderType => RenderableType.Mesh;

        private string _modelPath = "";

        [ToStorage]
        public string ModelPath
        {
            get => _modelPath;
            set
            {
                if (_modelPath != value)
                {
                    _modelPath = value;
                    if (_isInitialized) OnModelPathChanged();
                }
            }
        }

        [ToStorage]
        public string[] MaterialPaths { get; set; } = new string[0];

        [NoSerializeToInspector] private AnimatedModel _model;
        [NoSerializeToInspector] private Animator _animator;
        [NoSerializeToInspector] private bool _isInitialized = false;

        [NoSerializeToInspector] public AnimatedModel AnimatedModel => _model;

        public AnimatedMeshRenderer() { }

        public override void Awake()
        {
            GraphicsEngine.Instance?.GetSceneRenderer()?.RegisterRenderer(this);

            if (!string.IsNullOrEmpty(ModelPath))
            {
                LoadModelFromPath(ModelPath);
                SyncMaterialSlots();
            }

            _animator = GameObject.GetComponent<Animator>();
            if (_animator == null)
            {
                GameObject.AddComponent<Animator>();
            }

            if (_model != null)
                _animator.SetModel(_model);

            _isInitialized = true;
        }

        public override void Start()
        {
            if (_model == null && !string.IsNullOrEmpty(ModelPath))
            {
                LoadModelFromPath(ModelPath);
                SyncMaterialSlots();

                if (_model != null && _animator != null)
                    _animator.SetModel(_model);
            }
        }

        public override void Update(float deltaTime)
        {
            // El Animator se actualiza solo vía su propio Update
        }

        private void OnModelPathChanged()
        {
            MaterialPaths = new string[0];
            LoadModelFromPath(ModelPath);
            SyncMaterialSlots();

            if (_model != null && _animator != null)
                _animator.SetModel(_model);
        }

        private void SyncMaterialSlots()
        {
            if (_model != null && _model.SubMeshCount > 0)
            {
                if (MaterialPaths.Length != _model.SubMeshCount)
                {
                    var oldPaths = MaterialPaths;
                    MaterialPaths = new string[_model.SubMeshCount];
                    for (int i = 0; i < Math.Min(oldPaths.Length, MaterialPaths.Length); i++)
                        MaterialPaths[i] = oldPaths[i];
                }
            }
        }

        protected virtual void LoadModelFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var assetInfo = AssetManager.GetBytes(Guid.Parse(path));
                _model = AnimatedModel.LoadFromBytes(assetInfo, "fbx");
                Console.WriteLine($"[AnimatedMeshRenderer] Modelo cargado: {_model.SubMeshCount} submeshes, {_model.BoneCount} huesos, {_model.Animations.Count} animaciones");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AnimatedMeshRenderer] Error cargando modelo: {ex.Message}");
                _model = null;
            }
        }

        public Material GetMaterial(int index)
        {
            if (index < 0 || index >= MaterialPaths.Length || string.IsNullOrEmpty(MaterialPaths[index]))
                return null;
            return GraphicsEngine.Instance.Materials.Get(MaterialPaths[index]);
        }

        public Animator GetAnimator() => _animator;

        public override void OnDestroy()
        {
            GraphicsEngine.Instance?.GetSceneRenderer()?.UnregisterRenderer(this);
            _model?.Dispose();
            _model = null;
        }
    }
}