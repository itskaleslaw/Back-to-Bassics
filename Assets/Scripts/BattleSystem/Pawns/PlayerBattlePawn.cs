using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Playable Battle Pawn
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(PlayerTraversalPawn))]
public class PlayerBattlePawn : BattlePawn, IAttackRequester, IAttackReceiver
{
    [Header("Player References")]
    [SerializeField] private PlayerWeaponData _weaponData;
    [SerializeField] private ParticleSystem _particleSystem;
    private PlayerTraversalPawn _traversalPawn;
    public PlayerWeaponData WeaponData => _weaponData;
    public bool blocking { get; private set; }
    public Vector2 SlashDirection { get; private set; }
    public Direction DodgeDirection { get; private set; }
    private Queue<IAttackRequester> _activeAttackRequesters;
    public EnemyBattlePawn CurrEnemyOpponent { get; private set; }
    public float AttackDamage { get => _weaponData.Dmg; }
    public float AttackLurch { get => _weaponData.Lrch; }
    public bool attacking { get; private set; }
    public bool deflectionWindow { get; private set; }
    public bool dodging { get; set; }
    private float battlePositionOffset = -1.8f;
    protected override void Awake()
    {
        base.Awake();
        _activeAttackRequesters = new Queue<IAttackRequester>();
        _traversalPawn = GetComponent<PlayerTraversalPawn>();
        SlashDirection = Vector2.zero;
    }
    // This will start a battle
    public void EngageEnemy(EnemyBattlePawn enemy)
    {
        StartCoroutine(EngageOpponent(enemy));
    }
    #region Player Actions
    /// <summary>
    /// Processes blocks to any active attack requests.
    /// </summary>
    public void Block()
    {
        if (IsStaggered || IsDead) return;
        AnimatorStateInfo animatorState = _spriteAnimator.GetCurrentAnimatorStateInfo(0);
        if (!animatorState.IsName("idle") || blocking) return;
        blocking = true;
        _spriteAnimator.Play("block");
        if (_activeAttackRequesters.Count > 0)
        {
            // (Suggestion) Maybe you should process all requests?
            // Note we are dequeing!
            _activeAttackRequesters.Peek().OnRequestBlock(this);
        }
    }
    /// <summary>
    /// Should Follow Blocking, the animation and the input.
    /// Might not need the !blocking check
    /// </summary>
    public void Unblock()
    {
        AnimatorStateInfo animatorState = _spriteAnimator.GetCurrentAnimatorStateInfo(0);
        if (!animatorState.IsName("block") || !blocking) return;
        blocking = false;
        _spriteAnimator.Play("unblock");
    }
    public void Dodge(Vector2 direction)
    {
        if (IsStaggered || IsDead) return;
        AnimatorStateInfo animatorState = _spriteAnimator.GetCurrentAnimatorStateInfo(0);
        if (!animatorState.IsName("idle")) return;
        // (Past Ryan 1) Figure out a way to make the dodging false later
        // (Past Ryan 2) I'm sorry future ryan, but I have figured it out through very scuffed means
        // Check a file called OnDodgeEnd.cs
        // (Ryan) This really sucky
        DodgeDirection = DirectionHelper.GetVectorDirection(direction);
        //dodging = true;
        _spriteAnimator.Play("dodge_" + DodgeDirection.ToString().ToLower());
    }
    /// <summary>
    /// Slash in a given direction. 
    /// If there are active attack requests, deflect them. 
    /// Otherwise request an attack to the enemy pawn.
    /// </summary>
    /// <param name="slashDirection"></param>
    public void Slash(Vector2 direction)
    {
        if (IsStaggered || IsDead || blocking || attacking) return;
        AnimatorStateInfo animatorState = _spriteAnimator.GetCurrentAnimatorStateInfo(0);
        if (!animatorState.IsName("idle")) return;
        // Set the Slash Direction
        SlashDirection = direction;
        SlashDirection.Normalize();
        //UIManager.Instance.PlayerSlash(SlashDirection);
        StartCoroutine(Attacking());
        //if (_activeAttackRequesters.Count > 0)
        //{
        //    // (Suggestion) Maybe you should process all requests?
        //    // Note we are dequeing!
        //    //_activeAttackRequesters.Peek().OnRequestDeflect(this);
        //}
        //else 
        if (_activeAttackRequesters.Count <= 0)
        {
            CurrEnemyOpponent.Damage(_weaponData.Dmg);
            //BattleManager.Instance.Enemy.Lurch(_weaponData.Lrch); -> Uncomment this if we should do this?
            // BattleManager.Instance.Enemy.ApplyStatusAilments(_weaponData.ailments); -> uncomment you have defined this

            // (Past Ryan) Whatever the fuck I call completing/processing an attack as opposed to "receving a request" bullshit
            // (Current Ryan) Oh there it is lmao
            CurrEnemyOpponent.ReceiveAttackRequest(this);
        }
    }
    #endregion
    /// <summary>
    /// Player cannot recover sp while blocking -> Could be brought further upward, in case we have items that use this method...
    /// </summary>
    /// <param name="amount"></param>
    public override void RecoverSP(float amount)
    {
        // Technically inefficent due to second method call, but good for readablity and modularity!
        if (!blocking && !attacking) base.RecoverSP(amount);
    }
    #region IAttackReceiver Methods
    public void ReceiveAttackRequest(IAttackRequester requester)
    {
        _activeAttackRequesters.Enqueue(requester);
        if (deflectionWindow)
        {
            requester.OnRequestDeflect(this);
        }
        else if (blocking)
        {
            requester.OnRequestBlock(this);
        }
        else if (dodging)
        {
            requester.OnRequestDodge(this);
        }
         
    }

