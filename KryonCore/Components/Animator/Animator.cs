using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore.Animation
{
    public class Animator : Component
    {
        public const int MAX_BONES = 256;

        private Matrix4[] _finalBoneMatrices;
        private AnimatedModel _animatedModel;
        private AnimationClip _currentClip;
        private float _currentTime = 0f;
        private float _playbackSpeed = 1f;
        private bool _isPlaying = false;
        private bool _loop = true;

        private AnimationClip _blendFromClip;
        private float _blendFromTime;
        private float _blendFactor = 0f;
        private float _blendDuration = 0f;
        private float _blendElapsed = 0f;
        private bool _isBlending = false;

        [NoSerializeToInspector]
        public Matrix4[] FinalBoneMatrices => _finalBoneMatrices;

        [NoSerializeToInspector]
        public bool IsPlaying => _isPlaying;

        [NoSerializeToInspector]
        public float CurrentTime => _currentTime;

        [NoSerializeToInspector]
        public AnimationClip CurrentClip => _currentClip;

        [ToStorage]
        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = value;
        }

        [ToStorage]
        public bool Loop
        {
            get => _loop;
            set => _loop = value;
        }

        public Animator()
        {
            _finalBoneMatrices = new Matrix4[MAX_BONES];
            for (int i = 0; i < MAX_BONES; i++)
                _finalBoneMatrices[i] = Matrix4.Identity;
        }

        public override void Awake() { }
        public override void Start() { }

        public void SetModel(AnimatedModel model)
        {
            _animatedModel = model;

            if (model != null && model.Animations.Count > 0)
            {
                SetAnimation(0);
                Play();
            }
        }

        public void SetAnimation(int index)
        {
            if (_animatedModel == null || index < 0 || index >= _animatedModel.Animations.Count)
                return;

            _currentClip = _animatedModel.Animations[index];
            _currentClip.RootNode = _animatedModel.RootNode;
            _currentTime = 0f;
        }

        public void SetAnimation(string name)
        {
            if (_animatedModel == null) return;

            for (int i = 0; i < _animatedModel.Animations.Count; i++)
            {
                if (_animatedModel.Animations[i].Name == name)
                {
                    SetAnimation(i);
                    return;
                }
            }

            Console.WriteLine($"[Animator] Animación no encontrada: {name}");
        }

        public void CrossFade(string animationName, float blendDuration = 0.3f)
        {
            if (_animatedModel == null) return;

            AnimationClip targetClip = null;
            foreach (var clip in _animatedModel.Animations)
            {
                if (clip.Name == animationName)
                {
                    targetClip = clip;
                    break;
                }
            }

            if (targetClip == null || targetClip == _currentClip) return;

            _blendFromClip = _currentClip;
            _blendFromTime = _currentTime;
            _currentClip = targetClip;
            _currentClip.RootNode = _animatedModel.RootNode;
            _currentTime = 0f;
            _blendDuration = blendDuration;
            _blendElapsed = 0f;
            _isBlending = true;
        }

        public void CrossFade(int animationIndex, float blendDuration = 0.3f)
        {
            if (_animatedModel == null || animationIndex < 0 || animationIndex >= _animatedModel.Animations.Count)
                return;
            CrossFade(_animatedModel.Animations[animationIndex].Name, blendDuration);
        }

        public void Play() => _isPlaying = true;
        public void Pause() => _isPlaying = false;

        public void Stop()
        {
            _isPlaying = false;
            _currentTime = 0f;
        }

        public override void OnWillRenderObject()
        {
            if (!_isPlaying || _currentClip == null || _animatedModel == null)
                return;

            float tickRate = _currentClip.TicksPerSecond;
            _currentTime += TimerData.DeltaTime * tickRate * _playbackSpeed;

            if (_currentTime >= _currentClip.Duration)
            {
                if (_loop)
                    _currentTime = _currentTime % _currentClip.Duration;
                else
                {
                    _currentTime = _currentClip.Duration;
                    _isPlaying = false;
                }
            }

            if (_isBlending)
            {
                _blendElapsed += TimerData.DeltaTime;
                _blendFactor = Math.Clamp(_blendElapsed / _blendDuration, 0f, 1f);

                _blendFromTime += TimerData.DeltaTime * _blendFromClip.TicksPerSecond * _playbackSpeed;
                if (_blendFromTime >= _blendFromClip.Duration)
                    _blendFromTime = _blendFromTime % _blendFromClip.Duration;

                if (_blendFactor >= 1f)
                    _isBlending = false;
            }

            CalculateBoneTransforms();
        }

        private void CalculateBoneTransforms()
        {
            // La inversa del transform raíz de Assimp elimina la escala/rotación
            // que FBX/Collada suelen inyectar en el nodo raíz de la escena
            // (p.ej. x100 en FBX por la conversión cm→m).
            Matrix4 globalInverse = _animatedModel.GlobalInverseTransform;

            if (_isBlending && _blendFromClip != null)
            {
                var matricesA = new Matrix4[MAX_BONES];
                var matricesB = new Matrix4[MAX_BONES];

                for (int i = 0; i < MAX_BONES; i++)
                {
                    matricesA[i] = Matrix4.Identity;
                    matricesB[i] = Matrix4.Identity;
                }

                CalculateNodeTransform(_blendFromClip, _blendFromTime, _animatedModel.RootNode, Matrix4.Identity, matricesA, globalInverse);
                CalculateNodeTransform(_currentClip, _currentTime, _animatedModel.RootNode, Matrix4.Identity, matricesB, globalInverse);

                for (int i = 0; i < MAX_BONES; i++)
                    _finalBoneMatrices[i] = LerpMatrix(matricesA[i], matricesB[i], _blendFactor);
            }
            else
            {
                for (int i = 0; i < MAX_BONES; i++)
                    _finalBoneMatrices[i] = Matrix4.Identity;

                CalculateNodeTransform(_currentClip, _currentTime, _animatedModel.RootNode, Matrix4.Identity, _finalBoneMatrices, globalInverse);
            }
        }

        private void CalculateNodeTransform(AnimationClip clip, float time, NodeData node, Matrix4 parentTransform, Matrix4[] outMatrices, Matrix4 globalInverse)
        {
            Matrix4 nodeTransform = node.Transform;

            var boneAnim = clip.FindBoneAnimation(node.Name);
            if (boneAnim != null)
            {
                // Descomponer la matriz de bind pose como fallback
                // para los canales que no tengan keyframes propios.
                // Muy común en huesos IK/pies que solo tienen rotación.
                DecomposeMatrix(node.Transform,
                    out Vector3 bindTrans, out OpenTK.Mathematics.Quaternion bindRot, out Vector3 bindScale);

                Vector3 translation = boneAnim.Positions.Count > 0
                    ? boneAnim.InterpolatePosition(time)
                    : bindTrans;

                OpenTK.Mathematics.Quaternion rotation = boneAnim.Rotations.Count > 0
                    ? boneAnim.InterpolateRotation(time)
                    : bindRot;

                Vector3 scale = boneAnim.Scales.Count > 0
                    ? boneAnim.InterpolateScale(time)
                    : bindScale;

                Matrix4 T = Matrix4.CreateTranslation(translation);
                Matrix4 R = Matrix4.CreateFromQuaternion(rotation);
                Matrix4 S = Matrix4.CreateScale(scale);

                nodeTransform = S * R * T;
            }

            Matrix4 globalTransform = nodeTransform * parentTransform;

            if (_animatedModel.BoneInfoMap.TryGetValue(node.Name, out var boneInfo))
            {
                int boneId = boneInfo.Id;
                if (boneId >= 0 && boneId < MAX_BONES)
                {
                    // Fórmula correcta de skinning (row-major OpenTK):
                    // OffsetMatrix   → bind-pose inverse (mesh space → bone space)
                    // globalTransform → pose actual del hueso en scene space
                    // globalInverse  → cancela el transform del nodo raíz de Assimp
                    outMatrices[boneId] = boneInfo.OffsetMatrix * globalTransform * globalInverse;
                }
            }

            foreach (var child in node.Children)
                CalculateNodeTransform(clip, time, child, globalTransform, outMatrices, globalInverse);
        }

        // UBO para las matrices de huesos — se crea una vez por Animator
        private int _boneUBO = -1;
        private const int BONE_UBO_BINDING = 1; // binding point, debe coincidir con el shader

        private void EnsureBoneUBO()
        {
            if (_boneUBO != -1) return;
            _boneUBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, _boneUBO);
            // Reservar espacio: MAX_BONES × 64 bytes (una mat4)
            GL.BufferData(BufferTarget.UniformBuffer, MAX_BONES * 64, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        /// <summary>
        /// Enviar las matrices de huesos al shader vía UBO.
        /// Llamar antes de dibujar el mesh animado.
        /// </summary>
        public void UploadBoneMatrices(int shaderProgram)
        {
            int instLoc = GL.GetUniformLocation(shaderProgram, "u_UseInstancing");
            if (instLoc != -1)
                GL.Uniform1(instLoc, 0);

            int useAnimLoc = GL.GetUniformLocation(shaderProgram, "u_UseAnimation");
            if (useAnimLoc != -1)
                GL.Uniform1(useAnimLoc, 1);

            EnsureBoneUBO();

            // World matrix del transform del GameObject
            Matrix4 worldMatrix = GameObject?.Transform?.GetWorldMatrix() ?? Matrix4.Identity;

            // Construir array flat de matrices combinadas (bone × world)
            var matrices = new Matrix4[MAX_BONES];
            int boneCount = Math.Min(_animatedModel?.BoneCount ?? 0, MAX_BONES);
            for (int i = 0; i < boneCount; i++)
                matrices[i] = _finalBoneMatrices[i] * worldMatrix;
            for (int i = boneCount; i < MAX_BONES; i++)
                matrices[i] = Matrix4.Identity;

            // Subir al UBO de una sola vez (mucho más eficiente que N llamadas uniform)
            GL.BindBuffer(BufferTarget.UniformBuffer, _boneUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, MAX_BONES * 64, matrices);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            // Vincular el UBO al binding point del shader
            int blockIndex = GL.GetUniformBlockIndex(shaderProgram, "BoneMatricesBlock");
            if (blockIndex != -1)
            {
                GL.UniformBlockBinding(shaderProgram, blockIndex, BONE_UBO_BINDING);
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, BONE_UBO_BINDING, _boneUBO);
            }
        }

        public static void DisableAnimation(int shaderProgram)
        {
            int useAnimLoc = GL.GetUniformLocation(shaderProgram, "u_UseAnimation");
            if (useAnimLoc != -1)
                GL.Uniform1(useAnimLoc, 0);
        }

        public string[] GetAnimationNames()
        {
            if (_animatedModel == null) return Array.Empty<string>();

            var names = new string[_animatedModel.Animations.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = _animatedModel.Animations[i].Name;
            return names;
        }

        public int GetAnimationCount()
        {
            return _animatedModel?.Animations.Count ?? 0;
        }

        private static Matrix4 LerpMatrix(Matrix4 a, Matrix4 b, float t)
        {
            return new Matrix4(
                Vector4.Lerp(a.Row0, b.Row0, t),
                Vector4.Lerp(a.Row1, b.Row1, t),
                Vector4.Lerp(a.Row2, b.Row2, t),
                Vector4.Lerp(a.Row3, b.Row3, t)
            );
        }

        /// <summary>
        /// Descompone una Matrix4 (row-major OpenTK) en traslación, rotación y escala.
        /// </summary>
        private static void DecomposeMatrix(Matrix4 m,
            out Vector3 translation, out OpenTK.Mathematics.Quaternion rotation, out Vector3 scale)
        {
            // Traslación: última fila (row-major OpenTK)
            translation = new Vector3(m.Row3.X, m.Row3.Y, m.Row3.Z);

            // Escala: longitud de cada columna (las 3 primeras filas)
            Vector3 col0 = new Vector3(m.Row0.X, m.Row1.X, m.Row2.X);
            Vector3 col1 = new Vector3(m.Row0.Y, m.Row1.Y, m.Row2.Y);
            Vector3 col2 = new Vector3(m.Row0.Z, m.Row1.Z, m.Row2.Z);

            scale = new Vector3(col0.Length, col1.Length, col2.Length);

            // Evitar división por cero
            float sx = scale.X > 1e-6f ? 1f / scale.X : 0f;
            float sy = scale.Y > 1e-6f ? 1f / scale.Y : 0f;
            float sz = scale.Z > 1e-6f ? 1f / scale.Z : 0f;

            // Matriz de rotación pura (normalizada)
            Matrix3 rotMat = new Matrix3(
                m.Row0.X * sx, m.Row0.Y * sy, m.Row0.Z * sz,
                m.Row1.X * sx, m.Row1.Y * sy, m.Row1.Z * sz,
                m.Row2.X * sx, m.Row2.Y * sy, m.Row2.Z * sz
            );

            rotation = OpenTK.Mathematics.Quaternion.FromMatrix(rotMat);
            rotation.Normalize();
        }

        public override void OnDestroy()
        {
            if (_boneUBO != -1)
            {
                GL.DeleteBuffer(_boneUBO);
                _boneUBO = -1;
            }
            _animatedModel = null;
            _currentClip = null;
            _blendFromClip = null;
        }
    }
}