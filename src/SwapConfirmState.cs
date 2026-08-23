namespace Morgott.Oracle
{
    /// <summary>
    /// Lifecycle of ONE confirm-popup request. The three things the old single <c>bool</c> collapsed —
    /// the geoscape view state, the SHARED reused <c>UIModal</c> GameObject and our own overlay — are all
    /// driven from this phase plus a request token, so "we asked for a popup" and "the popup is really on
    /// screen" are no longer the same fact.
    /// </summary>
    internal enum SwapModalPhase
    {
        /// <summary>No popup requested. The only phase in which a new request is accepted.</summary>
        Idle,

        /// <summary>We opened the modal state; its show handler has not run yet.</summary>
        Requested,

        /// <summary>The popup reached the binder's show handler, i.e. it is actually displayed.</summary>
        Shown,
    }

    /// <summary>
    /// Outcome of a show request. Splits the old <c>bool</c>'s two meanings into three, because "busy"
    /// and "cannot show" must NOT be handled the same way: only <see cref="CannotShow"/> may fall back to
    /// the immediate, unconfirmed swap.
    /// </summary>
    internal enum SwapShowOutcome
    {
        /// <summary>The popup is up; the caller does nothing and waits for the callback.</summary>
        Opened,

        /// <summary>A popup is already pending; the click is ignored. NEVER swap on this.</summary>
        Busy,

        /// <summary>No popup is possible here; the caller performs the immediate swap as before.</summary>
        CannotShow,
    }

    /// <summary>
    /// Pure (engine-free) state machine behind <see cref="SwapConfirmModal"/>. Allowed transitions:
    /// <code>
    /// Idle      --TryBegin(token)-->      Requested
    /// Requested --TryMarkShown(token)-->  Shown
    /// Requested --TryResolve(token)-->    Idle   (opened but never displayed)
    /// Shown     --TryResolve(token)-->    Idle   (confirm / cancel / hide)
    /// any       --Reset()-->              Idle   (teardown, self-heal)
    /// </code>
    /// Every transition but <see cref="Reset"/> is token-gated: a callback minted for an older request
    /// can never resolve or relabel a newer one, and a handler that fires twice for the same request
    /// (the game's <c>ModalHideHandler</c> does) is a no-op the second time.
    /// </summary>
    internal sealed class SwapConfirmState
    {
        private SwapModalPhase _phase = SwapModalPhase.Idle;
        private int _token;

        internal SwapModalPhase Phase => _phase;

        /// <summary>Token of the current request. 0 until the first request; never reused.</summary>
        internal int Token => _token;

        /// <summary>True while a request is outstanding (requested or shown).</summary>
        internal bool Busy => _phase != SwapModalPhase.Idle;

        /// <summary>
        /// Idle -&gt; Requested, minting a fresh <paramref name="token"/>. Returns false and changes
        /// NOTHING when a request is already outstanding (a second click is ignored, not a replacement).
        /// </summary>
        internal bool TryBegin(out int token)
        {
            if (_phase != SwapModalPhase.Idle)
            {
                token = 0;
                return false;
            }
            token = ++_token;
            _phase = SwapModalPhase.Requested;
            return true;
        }

        /// <summary>Requested -&gt; Shown for the matching token. Stale or repeated shows are refused.</summary>
        internal bool TryMarkShown(int token)
        {
            if (_phase != SwapModalPhase.Requested || token != _token)
            {
                return false;
            }
            _phase = SwapModalPhase.Shown;
            return true;
        }

        /// <summary>
        /// Requested/Shown -&gt; Idle for the matching token. A stale token, or a second resolve of the
        /// same request, is refused so it cannot clear a newer request's state.
        /// </summary>
        internal bool TryResolve(int token)
        {
            if (_phase == SwapModalPhase.Idle || token != _token)
            {
                return false;
            }
            _phase = SwapModalPhase.Idle;
            return true;
        }

        /// <summary>Force back to Idle (teardown / self-heal). Outstanding tokens become stale.</summary>
        internal void Reset()
        {
            _phase = SwapModalPhase.Idle;
        }
    }
}
