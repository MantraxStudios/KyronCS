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

        // ── Array de animaciones cargadas ───────────────────────────────
        private readonly List<AnimationClip> _clips = new();
        private int _currentIndex = -1;

        /// <summary>
        /// Array de rutas (GUIDs) a archivos FBX que contienen animaciones.
        /// Cada FBX puede tener una o varias animaciones; todas se agregan
        /// al array interno en orden.
        /// Asignar desde el inspector o desde código antes de Awake/Start.
        /// </summary>
        [ToStorage]
        public string[] AnimationPaths { get; set; } = new string[0];

        [NoSerializeToInspector]
        public Matrix4[] FinalBoneMatrices => _finalBoneMatrices;

        [NoSerializeToInspector]
        public bool IsPlaying => _isPlaying;

        [NoSerializeToInspector]
        public float CurrentTime => _currentTime;

        [NoSerializeToInspector]
        public AnimationClip CurrentClip => _currentClip;

        [NoSerializeToInspector]
        public int CurrentIndex => _currentIndex;
        private int _pendingIndex = 0;

        [NoSerializeToInspector]
        public int ClipCount => _clips.Count;

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

        [ToStorage]
        public int CurrentPlaying
        {
            get => _currentIndex >= 0 ? _currentIndex : _pendingIndex;
            set
            {
                _pendingIndex = value;
                if (value >= 0 && value < _clips.Count)
                {
                    PlayCrossFade(value, 0.3f);
                }
            }
        }

        public Animator()
        {
            _finalBoneMatrices = new Matrix4[MAX_BONES];
            for (int i = 0; i < MAX_BONES; i++)
                _finalBoneMatrices[i] = Matrix4.Identity;
        }

        public override void Awake() { }
        public override void Start() { }

        // ════════════════════════════════════════════════════════════════
        //  MODELO BASE (esqueleto + bind pose)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Asigna el modelo base (esqueleto, huesos, bind pose).
        /// Las animaciones del propio modelo se agregan al array interno.
        /// Luego se cargan las animaciones de AnimationPaths.
        /// </summary>
        public void SetModel(AnimatedModel model)
        {
            _animatedModel = model;
            _clips.Clear();
            _currentIndex = -1;

            // 1) Agregar las animaciones que trae el propio modelo
            if (model != null)
            {
                foreach (var clip in model.Animations)
                {
                    clip.RootNode = model.RootNode;
                    _clips.Add(clip);
                }
            }

            // 2) Cargar animaciones externas desde AnimationPaths
            LoadAnimationPaths();

            // 3) Reproducir la animación pendiente o la primera
            if (_clips.Count > 0)
            {
                int startIndex = (_pendingIndex >= 0 && _pendingIndex < _clips.Count)
                    ? _pendingIndex
                    : 0;
                SetAnimation(startIndex);  // la primera vez sí va directo (no hay "desde" qué blendear)
                Play();
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  CARGA DE ANIMACIONES EXTERNAS
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Carga los FBX referenciados en AnimationPaths y extrae sus clips.
        /// Se llama automáticamente desde SetModel(), pero puedes llamarlo
        /// de nuevo si cambias AnimationPaths en runtime.
        /// </summary>
        public void LoadAnimationPaths()
        {
            if (AnimationPaths == null || AnimationPaths.Length == 0)
                return;

            for (int i = 0; i < AnimationPaths.Length; i++)
            {
                string path = AnimationPaths[i];
                if (string.IsNullOrEmpty(path)) continue;

                try
                {
                    var assetInfo = AssetManager.GetBytes(Guid.Parse(path));
                    var animModel = AnimatedModel.LoadFromBytes(assetInfo, "fbx");

                    if (animModel == null || animModel.Animations.Count == 0)
                    {
                        Console.WriteLine($"[Animator] AnimationPaths[{i}]: sin animaciones");
                        continue;
                    }

                    foreach (var clip in animModel.Animations)
                    {
                        // Vincular al esqueleto del modelo base
                        if (_animatedModel != null)
                            clip.RootNode = _animatedModel.RootNode;

                        _clips.Add(clip);
                        Console.WriteLine($"[Animator] Clip cargado [{_clips.Count - 1}]: \"{clip.Name}\" " +
                            $"({clip.Duration / Math.Max(clip.TicksPerSecond, 1f):F2}s)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Animator] Error cargando AnimationPaths[{i}]: {ex.Message}");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  AGREGAR / QUITAR CLIPS MANUALMENTE
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Agrega un clip al final del array. Devuelve su índice.
        /// </summary>
        public int AddClip(AnimationClip clip)
        {
            if (clip == null) return -1;

            if (_animatedModel != null)
                clip.RootNode = _animatedModel.RootNode;

            _clips.Add(clip);
            return _clips.Count - 1;
        }

        /// <summary>
        /// Agrega todos los clips de un AnimatedModel externo.
        /// Devuelve el índice del primero agregado, o -1 si no había clips.
        /// </summary>
        public int AddClipsFromModel(AnimatedModel externalModel)
        {
            if (externalModel == null || externalModel.Animations.Count == 0)
                return -1;

            int firstIndex = _clips.Count;

            foreach (var clip in externalModel.Animations)
            {
                if (_animatedModel != null)
                    clip.RootNode = _animatedModel.RootNode;

                _clips.Add(clip);
            }

            return firstIndex;
        }

        /// <summary>
        /// Elimina el clip en el índice dado.
        /// </summary>
        public void RemoveClip(int index)
        {
            if (index < 0 || index >= _clips.Count) return;

            _clips.RemoveAt(index);

            // Ajustar índice actual
            if (_currentIndex == index)
            {
                _currentClip = null;
                _currentIndex = -1;
                _isPlaying = false;
            }
            else if (_currentIndex > index)
            {
                _currentIndex--;
            }
        }

        /// <summary>
        /// Elimina todos los clips.
        /// </summary>
        public void ClearClips()
        {
            _clips.Clear();
            _currentClip = null;
            _currentIndex = -1;
            _isPlaying = false;
        }

        // ════════════════════════════════════════════════════════════════
        //  SELECCIÓN DE ANIMACIÓN
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Cambia a la animación en el índice dado. Reinicia el tiempo a 0.
        /// </summary>
        public void SetAnimation(int index)
        {
            if (index < 0 || index >= _clips.Count)
            {
                Console.WriteLine($"[Animator] Índice fuera de rango: {index} (total: {_clips.Count})");
                return;
            }

            _currentClip = _clips[index];
            _currentIndex = index;
            _currentTime = 0f;
            _isBlending = false;

            if (_animatedModel != null)
                _currentClip.RootNode = _animatedModel.RootNode;
        }

        /// <summary>
        /// Busca un clip por nombre y cambia a él.
        /// </summary>
        public void SetAnimation(string name)
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                if (_clips[i].Name == name)
                {
                    SetAnimation(i);
                    return;
                }
            }

            Console.WriteLine($"[Animator] Animación no encontrada: \"{name}\"");
        }

        // ════════════════════════════════════════════════════════════════
        //  CROSSFADE / BLEND
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Transición suave desde la animación actual hacia la del índice dado.
        /// </summary>
        public void CrossFade(int index, float blendDuration = 0.3f)
        {
            if (index < 0 || index >= _clips.Count) return;
            if (_clips[index] == _currentClip) return;

            _blendFromClip = _currentClip;
            _blendFromTime = _currentTime;
            _currentClip = _clips[index];
            _currentIndex = index;

            if (_animatedModel != null)
                _currentClip.RootNode = _animatedModel.RootNode;

            _currentTime = 0f;
            _blendDuration = blendDuration;
            _blendElapsed = 0f;
            _isBlending = true;
        }

        /// <summary>
        /// Transición suave buscando por nombre.
        /// </summary>
        public void CrossFade(string animationName, float blendDuration = 0.3f)
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                if (_clips[i].Name == animationName)
                {
                    CrossFade(i, blendDuration);
                    return;
                }
            }

            Console.WriteLine($"[Animator] CrossFade: no encontrada: \"{animationName}\"");
        }

        // ════════════════════════════════════════════════════════════════
        //  PLAY / PAUSE / STOP
        // ════════════════════════════════════════════════════════════════

        public void Play() => _isPlaying = true;
        public void Pause() => _isPlaying = false;

        public void Stop()
        {
            _isPlaying = false;
            _currentTime = 0f;
        }

        /// <summary>
        /// Cambia al índice indicado y reproduce.
        /// </summary>
        public void Play(int index)
        {
            SetAnimation(index);
            Play();
        }

        /// <summary>
        /// Cambia al nombre indicado y reproduce.
        /// </summary>
        public void Play(string name)
        {
            SetAnimation(name);
            Play();
        }

        /// <summary>
        /// Cambia con crossfade al índice y reproduce.
        /// Si no hay nada reproduciéndose, va directo sin blend.
        /// </summary>
        public void PlayCrossFade(int index, float blendDuration = 0.3f)
        {
            if (!_isPlaying || _currentClip == null)
            {
                SetAnimation(index);
                Play();
            }
            else
            {
                CrossFade(index, blendDuration);
            }
        }

        public void PlayCrossFade(string name, float blendDuration = 0.3f)
        {
            if (!_isPlaying || _currentClip == null)
            {
                SetAnimation(name);
                Play();
            }
            else
            {
                CrossFade(name, blendDuration);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  UPDATE
        // ════════════════════════════════════════════════════════════════

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

        // ════════════════════════════════════════════════════════════════
        //  CÁLCULO DE TRANSFORMS (sin cambios respecto al original)
        // ════════════════════════════════════════════════════════════════

        private void CalculateBoneTransforms()
        {
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
                    outMatrices[boneId] = boneInfo.OffsetMatrix * globalTransform * globalInverse;
                }
            }

            foreach (var child in node.Children)
                CalculateNodeTransform(clip, time, child, globalTransform, outMatrices, globalInverse);
        }

        // ════════════════════════════════════════════════════════════════
        //  UBO PARA MATRICES DE HUESOS (sin cambios)
        // ════════════════════════════════════════════════════════════════

        private int _boneUBO = -1;
        private const int BONE_UBO_BINDING = 1;

        private void EnsureBoneUBO()
        {
            if (_boneUBO != -1) return;
            _boneUBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, _boneUBO);
            GL.BufferData(BufferTarget.UniformBuffer, MAX_BONES * 64, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        public void UploadBoneMatrices(int shaderProgram)
        {
            int instLoc = GL.GetUniformLocation(shaderProgram, "u_UseInstancing");
            if (instLoc != -1)
                GL.Uniform1(instLoc, 0);

            int useAnimLoc = GL.GetUniformLocation(shaderProgram, "u_UseAnimation");
            if (useAnimLoc != -1)
                GL.Uniform1(useAnimLoc, 1);

            EnsureBoneUBO();

            var matrices = new Matrix4[MAX_BONES];
            int boneCount = Math.Min(_animatedModel?.BoneCount ?? 0, MAX_BONES);
            for (int i = 0; i < boneCount; i++)
                matrices[i] = _finalBoneMatrices[i];
            for (int i = boneCount; i < MAX_BONES; i++)
                matrices[i] = Matrix4.Identity;

            GL.BindBuffer(BufferTarget.UniformBuffer, _boneUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, MAX_BONES * 64, matrices);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

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

        // ════════════════════════════════════════════════════════════════
        //  CONSULTAS
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Devuelve los nombres de todas las animaciones en el array.
        /// </summary>
        public string[] GetAnimationNames()
        {
            var names = new string[_clips.Count];
            for (int i = 0; i < _clips.Count; i++)
                names[i] = _clips[i].Name;
            return names;
        }

        /// <summary>
        /// Devuelve la cantidad de animaciones en el array.
        /// </summary>
        public int GetAnimationCount() => _clips.Count;

        /// <summary>
        /// Devuelve el clip en el índice dado, o null.
        /// </summary>
        public AnimationClip GetClip(int index)
        {
            if (index < 0 || index >= _clips.Count) return null;
            return _clips[index];
        }

        /// <summary>
        /// Busca el índice de un clip por nombre. Devuelve -1 si no existe.
        /// </summary>
        public int FindClipIndex(string name)
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                if (_clips[i].Name == name)
                    return i;
            }
            return -1;
        }

        // ════════════════════════════════════════════════════════════════
        //  UTILIDADES (sin cambios)
        // ════════════════════════════════════════════════════════════════

        private static Matrix4 LerpMatrix(Matrix4 a, Matrix4 b, float t)
        {
            return new Matrix4(
                Vector4.Lerp(a.Row0, b.Row0, t),
                Vector4.Lerp(a.Row1, b.Row1, t),
                Vector4.Lerp(a.Row2, b.Row2, t),
                Vector4.Lerp(a.Row3, b.Row3, t)
            );
        }

        private static void DecomposeMatrix(Matrix4 m,
            out Vector3 translation, out OpenTK.Mathematics.Quaternion rotation, out Vector3 scale)
        {
            translation = new Vector3(m.Row3.X, m.Row3.Y, m.Row3.Z);

            Vector3 col0 = new Vector3(m.Row0.X, m.Row1.X, m.Row2.X);
            Vector3 col1 = new Vector3(m.Row0.Y, m.Row1.Y, m.Row2.Y);
            Vector3 col2 = new Vector3(m.Row0.Z, m.Row1.Z, m.Row2.Z);

            scale = new Vector3(col0.Length, col1.Length, col2.Length);

            float sx = scale.X > 1e-6f ? 1f / scale.X : 0f;
            float sy = scale.Y > 1e-6f ? 1f / scale.Y : 0f;
            float sz = scale.Z > 1e-6f ? 1f / scale.Z : 0f;

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
            _clips.Clear();
        }
    }
}