using KrayonCore.Animation;
using System;

namespace KrayonCore.Components
{
    public class AnimationTest : Component
    {
        private Animator _animator;

        public override void Awake() { }

        public override void Start()
        {
            var animRenderer = GameObject.GetComponent<AnimatedMeshRenderer>();
            if (animRenderer == null)
            {
                Console.WriteLine("[AnimationTest] No se encontró AnimatedMeshRenderer en este objeto");
                return;
            }

            _animator = animRenderer.GetAnimator();
            if (_animator == null)
            {
                Console.WriteLine("[AnimationTest] No se encontró Animator");
                return;
            }

            // Listar todas las animaciones disponibles
            string[] names = _animator.GetAnimationNames();
            Console.WriteLine($"[AnimationTest] Animaciones encontradas: {names.Length}");
            for (int i = 0; i < names.Length; i++)
            {
                Console.WriteLine($"  [{i}] {names[i]}");
            }

            // Reproducir la primera que exista
            if (names.Length > 0)
            {
                _animator.SetAnimation(0);
                _animator.Loop = true;
                _animator.PlaybackSpeed = 1f;
                _animator.Play();
                Console.WriteLine($"[AnimationTest] Reproduciendo: {names[0]}");
            }
            else
            {
                Console.WriteLine("[AnimationTest] Este modelo no tiene animaciones");
            }
        }

        public override void Update(float deltaTime) { }
    }
}