using KrayonCore.Components.RenderComponents;
using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace KrayonCore.Components
{
    public class EntityLoader : Component
    {
        [NoSerializeToInspector] public GameScene Scene = null;

        private string _entity = string.Empty;

        [ToStorage]
        public string Entity
        {
            get => _entity;
            set
            {
                if (_entity == value) return;
                _entity = value;
                OnEntityChanged();
            }
        }

        private void OnEntityChanged()
        {
            if (string.IsNullOrEmpty(_entity)) return;

            if (!Guid.TryParse(_entity, out Guid guid)) return;

            Scene?.Dispose();

            byte[] bytes = AssetManager.GetBytes(guid);
            Scene = SceneManager.LoadSceneOnlyFromBytes(bytes, SelfScene.SelfRenderScene);
            Start();
        }

        public override void Awake()
        {
            OnEntityChanged();
        }

        public override void Start()
        {
            Scene?.Start();
        }

        public override void OnWillRenderObject()
        {
            if (!AppInfo.IsPlayingGame)
                Scene.WorldMatrix = GameObject.Transform.GetWorldMatrix();

            Scene?.Render();
        }

        public override void Update(float deltaTime)
        {
            if (AppInfo.IsPlayingGame)
                Scene.WorldMatrix = GameObject.Transform.GetWorldMatrix();

            Scene?.Update(deltaTime);
        }

        public override void OnDestroy()
        {
            Scene?.Dispose();
        }
    }
}