    public void CompleteAttackRequest(IAttackRequester requester)
    {
        if (_activeAttackRequesters.Peek() != requester)
        {
            Debug.LogError("Attack Request and Completion missmatch, expected attack requester \"" + _activeAttackRequesters.Peek() + "\" instead got \"" + requester + ".\"");
            return;
        }
        _activeAttackRequesters.Dequeue();
    }
    #endregion

    public void OnRequestDeflect(IAttackReceiver receiver)
    {
        throw new System.NotImplementedException();
    }
    public void OnRequestBlock(IAttackReceiver receiver)
    {
        _spriteAnimator.Play("attack_blocked");
    }

    public void OnRequestDodge(IAttackReceiver receiver)
    {
        throw new System.NotImplementedException();
    }
    private IEnumerator Attacking()
    {
        //if (attacking && BattleManager.Instance.Enemy.ESM.IsOnState<EnemyStateMachine.Attacking>()) Lurch(2f);
        //StopAllCoroutines();
        // Divides duration beats into four sections!
        // First Divsion is early reveive
        // second divsion is deflection window
        // Third Division is late receive
        float divisionTime = _weaponData.AttackDuration / 4f;
        attacking = true;
        deflectionWindow = true;
        yield return new WaitForSeconds(3 * divisionTime * Conductor.quarter * Conductor.Instance.spb);
        deflectionWindow = false;
        yield return new WaitForSeconds(divisionTime * Conductor.quarter * Conductor.Instance.spb);
        attacking = false;
    }
    protected override void OnStagger()
    {
        base.OnStagger();
        Unblock();
        _particleSystem.Play();
    }
    protected override void OnUnstagger()
    {
        base.OnUnstagger();
        _particleSystem.Stop();
    }
    private IEnumerator EngageOpponent(EnemyBattlePawn opponentPawn)
    {
        CurrEnemyOpponent = opponentPawn;
        _traversalPawn.MoveToDestination(new Vector2(opponentPawn.transform.position.x, opponentPawn.transform.position.z + battlePositionOffset));
        yield return new WaitUntil(() => !_traversalPawn.movingToDestination);
        GameManager.Instance.GSM.Transition<GameStateMachine.Battle>();
    }
}