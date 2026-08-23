using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure confirm-popup state machine: one request at a time, token-gated
    /// transitions, and a guaranteed return to Idle after confirm, after cancel, and after a show that
    /// never displayed. Plain enums and ints — nothing Unity/TFTV is involved.
    /// </summary>
    public class SwapConfirmStateTests
    {
        private static SwapConfirmState Requested(out int token)
        {
            var state = new SwapConfirmState();
            Assert.True(state.TryBegin(out token));
            return state;
        }

        private static SwapConfirmState Shown(out int token)
        {
            SwapConfirmState state = Requested(out token);
            Assert.True(state.TryMarkShown(token));
            return state;
        }

        [Fact]
        public void FreshState_IsIdle()
        {
            var state = new SwapConfirmState();
            Assert.Equal(SwapModalPhase.Idle, state.Phase);
            Assert.False(state.Busy);
        }

        [Fact]
        public void Begin_FromIdle_MintsATokenAndBecomesRequested()
        {
            SwapConfirmState state = Requested(out int token);
            Assert.Equal(SwapModalPhase.Requested, state.Phase);
            Assert.True(state.Busy);
            Assert.NotEqual(0, token);
            Assert.Equal(token, state.Token);
        }

        // ---- re-entrancy: a second click while a popup is unanswered ---------------------------------

        [Fact]
        public void Begin_WhileRequested_IsRefusedAndChangesNothing()
        {
            SwapConfirmState state = Requested(out int first);

            Assert.False(state.TryBegin(out int second));

            Assert.Equal(0, second);                       // no token minted for the ignored click
            Assert.Equal(SwapModalPhase.Requested, state.Phase);
            Assert.Equal(first, state.Token);              // the pending request is NOT replaced
        }

        [Fact]
        public void Begin_WhileShown_IsRefusedAndChangesNothing()
        {
            SwapConfirmState state = Shown(out int first);

            Assert.False(state.TryBegin(out int _));

            Assert.Equal(SwapModalPhase.Shown, state.Phase);
            Assert.Equal(first, state.Token);
        }

        /// <summary>
        /// The refusal above must reach the caller as "busy, do nothing" — NEVER as the outcome that
        /// authorises the immediate, unconfirmed swap. Two distinct outcomes, so the old bool's
        /// conflation of them cannot come back.
        /// </summary>
        [Fact]
        public void BusyIsNotTheFallbackOutcome()
        {
            Assert.NotEqual(SwapShowOutcome.Busy, SwapShowOutcome.CannotShow);
            Assert.NotEqual(SwapShowOutcome.Busy, SwapShowOutcome.Opened);
        }

        // ---- token gating: a stale callback can never touch a newer request --------------------------

        [Fact]
        public void Resolve_WithAStaleToken_IsIgnored()
        {
            SwapConfirmState state = Shown(out int first);
            Assert.True(state.TryResolve(first));          // first request finishes
            Assert.True(state.TryBegin(out int second));   // ...and a NEW one starts
            Assert.NotEqual(first, second);                // tokens are never reused

            Assert.False(state.TryResolve(first));         // the old callback fires late

            Assert.Equal(SwapModalPhase.Requested, state.Phase);
            Assert.Equal(second, state.Token);
        }

        [Fact]
        public void MarkShown_WithAStaleToken_IsIgnored()
        {
            SwapConfirmState state = Requested(out int first);
            Assert.True(state.TryResolve(first));
            Assert.True(state.TryBegin(out int _));

            Assert.False(state.TryMarkShown(first));

            Assert.Equal(SwapModalPhase.Requested, state.Phase);
        }

        [Fact]
        public void MarkShown_Twice_IsIgnored()
        {
            SwapConfirmState state = Shown(out int token);

            Assert.False(state.TryMarkShown(token));

            Assert.Equal(SwapModalPhase.Shown, state.Phase);
        }

        // ---- every exit path returns to Idle ---------------------------------------------------------

        [Fact]
        public void Resolve_AfterConfirmOrCancel_ReturnsToIdle()
        {
            SwapConfirmState state = Shown(out int token);

            Assert.True(state.TryResolve(token));

            Assert.Equal(SwapModalPhase.Idle, state.Phase);
            Assert.False(state.Busy);
            Assert.True(state.TryBegin(out int _));        // the next click works again
        }

        /// <summary>The game's ModalHideHandler runs twice per hide; the second must be a no-op.</summary>
        [Fact]
        public void Resolve_Twice_IsIgnored()
        {
            SwapConfirmState state = Shown(out int token);
            Assert.True(state.TryResolve(token));

            Assert.False(state.TryResolve(token));

            Assert.Equal(SwapModalPhase.Idle, state.Phase);
        }

        /// <summary>Opened but never displayed (no show handler): the request must not stick.</summary>
        [Fact]
        public void Resolve_FromRequested_ReturnsToIdle()
        {
            SwapConfirmState state = Requested(out int token);

            Assert.True(state.TryResolve(token));

            Assert.Equal(SwapModalPhase.Idle, state.Phase);
            Assert.False(state.Busy);
            Assert.True(state.TryBegin(out int _));
        }

        // ---- confirm entitlement: hide and dialog result race, in EITHER order ----------------------

        /// <summary>
        /// The real game order: UIStateGeoModal.ExitState hides the modal (:120, our ModalHideHandler
        /// postfix) BEFORE it invokes the dialog callback (:86). The hide resolves the phase, so the
        /// callback's own resolve must fail — and the confirm must still run, exactly once.
        /// </summary>
        [Fact]
        public void Confirm_WhenHideArrivesBeforeTheResultCallback_RunsExactlyOnce()
        {
            SwapConfirmState state = Shown(out int token);
            int confirmed = 0;

            Assert.True(state.TryResolve(token));           // hide handler, first pass
            Assert.False(state.TryResolve(token));          // hide handler, second pass (no-op)

            if (state.TryResolve(token)) { }                // result callback: refused, already Idle
            if (state.TryClaimConfirm(token)) { confirmed++; }

            Assert.Equal(1, confirmed);
            Assert.Equal(SwapModalPhase.Idle, state.Phase);
            Assert.False(state.TryClaimConfirm(token));     // a repeat callback cannot swap again
            Assert.Equal(1, confirmed);
        }

        /// <summary>The other order (callback first, hide after): still exactly one confirm.</summary>
        [Fact]
        public void Confirm_WhenHideArrivesAfterTheResultCallback_RunsExactlyOnce()
        {
            SwapConfirmState state = Shown(out int token);
            int confirmed = 0;

            Assert.True(state.TryResolve(token));           // result callback wins the race
            if (state.TryClaimConfirm(token)) { confirmed++; }

            Assert.False(state.TryResolve(token));          // hide handler afterwards: harmless no-op
            Assert.False(state.TryResolve(token));

            Assert.Equal(1, confirmed);
            Assert.Equal(SwapModalPhase.Idle, state.Phase);
        }

        [Fact]
        public void ClaimConfirm_WithAStaleToken_IsRefused()
        {
            SwapConfirmState state = Shown(out int first);
            Assert.True(state.TryResolve(first));
            Assert.True(state.TryBegin(out int second));

            Assert.False(state.TryClaimConfirm(first));     // old popup's callback fires late
            Assert.True(state.TryClaimConfirm(second));     // the live request is untouched
        }

        [Fact]
        public void ClaimConfirm_AfterReset_IsRefused()
        {
            SwapConfirmState state = Shown(out int token);

            state.Reset();

            Assert.False(state.TryClaimConfirm(token));
        }

        [Fact]
        public void ClaimConfirm_BeforeAnyRequest_IsRefused()
        {
            var state = new SwapConfirmState();

            Assert.False(state.TryClaimConfirm(0));
            Assert.False(state.TryClaimConfirm(1));
        }

        [Fact]
        public void Reset_FromAnyPhase_ReturnsToIdleAndStalesTheToken()
        {
            SwapConfirmState state = Shown(out int token);

            state.Reset();

            Assert.Equal(SwapModalPhase.Idle, state.Phase);
            Assert.False(state.TryResolve(token));         // the abandoned callback cannot act
            Assert.True(state.TryBegin(out int next));
            Assert.NotEqual(token, next);
        }
    }
}
