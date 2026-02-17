using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace KrayonCore.Animation
{
    internal class AnimatorState
    {
        public string Name;
        public AnimationClip Clip;
        public bool Loop;
        public float Speed;
        public List<StateTransitionData> Transitions;
    }

    internal class AnimatorParameter
    {
        public string Name;
        public ParameterType Type;
        public float FloatValue;
        public int IntValue;
        public bool BoolValue;
        public bool TriggerValue;
    }

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

        private readonly Dictionary<string, AnimatorState> _states = new();
        private readonly Dictionary<string, AnimatorParameter> _parameters = new();
        private AnimatorState _currentState;

        private int _boneUBO = -1;
        private const int BONE_UBO_BINDING = 1;

        private string _controllerGuid = "";
        [ToStorage]
        public string ControllerGuid
        {
            get => _controllerGuid;
            set
            {
                if (_controllerGuid == value) return;
                _controllerGuid = value;
                if (_animatedModel != null && !string.IsNullOrEmpty(_controllerGuid))
                    LoadController(_controllerGuid);
            }
        }

        [NoSerializeToInspector] public Matrix4[] FinalBoneMatrices => _finalBoneMatrices;
        [NoSerializeToInspector] public bool IsPlaying => _isPlaying;
        [NoSerializeToInspector] public float CurrentTime => _currentTime;
        [NoSerializeToInspector] public AnimationClip CurrentClip => _currentClip;
        [NoSerializeToInspector] public string CurrentStateName => _currentState?.Name ?? "";

        [ToStorage]
        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = value;
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
            _states.Clear();
            _parameters.Clear();
            _currentState = null;
            _currentClip = null;
            _isPlaying = false;

            if (!string.IsNullOrEmpty(ControllerGuid))
                LoadController(ControllerGuid);
            else
                Console.WriteLine("[Animator] ControllerGuid vacío — asigna un .animcontroller en el inspector.");
        }

        [CallEvent("Reload Controller")]
        public void ReloadController()
        {
            if (_animatedModel != null && !string.IsNullOrEmpty(_controllerGuid))
                LoadController(_controllerGuid);
        }

        private void LoadController(string guid)
        {
            try
            {
                byte[] rawData = AssetManager.GetBytes(Guid.Parse(guid));
                string json = System.Text.Encoding.UTF8.GetString(rawData);
                var data = JsonSerializer.Deserialize<AnimatorControllerData>(json);

                if (data == null)
                {
                    Console.WriteLine("[Animator] Controller vacío o JSON inválido.");
                    return;
                }

                BuildParameters(data);
                BuildStates(data);
                EnterDefaultState(data);

                Console.WriteLine($"[Animator] Controller \"{data.Name}\" cargado: {_states.Count} estados, {_parameters.Count} parámetros.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Animator] Error cargando controller ({guid}): {ex.Message}");
            }
        }

        private void BuildParameters(AnimatorControllerData data)
        {
            foreach (var p in data.Parameters)
            {
                _parameters[p.Name] = new AnimatorParameter
                {
                    Name = p.Name,
                    Type = p.Type,
                    FloatValue = p.DefaultValue,
                    IntValue = (int)p.DefaultValue,
                    BoolValue = p.DefaultValue != 0f,
                    TriggerValue = false
                };
            }
        }

        private void BuildStates(AnimatorControllerData data)
        {
            foreach (var sd in data.States)
            {
                var state = new AnimatorState
                {
                    Name = sd.Name,
                    Loop = sd.Loop,
                    Speed = sd.Speed,
                    Transitions = sd.Transitions,
                    Clip = ResolveClip(sd.ClipGuid, sd.ClipName)
                };
                _states[sd.Name] = state;

                Console.WriteLine(state.Clip != null
                    ? $"[Animator] Estado \"{state.Name}\" → \"{state.Clip.Name}\""
                    : $"[Animator] Estado \"{state.Name}\" → clip NO RESUELTO ({sd.ClipGuid})");
            }
        }

        private AnimationClip ResolveClip(string clipGuid, string clipName)
        {
            if (string.IsNullOrEmpty(clipGuid)) return null;
            try
            {
                byte[] rawData = AssetManager.GetBytes(Guid.Parse(clipGuid));
                var animModel = AnimatedModel.LoadFromBytes(rawData, "fbx");

                if (animModel == null || animModel.Animations.Count == 0)
                {
                    Console.WriteLine($"[Animator] ResolveClip: FBX sin animaciones ({clipGuid})");
                    return null;
                }

                AnimationClip found = null;
                if (!string.IsNullOrEmpty(clipName))
                {
                    foreach (var c in animModel.Animations)
                        if (c.Name == clipName) { found = c; break; }

                    if (found == null)
                        Console.WriteLine($"[Animator] Clip \"{clipName}\" no encontrado, usando el primero.");
                }
                found ??= animModel.Animations[0];

                if (_animatedModel != null)
                    found.RootNode = _animatedModel.RootNode;

                return found;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Animator] ResolveClip error ({clipGuid}): {ex.Message}");
                return null;
            }
        }

        private void EnterDefaultState(AnimatorControllerData data)
        {
            string target = data.DefaultState;

            if (string.IsNullOrEmpty(target))
                foreach (var key in _states.Keys) { target = key; break; }

            if (!string.IsNullOrEmpty(target) && _states.TryGetValue(target, out var state))
            {
                EnterState(state);
                Console.WriteLine($"[Animator] Estado inicial: \"{state.Name}\" | Clip: {state.Clip?.Name ?? "NULL"}");
            }
            else
            {
                Console.WriteLine($"[Animator] ERROR: Estado default \"{target}\" no encontrado.");
                Console.WriteLine($"[Animator] Estados disponibles: {string.Join(", ", _states.Keys)}");
            }
        }

        private void EnterState(AnimatorState state, float blendDuration = 0f)
        {
            if (state == null || state.Clip == null) return;

            if (blendDuration > 0f && _currentClip != null)
            {
                _blendFromClip = _currentClip;
                _blendFromTime = _currentTime;
                _blendDuration = blendDuration;
                _blendElapsed = 0f;
                _blendFactor = 0f;
                _isBlending = true;
            }
            else
            {
                _isBlending = false;
            }

            _currentState = state;
            _currentClip = state.Clip;
            _loop = state.Loop;
            _playbackSpeed = state.Speed;
            _currentTime = 0f;
            _isPlaying = true;
        }

        public void SetFloat(string name, float value)
        {
            if (_parameters.TryGetValue(name, out var p)) p.FloatValue = value;
            else Console.WriteLine($"[Animator] SetFloat: \"{name}\" no existe.");
        }

        public void SetInt(string name, int value)
        {
            if (_parameters.TryGetValue(name, out var p)) p.IntValue = value;
            else Console.WriteLine($"[Animator] SetInt: \"{name}\" no existe.");
        }

        public void SetBool(string name, bool value)
        {
            if (_parameters.TryGetValue(name, out var p)) p.BoolValue = value;
            else Console.WriteLine($"[Animator] SetBool: \"{name}\" no existe.");
        }

        public void SetTrigger(string name)
        {
            if (_parameters.TryGetValue(name, out var p)) p.TriggerValue = true;
            else Console.WriteLine($"[Animator] SetTrigger: \"{name}\" no existe.");
        }

        public void ResetTrigger(string name)
        {
            if (_parameters.TryGetValue(name, out var p)) p.TriggerValue = false;
        }

        public float GetFloat(string name) =>
            _parameters.TryGetValue(name, out var p) ? p.FloatValue : 0f;

        public int GetInt(string name) =>
            _parameters.TryGetValue(name, out var p) ? p.IntValue : 0;

        public bool GetBool(string name) =>
            _parameters.TryGetValue(name, out var p) && p.BoolValue;

        public void Play() => _isPlaying = true;
        public void Pause() => _isPlaying = false;
        public void Stop() { _isPlaying = false; _currentTime = 0f; }

        public void Play(string stateName)
        {
            if (!_states.TryGetValue(stateName, out var state))
            {
                Console.WriteLine($"[Animator] Play: estado \"{stateName}\" no existe.");
                return;
            }
            EnterState(state);
        }

        public void CrossFade(string stateName, float blendDuration = 0.2f)
        {
            if (!_states.TryGetValue(stateName, out var state))
            {
                Console.WriteLine($"[Animator] CrossFade: estado \"{stateName}\" no existe.");
                return;
            }
            if (state == _currentState) return;
            EnterState(state, blendDuration);
        }

        public bool IsInState(string stateName) => _currentState?.Name == stateName;

        public string[] GetStateNames()
        {
            var names = new string[_states.Count];
            int i = 0;
            foreach (var key in _states.Keys) names[i++] = key;
            return names;
        }

        public override void OnWillRenderObject()
        {
            if (!_isPlaying || _currentClip == null || _animatedModel == null)
                return;

            float dt = TimerData.DeltaTime;

            _currentTime += dt * _currentClip.TicksPerSecond * _playbackSpeed;
            if (_currentTime >= _currentClip.Duration)
            {
                if (_loop)
                    _currentTime %= _currentClip.Duration;
                else
                {
                    _currentTime = _currentClip.Duration;
                    _isPlaying = false;
                }
            }

            if (_isBlending)
            {
                _blendElapsed += dt;
                _blendFactor = Math.Clamp(_blendElapsed / _blendDuration, 0f, 1f);

                if (_blendFromClip != null)
                {
                    _blendFromTime += dt * _blendFromClip.TicksPerSecond * _playbackSpeed;
                    if (_blendFromTime >= _blendFromClip.Duration)
                        _blendFromTime %= _blendFromClip.Duration;
                }

                if (_blendFactor >= 1f)
                    _isBlending = false;
            }

            EvaluateTransitions();
            CalculateBoneTransforms();
        }

        private void EvaluateTransitions()
        {
            if (_currentState == null) return;

            float normalizedTime = (_currentClip != null && _currentClip.Duration > 0f)
                ? _currentTime / _currentClip.Duration
                : 0f;

            foreach (var t in _currentState.Transitions)
            {
                if (t.HasExitTime && normalizedTime < t.ExitTime)
                    continue;

                if (!t.CanInterrupt && normalizedTime < 1f)
                    continue;

                if (!_states.TryGetValue(t.ToState, out _))
                    continue;

                bool allMet = true;
                List<AnimatorParameter> triggersToConsume = null;

                foreach (var cond in t.Conditions)
                {
                    if (!_parameters.TryGetValue(cond.Parameter, out var param))
                    {
                        allMet = false;
                        break;
                    }

                    if (!EvaluateCondition(param, cond))
                    {
                        allMet = false;
                        break;
                    }

                    if (param.Type == ParameterType.Trigger)
                    {
                        triggersToConsume ??= new List<AnimatorParameter>();
                        triggersToConsume.Add(param);
                    }
                }

                if (!allMet) continue;

                triggersToConsume?.ForEach(tr => tr.TriggerValue = false);

                if (t.Duration > 0f)
                    CrossFade(t.ToState, t.Duration);
                else
                    Play(t.ToState);

                break;
            }
        }

        private static bool EvaluateCondition(AnimatorParameter param, TransitionConditionData cond)
        {
            return cond.Mode switch
            {
                ConditionMode.True => param.Type == ParameterType.Trigger ? param.TriggerValue : param.BoolValue,
                ConditionMode.False => !param.BoolValue,
                ConditionMode.Greater => param.Type == ParameterType.Int
                                            ? param.IntValue > (int)cond.Threshold
                                            : param.FloatValue > cond.Threshold,
                ConditionMode.Less => param.Type == ParameterType.Int
                                            ? param.IntValue < (int)cond.Threshold
                                            : param.FloatValue < cond.Threshold,
                ConditionMode.Equals => param.IntValue == (int)cond.Threshold,
                ConditionMode.Trigger => param.TriggerValue,
                _ => false
            };
        }

        private void CalculateBoneTransforms()
        {
            Matrix4 globalInverse = _animatedModel.GlobalInverseTransform;

            if (_isBlending && _blendFromClip != null)
            {
                var matricesA = new Matrix4[MAX_BONES];
                var matricesB = new Matrix4[MAX_BONES];
                for (int i = 0; i < MAX_BONES; i++)
                    matricesA[i] = matricesB[i] = Matrix4.Identity;

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

        private void CalculateNodeTransform(AnimationClip clip, float time, NodeData node,
            Matrix4 parentTransform, Matrix4[] outMatrices, Matrix4 globalInverse)
        {
            if (node == null) return;

            Matrix4 nodeTransform = node.Transform;

            var boneAnim = clip.FindBoneAnimation(node.Name);
            if (boneAnim != null)
            {
                DecomposeMatrix(node.Transform,
                    out Vector3 bindTrans,
                    out OpenTK.Mathematics.Quaternion bindRot,
                    out Vector3 bindScale);

                Vector3 t = boneAnim.Positions.Count > 0 ? boneAnim.InterpolatePosition(time) : bindTrans;
                var r = boneAnim.Rotations.Count > 0 ? boneAnim.InterpolateRotation(time) : bindRot;
                Vector3 s = boneAnim.Scales.Count > 0 ? boneAnim.InterpolateScale(time) : bindScale;

                nodeTransform = Matrix4.CreateScale(s)
                              * Matrix4.CreateFromQuaternion(r)
                              * Matrix4.CreateTranslation(t);
            }

            Matrix4 globalTransform = nodeTransform * parentTransform;

            if (_animatedModel.BoneInfoMap.TryGetValue(node.Name, out var boneInfo))
            {
                int boneId = boneInfo.Id;
                if (boneId >= 0 && boneId < MAX_BONES)
                    outMatrices[boneId] = boneInfo.OffsetMatrix * globalTransform * globalInverse;
            }

            if (node.Children == null) return;
            foreach (var child in node.Children)
                CalculateNodeTransform(clip, time, child, globalTransform, outMatrices, globalInverse);
        }

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
            if (instLoc != -1) GL.Uniform1(instLoc, 0);

            int useAnimLoc = GL.GetUniformLocation(shaderProgram, "u_UseAnimation");
            if (useAnimLoc != -1) GL.Uniform1(useAnimLoc, 1);

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
            int loc = GL.GetUniformLocation(shaderProgram, "u_UseAnimation");
            if (loc != -1) GL.Uniform1(loc, 0);
        }

        private static Matrix4 LerpMatrix(Matrix4 a, Matrix4 b, float t) =>
            new(Vector4.Lerp(a.Row0, b.Row0, t),
                Vector4.Lerp(a.Row1, b.Row1, t),
                Vector4.Lerp(a.Row2, b.Row2, t),
                Vector4.Lerp(a.Row3, b.Row3, t));

        private static void DecomposeMatrix(Matrix4 m,
            out Vector3 translation, out OpenTK.Mathematics.Quaternion rotation, out Vector3 scale)
        {
            translation = new Vector3(m.Row3.X, m.Row3.Y, m.Row3.Z);

            Vector3 col0 = new(m.Row0.X, m.Row1.X, m.Row2.X);
            Vector3 col1 = new(m.Row0.Y, m.Row1.Y, m.Row2.Y);
            Vector3 col2 = new(m.Row0.Z, m.Row1.Z, m.Row2.Z);

            scale = new Vector3(col0.Length, col1.Length, col2.Length);

            float sx = scale.X > 1e-6f ? 1f / scale.X : 0f;
            float sy = scale.Y > 1e-6f ? 1f / scale.Y : 0f;
            float sz = scale.Z > 1e-6f ? 1f / scale.Z : 0f;

            Matrix3 rotMat = new(
                m.Row0.X * sx, m.Row0.Y * sy, m.Row0.Z * sz,
                m.Row1.X * sx, m.Row1.Y * sy, m.Row1.Z * sz,
                m.Row2.X * sx, m.Row2.Y * sy, m.Row2.Z * sz);

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
            _currentState = null;
            _states.Clear();
            _parameters.Clear();
        }
    }
}