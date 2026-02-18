using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace KrayonCore.Animation
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Runtime state for a single state inside any layer
    // ─────────────────────────────────────────────────────────────────────────
    internal class AnimatorState
    {
        public string Name;
        public AnimationClip Clip;
        public bool Loop;
        public float Speed;
        public List<StateTransitionData> Transitions;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Runtime parameter
    // ─────────────────────────────────────────────────────────────────────────
    internal class AnimatorParameter
    {
        public string Name;
        public ParameterType Type;
        public float FloatValue;
        public int IntValue;
        public bool BoolValue;
        public bool TriggerValue;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Runtime state for a whole layer
    // ─────────────────────────────────────────────────────────────────────────
    internal class AnimatorLayerRuntime
    {
        // ── data from asset ──────────────────────────────────────────────────
        public string Name;
        public float Weight = 1f;
        public LayerBlendingMode BlendingMode = LayerBlendingMode.Override;

        /// <summary>
        /// Bone names that this layer is allowed to affect.
        /// null = no mask (affects all bones).
        /// </summary>
        public HashSet<string> MaskBones; // null → no mask

        // ── state machine ────────────────────────────────────────────────────
        public Dictionary<string, AnimatorState> States = new();
        public AnimatorState CurrentState;

        // ── playback ─────────────────────────────────────────────────────────
        public AnimationClip CurrentClip;
        public float CurrentTime;
        public bool IsPlaying;
        public bool Loop;
        public float PlaybackSpeed = 1f;

        // ── crossfade ────────────────────────────────────────────────────────
        public AnimationClip BlendFromClip;
        public float BlendFromTime;
        public float BlendFactor;
        public float BlendDuration;
        public float BlendElapsed;
        public bool IsBlending;

        // ── per-layer final matrices (filled by CalculateBoneTransforms) ──────
        public Matrix4[] Matrices = new Matrix4[Animator.MAX_BONES];

        public AnimatorLayerRuntime()
        {
            for (int i = 0; i < Animator.MAX_BONES; i++)
                Matrices[i] = Matrix4.Identity;
        }
    }

    // =========================================================================
    //  Animator  —  multi-layer skeletal animation controller
    // =========================================================================
    public class Animator : Component
    {
        public const int MAX_BONES = 256;

        // ── bone matrices uploaded to GPU ─────────────────────────────────────
        private Matrix4[] _finalBoneMatrices;

        // ── model ─────────────────────────────────────────────────────────────
        private AnimatedModel _animatedModel;

        // ── layers ────────────────────────────────────────────────────────────
        private readonly List<AnimatorLayerRuntime> _layers = new();

        // ── parameters (shared across all layers) ─────────────────────────────
        private readonly Dictionary<string, AnimatorParameter> _parameters = new();

        // ── convenience shortcuts to layer 0 ─────────────────────────────────
        private AnimatorLayerRuntime BaseLayer =>
            _layers.Count > 0 ? _layers[0] : null;

        // ── GPU buffer ────────────────────────────────────────────────────────
        private int _boneUBO = -1;
        private const int BONE_UBO_BINDING = 1;

        // ─────────────────────────────────────────────────────────────────────
        //  Serialised properties
        // ─────────────────────────────────────────────────────────────────────
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

        [ToStorage]
        public float PlaybackSpeed
        {
            get => BaseLayer?.PlaybackSpeed ?? 1f;
            set { if (BaseLayer != null) BaseLayer.PlaybackSpeed = value; }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Read-only public info (base layer)
        // ─────────────────────────────────────────────────────────────────────
        [NoSerializeToInspector] public Matrix4[] FinalBoneMatrices => _finalBoneMatrices;
        [NoSerializeToInspector] public bool IsPlaying => BaseLayer?.IsPlaying ?? false;
        [NoSerializeToInspector] public float CurrentTime => BaseLayer?.CurrentTime ?? 0f;
        [NoSerializeToInspector] public AnimationClip CurrentClip => BaseLayer?.CurrentClip;
        [NoSerializeToInspector] public string CurrentStateName => BaseLayer?.CurrentState?.Name ?? "";

        // ─────────────────────────────────────────────────────────────────────
        //  Construction
        // ─────────────────────────────────────────────────────────────────────
        public Animator()
        {
            _finalBoneMatrices = new Matrix4[MAX_BONES];
            for (int i = 0; i < MAX_BONES; i++)
                _finalBoneMatrices[i] = Matrix4.Identity;
        }

        public override void Awake() { }
        public override void Start() { }

        // ─────────────────────────────────────────────────────────────────────
        //  Model binding
        // ─────────────────────────────────────────────────────────────────────
        public void SetModel(AnimatedModel model)
        {
            _animatedModel = model;
            _layers.Clear();
            _parameters.Clear();

            if (!string.IsNullOrEmpty(ControllerGuid))
                LoadController(ControllerGuid);
            else
                Console.WriteLine("[Animator] ControllerGuid vacío — asigna un .animcontroller.");
        }

        [CallEvent("Reload Controller")]
        public void ReloadController()
        {
            if (_animatedModel != null && !string.IsNullOrEmpty(_controllerGuid))
                LoadController(_controllerGuid);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Controller loading
        // ─────────────────────────────────────────────────────────────────────
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

                // Migrate legacy single-layer assets into Layers[0]
                data.EnsureBaseLayer();

                _layers.Clear();
                _parameters.Clear();

                BuildParameters(data);
                BuildLayers(data);

                int totalStates = 0;
                foreach (var l in _layers) totalStates += l.States.Count;
                Console.WriteLine($"[Animator] Controller \"{data.Name}\" cargado: " +
                                  $"{_layers.Count} capa(s), {totalStates} estados, {_parameters.Count} parámetros.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Animator] Error cargando controller ({guid}): {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Build parameters
        // ─────────────────────────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────────────────────────
        //  Build layers + state machines
        // ─────────────────────────────────────────────────────────────────────
        private void BuildLayers(AnimatorControllerData data)
        {
            for (int li = 0; li < data.Layers.Count; li++)
            {
                var layerData = data.Layers[li];
                var runtime = new AnimatorLayerRuntime
                {
                    Name = layerData.Name,
                    Weight = li == 0 ? 1f : layerData.Weight,  // base layer always weight 1
                    BlendingMode = layerData.BlendingMode,
                    MaskBones = BuildMaskBones(layerData.AvatarMask)
                };

                foreach (var sd in layerData.States)
                {
                    var state = new AnimatorState
                    {
                        Name = sd.Name,
                        Loop = sd.Loop,
                        Speed = sd.Speed,
                        Transitions = sd.Transitions,
                        Clip = ResolveClip(sd.ClipGuid, sd.ClipName)
                    };
                    runtime.States[sd.Name] = state;

                    Console.WriteLine(state.Clip != null
                        ? $"[Animator] [{runtime.Name}] Estado \"{state.Name}\" → \"{state.Clip.Name}\""
                        : $"[Animator] [{runtime.Name}] Estado \"{state.Name}\" → clip NO RESUELTO ({sd.ClipGuid})");
                }

                // Enter default state for this layer
                string target = layerData.DefaultState;
                if (string.IsNullOrEmpty(target))
                    foreach (var k in runtime.States.Keys) { target = k; break; }

                if (!string.IsNullOrEmpty(target) && runtime.States.TryGetValue(target, out var defState))
                    EnterLayerState(runtime, defState);
                else if (li == 0 && !string.IsNullOrEmpty(target))
                    Console.WriteLine($"[Animator] ERROR: Estado default \"{target}\" no encontrado en capa \"{runtime.Name}\".");

                _layers.Add(runtime);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  AvatarMask → HashSet<string> of bone names
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Loads an AvatarMask asset and returns the set of bone names it includes.
        /// Returns null if maskGuid is empty (= no mask = affects all bones).
        /// </summary>
        private static HashSet<string> BuildMaskBones(string maskGuid)
        {
            if (string.IsNullOrWhiteSpace(maskGuid))
                return null;

            try
            {
                byte[] raw = AssetManager.GetBytes(Guid.Parse(maskGuid));
                string json = System.Text.Encoding.UTF8.GetString(raw);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list != null ? new HashSet<string>(list, StringComparer.Ordinal) : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Animator] BuildMaskBones: no se pudo cargar máscara ({maskGuid}): {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Resolve animation clip from FBX asset
        // ─────────────────────────────────────────────────────────────────────
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
                        Console.WriteLine($"[Animator] Clip \"{clipName}\" no encontrado en FBX, usando el primero.");
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

        // ─────────────────────────────────────────────────────────────────────
        //  State machine — enter state  (STATIC — no instance access needed)
        // ─────────────────────────────────────────────────────────────────────
        private static void EnterLayerState(AnimatorLayerRuntime layer,
                                            AnimatorState state,
                                            float blendDuration = 0f)
        {
            if (state == null || state.Clip == null) return;

            if (blendDuration > 0f && layer.CurrentClip != null)
            {
                layer.BlendFromClip = layer.CurrentClip;
                layer.BlendFromTime = layer.CurrentTime;
                layer.BlendDuration = blendDuration;
                layer.BlendElapsed = 0f;
                layer.BlendFactor = 0f;
                layer.IsBlending = true;
            }
            else
            {
                layer.IsBlending = false;
            }

            layer.CurrentState = state;
            layer.CurrentClip = state.Clip;
            layer.Loop = state.Loop;
            layer.PlaybackSpeed = state.Speed;
            layer.CurrentTime = 0f;
            layer.IsPlaying = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public playback API  (operates on base layer 0 for compat)
        // ─────────────────────────────────────────────────────────────────────
        public void Play() { if (BaseLayer != null) BaseLayer.IsPlaying = true; }
        public void Pause() { if (BaseLayer != null) BaseLayer.IsPlaying = false; }
        public void Stop() { if (BaseLayer != null) { BaseLayer.IsPlaying = false; BaseLayer.CurrentTime = 0f; } }

        public void Play(string stateName)
            => PlayOnLayer(0, stateName, 0f);

        public void CrossFade(string stateName, float blendDuration = 0.2f)
            => PlayOnLayer(0, stateName, blendDuration);

        /// <summary>Plays a state on a specific layer index.</summary>
        public void PlayOnLayer(int layerIndex, string stateName, float blendDuration = 0f)
        {
            if (layerIndex < 0 || layerIndex >= _layers.Count)
            {
                Console.WriteLine($"[Animator] PlayOnLayer: capa {layerIndex} no existe.");
                return;
            }
            var layer = _layers[layerIndex];
            if (!layer.States.TryGetValue(stateName, out var state))
            {
                Console.WriteLine($"[Animator] PlayOnLayer: estado \"{stateName}\" no existe en capa \"{layer.Name}\".");
                return;
            }
            if (state == layer.CurrentState && blendDuration <= 0f) return;
            EnterLayerState(layer, state, blendDuration);
        }

        /// <summary>Sets the weight of an upper layer (index >= 1) at runtime.</summary>
        public void SetLayerWeight(int layerIndex, float weight)
        {
            if (layerIndex <= 0 || layerIndex >= _layers.Count) return;
            _layers[layerIndex].Weight = Math.Clamp(weight, 0f, 1f);
        }

        public float GetLayerWeight(int layerIndex)
            => (layerIndex >= 0 && layerIndex < _layers.Count) ? _layers[layerIndex].Weight : 0f;

        public bool IsInState(string stateName) => BaseLayer?.CurrentState?.Name == stateName;

        public string[] GetStateNames()
        {
            if (BaseLayer == null) return Array.Empty<string>();
            var names = new string[BaseLayer.States.Count];
            int i = 0;
            foreach (var k in BaseLayer.States.Keys) names[i++] = k;
            return names;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Parameter API
        // ─────────────────────────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────────────────────────
        //  Per-frame update
        // ─────────────────────────────────────────────────────────────────────
        public override void OnWillRenderObject()
        {
            if (_animatedModel == null || _layers.Count == 0) return;

            float dt = TimerData.DeltaTime;

            foreach (var layer in _layers)
                TickLayer(layer, dt);

            CompositeLayers();
        }

        private void TickLayer(AnimatorLayerRuntime layer, float dt)
        {
            if (!layer.IsPlaying || layer.CurrentClip == null) return;

            // Advance clip time
            layer.CurrentTime += dt * layer.CurrentClip.TicksPerSecond * layer.PlaybackSpeed;
            if (layer.CurrentTime >= layer.CurrentClip.Duration)
            {
                if (layer.Loop)
                    layer.CurrentTime %= layer.CurrentClip.Duration;
                else
                {
                    layer.CurrentTime = layer.CurrentClip.Duration;
                    layer.IsPlaying = false;
                }
            }

            // Advance crossfade blend
            if (layer.IsBlending)
            {
                layer.BlendElapsed += dt;
                layer.BlendFactor = Math.Clamp(layer.BlendElapsed / layer.BlendDuration, 0f, 1f);

                if (layer.BlendFromClip != null)
                {
                    layer.BlendFromTime += dt * layer.BlendFromClip.TicksPerSecond * layer.PlaybackSpeed;
                    if (layer.BlendFromTime >= layer.BlendFromClip.Duration)
                        layer.BlendFromTime %= layer.BlendFromClip.Duration;
                }

                if (layer.BlendFactor >= 1f)
                    layer.IsBlending = false;
            }

            EvaluateTransitions(layer);
            CalculateBoneTransformsForLayer(layer);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Transition evaluation  (per layer)
        // ─────────────────────────────────────────────────────────────────────
        private void EvaluateTransitions(AnimatorLayerRuntime layer)
        {
            if (layer.CurrentState == null) return;

            float normalizedTime = (layer.CurrentClip != null && layer.CurrentClip.Duration > 0f)
                ? layer.CurrentTime / layer.CurrentClip.Duration : 0f;

            int layerIndex = _layers.IndexOf(layer);

            foreach (var t in layer.CurrentState.Transitions)
            {
                if (t.HasExitTime && normalizedTime < t.ExitTime) continue;
                if (!t.CanInterrupt && normalizedTime < 1f) continue;
                if (!layer.States.ContainsKey(t.ToState)) continue;

                bool allMet = true;
                List<AnimatorParameter> triggersToConsume = null;

                foreach (var cond in t.Conditions)
                {
                    if (!_parameters.TryGetValue(cond.Parameter, out var param))
                    { allMet = false; break; }

                    if (!EvaluateCondition(param, cond))
                    { allMet = false; break; }

                    if (param.Type == ParameterType.Trigger)
                    {
                        triggersToConsume ??= new List<AnimatorParameter>();
                        triggersToConsume.Add(param);
                    }
                }

                if (!allMet) continue;

                triggersToConsume?.ForEach(tr => tr.TriggerValue = false);
                PlayOnLayer(layerIndex, t.ToState, t.Duration);
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

        // ─────────────────────────────────────────────────────────────────────
        //  Bone transform calculation  (per layer)
        // ─────────────────────────────────────────────────────────────────────
        private void CalculateBoneTransformsForLayer(AnimatorLayerRuntime layer)
        {
            Matrix4 globalInverse = _animatedModel.GlobalInverseTransform;

            for (int i = 0; i < MAX_BONES; i++)
                layer.Matrices[i] = Matrix4.Identity;

            if (layer.IsBlending && layer.BlendFromClip != null)
            {
                var matA = new Matrix4[MAX_BONES];
                var matB = new Matrix4[MAX_BONES];
                for (int i = 0; i < MAX_BONES; i++) matA[i] = matB[i] = Matrix4.Identity;

                CalculateNodeTransform(layer.BlendFromClip, layer.BlendFromTime,
                    _animatedModel.RootNode, Matrix4.Identity, matA,
                    globalInverse, _animatedModel.BoneInfoMap, layer.MaskBones);

                CalculateNodeTransform(layer.CurrentClip, layer.CurrentTime,
                    _animatedModel.RootNode, Matrix4.Identity, matB,
                    globalInverse, _animatedModel.BoneInfoMap, layer.MaskBones);

                for (int i = 0; i < MAX_BONES; i++)
                    layer.Matrices[i] = LerpMatrix(matA[i], matB[i], layer.BlendFactor);
            }
            else if (layer.CurrentClip != null)
            {
                CalculateNodeTransform(layer.CurrentClip, layer.CurrentTime,
                    _animatedModel.RootNode, Matrix4.Identity, layer.Matrices,
                    globalInverse, _animatedModel.BoneInfoMap, layer.MaskBones);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Composite all layers into _finalBoneMatrices
        // ─────────────────────────────────────────────────────────────────────
        private void CompositeLayers()
        {
            if (_layers.Count == 0) return;

            // Base layer (index 0) is always full weight
            Array.Copy(_layers[0].Matrices, _finalBoneMatrices, MAX_BONES);

            // Blend upper layers on top
            for (int li = 1; li < _layers.Count; li++)
            {
                var layer = _layers[li];
                if (layer.Weight <= 0f) continue;

                for (int i = 0; i < MAX_BONES; i++)
                {
                    if (!IsBoneInMaskByIndex(i, layer.MaskBones, _animatedModel.BoneInfoMap))
                        continue;

                    _finalBoneMatrices[i] = layer.BlendingMode == LayerBlendingMode.Override
                        ? LerpMatrix(_finalBoneMatrices[i], layer.Matrices[i], layer.Weight)
                        : AddMatrices(_finalBoneMatrices[i], layer.Matrices[i], layer.Weight);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CalculateNodeTransform  — STATIC
        // ─────────────────────────────────────────────────────────────────────
        private static void CalculateNodeTransform(
            AnimationClip clip,
            float time,
            NodeData node,
            Matrix4 parentTransform,
            Matrix4[] outMatrices,
            Matrix4 globalInverse,
            Dictionary<string, BoneInfo> boneInfoMap,
            HashSet<string> maskBones)
        {
            if (node == null) return;

            Matrix4 nodeTransform = node.Transform;
            bool inMask = IsBoneInMask(node.Name, maskBones);

            if (inMask)
            {
                var boneAnim = clip.FindBoneAnimation(node.Name);
                if (boneAnim != null)
                {
                    DecomposeMatrix(node.Transform,
                        out Vector3 bindTrans,
                        out Quaternion bindRot,
                        out Vector3 bindScale);

                    Vector3 t = boneAnim.Positions.Count > 0 ? boneAnim.InterpolatePosition(time) : bindTrans;
                    Quaternion r = boneAnim.Rotations.Count > 0 ? boneAnim.InterpolateRotation(time) : bindRot;
                    Vector3 s = boneAnim.Scales.Count > 0 ? boneAnim.InterpolateScale(time) : bindScale;

                    nodeTransform = Matrix4.CreateScale(s)
                                  * Matrix4.CreateFromQuaternion(r)
                                  * Matrix4.CreateTranslation(t);
                }
            }

            Matrix4 globalTransform = nodeTransform * parentTransform;

            if (inMask && boneInfoMap.TryGetValue(node.Name, out var boneInfo))
            {
                int boneId = boneInfo.Id;
                if (boneId >= 0 && boneId < MAX_BONES)
                    outMatrices[boneId] = boneInfo.OffsetMatrix * globalTransform * globalInverse;
            }

            if (node.Children == null) return;
            foreach (var child in node.Children)
                CalculateNodeTransform(clip, time, child, globalTransform,
                                       outMatrices, globalInverse, boneInfoMap, maskBones);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IsBoneInMask — STATIC
        //
        //  maskBones == null  →  no mask, all bones pass (returns true).
        //  maskBones != null  →  only bones whose name is in the set pass.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks by bone name (node name). Used inside CalculateNodeTransform.
        /// Static — does NOT require an instance reference.
        /// </summary>
        private static bool IsBoneInMask(string boneName, HashSet<string> maskBones)
            => maskBones == null || maskBones.Contains(boneName);

        /// <summary>
        /// Checks by bone ID via a reverse lookup through the boneInfoMap.
        /// Used in CompositeLayers where only the matrix index is available.
        /// Static — does NOT require an instance reference.
        /// </summary>
        private static bool IsBoneInMaskByIndex(int boneId,
                                                HashSet<string> maskBones,
                                                Dictionary<string, BoneInfo> boneInfoMap)
        {
            if (maskBones == null) return true;

            foreach (var kv in boneInfoMap)
                if (kv.Value.Id == boneId) return maskBones.Contains(kv.Key);

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GPU upload
        // ─────────────────────────────────────────────────────────────────────
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
            for (int i = 0; i < boneCount; i++) matrices[i] = _finalBoneMatrices[i];
            for (int i = boneCount; i < MAX_BONES; i++) matrices[i] = Matrix4.Identity;

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

        // ─────────────────────────────────────────────────────────────────────
        //  Math helpers — all STATIC
        // ─────────────────────────────────────────────────────────────────────
        private static Matrix4 LerpMatrix(Matrix4 a, Matrix4 b, float t) =>
            new(Vector4.Lerp(a.Row0, b.Row0, t),
                Vector4.Lerp(a.Row1, b.Row1, t),
                Vector4.Lerp(a.Row2, b.Row2, t),
                Vector4.Lerp(a.Row3, b.Row3, t));

        private static Matrix4 AddMatrices(Matrix4 a, Matrix4 b, float weight) =>
            new(a.Row0 + b.Row0 * weight,
                a.Row1 + b.Row1 * weight,
                a.Row2 + b.Row2 * weight,
                a.Row3 + b.Row3 * weight);

        private static void DecomposeMatrix(Matrix4 m,
            out Vector3 translation, out Quaternion rotation, out Vector3 scale)
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

            rotation = Quaternion.FromMatrix(rotMat);
            rotation.Normalize();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Cleanup
        // ─────────────────────────────────────────────────────────────────────
        public override void OnDestroy()
        {
            if (_boneUBO != -1)
            {
                GL.DeleteBuffer(_boneUBO);
                _boneUBO = -1;
            }
            _animatedModel = null;
            _layers.Clear();
            _parameters.Clear();
        }
    }
}