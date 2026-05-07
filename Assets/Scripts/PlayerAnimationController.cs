using Fusion;
using UnityEngine;

public class PlayerAnimationController : NetworkBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkPlayerController movement;
    [SerializeField] private Health health;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IsAliveHash = Animator.StringToHash("IsAlive");
    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DieHash = Animator.StringToHash("Die");
    private static readonly int RespawnHash = Animator.StringToHash("Respawn");

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (movement == null)
            movement = GetComponent<NetworkPlayerController>();

        if (health == null)
            health = GetComponent<Health>();
    }

    public override void Render()
    {
        if (animator == null)
            return;

        float speed = movement != null ? movement.NetworkMoveSpeed : 0f;
        animator.SetFloat(SpeedHash, speed);
        animator.SetFloat(MoveXHash, movement != null ? movement.NetworkMoveX : 0f);
        animator.SetFloat(MoveYHash, movement != null ? movement.NetworkMoveY : 0f);

        if (health != null)
            animator.SetBool(IsAliveHash, health.IsAlive);
    }

    public void PlayFire()
    {
        if (animator != null)
            animator.SetTrigger(FireHash);
    }

    public void PlayHit()
    {
        if (animator != null)
            animator.SetTrigger(HitHash);
    }

    public void PlayDeath()
    {
        if (animator != null)
            animator.SetTrigger(DieHash);
    }

    public void PlayRespawn()
    {
        if (animator != null)
            animator.SetTrigger(RespawnHash);
    }
}
