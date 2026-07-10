using System;
using System.IO;
using System.Linq;
using Base.Defs;
using Base.UI;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Entities.Research;
using PhoenixPoint.Geoscape.Entities.Research.Cost;
using PhoenixPoint.Geoscape.Entities.Research.Reward;
using UnityEngine;

namespace Morgott.Oracle
{
    /// <summary>
    /// Registers the custom "Operative Reconditioning" geoscape research that gates the perk swap, by
    /// cloning a vanilla research def at def-repo patch time (works on fresh games AND existing saves,
    /// because the clone is injected before <c>Research.Initialize</c> walks the research DBs).
    ///
    /// Vanilla-only (no TFTV). The clone/clear/register sequence mirrors the verified pattern used by both
    /// the Officer mod (PX_OfficerTraining) and TFTV's <c>CreateNewPXResearch</c>, but configured per spec:
    /// the project is available from game start (all <c>InitialStates</c> Unlocked, no reveal/unlock
    /// requirements) and grants no rewards (its only effect is unblocking the swap once completed).
    /// Everything is guarded so a missing template aborts registration gracefully rather than throwing.
    /// </summary>
    public static class PerkSwapResearch
    {
        /// <summary>
        /// Research <see cref="ResearchDef.Id"/> AND clone Guid. The swap gate checks completion via
        /// <c>Research.HasCompleted(string)</c>, which matches against <see cref="ResearchDef.Id"/> (NOT the
        /// Guid) — so the runtime check uses this exact string. We reuse it as the Guid too (dedup key).
        /// </summary>
        // SAVE KEY — permanent neutral GUID, decoupled from mod name. NEVER CHANGE (game stores research completion under this Id).
        public const string ResearchId = "f4b8e2a1-6c93-4d07-a5e1-3b9f0c2d8e64";

        /// <summary>
        /// Cached custom research illustration, loaded once (lazily) from
        /// <see cref="CustomIconRelativePath"/>. Null when the PNG is missing or fails to decode — callers
        /// (the <c>ResearchTooltip.Init</c> postfix) MUST null-check and no-op so the tooltip never breaks.
        /// Backing field + flag so a failed load is not retried on every tooltip open.
        /// </summary>
        private static Sprite _iconSprite;
        private static bool _iconLoadAttempted;
        public static Sprite IconSprite
        {
            get
            {
                if (!_iconLoadAttempted)
                {
                    _iconLoadAttempted = true;
                    _iconSprite = TryLoadCustomIconSprite();
                }
                return _iconSprite;
            }
        }

        /// <summary>Guid of the cloned <see cref="ResearchViewElementDef"/> (must differ from the research's).</summary>
        private const string ViewElementGuid = "a7c1d5e9-2b48-4f60-8d3a-1e6b9c4f7d20";

        /// <summary>
        /// Vanilla research def cloned as the template. <c>PX_AtmosphericAnalysis</c> is an early, simple
        /// project (the same template TFTV clones for its custom research), giving us a well-formed
        /// <c>InitialStates</c>/cost/view skeleton to overwrite.
        /// </summary>
        private const string TemplateResearchName = "PX_AtmosphericAnalysis_ResearchDef";

        /// <summary>Name of the Phoenix faction research database the project is registered into.</summary>
        private const string ResearchDbName = "pp_ResearchDB";

        /// <summary>
        /// Research point cost. Calibrated from player feedback: at ~80 research points/day, 1500 took ~19
        /// in-game days — far too long for this utility project. 240 ≈ 3 days, the intended pace. This is
        /// also in the ballpark of a vanilla early-game project. Single tunable const — change here to rebalance.
        /// </summary>
        private const int ResearchPointCost = 240;

        /// <summary>
        /// Relative path (under the mod install dir) of an optional custom research icon PNG. See the icon
        /// note in <see cref="ApplyCustomIcon"/>: v1 keeps the cloned template's stock icon because the
        /// research icon field is an Addressable (<see cref="ResearchViewElementDef.ResearchIcon"/>) and
        /// cannot accept a runtime-loaded <see cref="Sprite"/>. The path/loader are left in place for when a
        /// non-Addressable display path is wired.
        /// </summary>
        private const string CustomIconRelativePath = "Assets/Icons/PerkSwapResearch.png";

        /// <summary>
        /// Idempotently inject the research def into the repository + the Phoenix research DB. Safe to call
        /// every <c>ApplyDefRepoPatches</c>: dedups on the def Guid, and null-checks every template so a
        /// missing vanilla asset aborts gracefully (logged) instead of throwing into geoscape init.
        /// </summary>
        public static void EnsureRegistered(DefRepository repo)
        {
            try
            {
                if (repo == null)
                {
                    OracleLog.Debug("[Oracle] PerkSwapResearch: null DefRepository, skipping.");
                    return;
                }

                // Dedup: if we (or a previous patch pass / loaded save) already created it, do nothing.
                if (repo.GetDef(ResearchId) != null)
                {
                    return;
                }

                // DefRepository.GetDef resolves by Guid ONLY (DefRepository.cs:70-75, _guid2Def keyed by
                // def.Guid), and TemplateResearchName is a NAME, not a Guid — so a plain GetDef would return
                // null and abort registration. Resolve name-safe: try GetDef first (handles the case where the
                // string happens to be a Guid), then fall back to a name scan over GetAllDefs<ResearchDef>().
                // This mirrors the verified pattern in TFTV (TFTV.PortedAATweaks.Utilities.cs:36).
                ResearchDef template = repo.GetDef(TemplateResearchName) as ResearchDef
                    ?? repo.GetAllDefs<ResearchDef>().FirstOrDefault(d => d.name == TemplateResearchName);
                if ((UnityEngine.Object)(object)template == (UnityEngine.Object)null)
                {
                    OracleLog.Debug("[Oracle] PerkSwapResearch: template '" + TemplateResearchName
                              + "' not found; skipping registration.");
                    return;
                }

                ResearchDbDef db = FindResearchDb(repo);
                if ((UnityEngine.Object)(object)db == (UnityEngine.Object)null || db.Researches == null)
                {
                    OracleLog.Debug("[Oracle] PerkSwapResearch: research DB '" + ResearchDbName
                              + "' not found; skipping registration.");
                    return;
                }

                // Clone the research def (CreateDef sets Guid + adds to AllDefs so the tree sees it).
                ResearchDef research = repo.CreateDef<ResearchDef>(ResearchId, template);
                if ((UnityEngine.Object)(object)research == (UnityEngine.Object)null)
                {
                    OracleLog.Debug("[Oracle] PerkSwapResearch: CreateDef returned null; skipping.");
                    return;
                }

                research.name = ResearchId;
                research.Id = ResearchId;
                research.ResearchCost = ResearchPointCost;
                research.HideInUI = false;

                // No rewards, no tags: the project's only effect is unblocking the swap once completed.
                research.Unlocks = new ResearchRewardDef[0];
                research.Tags = new ResearchTagDef[0];
                research.Costs = new ResearchCostDef[0];
                research.InvalidatedBy = new string[0];

                // Available from game start: clear reveal/unlock requirement containers so the empty set
                // auto-satisfies, and force every faction's initial state to Unlocked.
                ClearRequirements(research);
                SetAllInitialStatesUnlocked(research);

                // View element: clone the template's so the project renders correctly, then point its loc
                // keys at our CSV terms. CreateDef again for a distinct, repository-owned def.
                ResearchViewElementDef ved = CreateViewElement(repo, template);
                if ((UnityEngine.Object)(object)ved != (UnityEngine.Object)null)
                {
                    research.ViewElementDef = ved;
                }

                db.Researches.Add(research);
                OracleLog.Debug("[Oracle] PerkSwapResearch registered: " + ResearchId
                          + " (cost " + ResearchPointCost + ") into " + ResearchDbName);
            }
            catch (Exception ex)
            {
                // Never throw out of ApplyDefRepoPatches — a failure just leaves the gate's HasCompleted
                // returning false, which the swap gate handles (deny + message) or the user disables.
                OracleLog.Debug("[Oracle] PerkSwapResearch.EnsureRegistered failed: " + ex);
            }
        }

        /// <summary>
        /// Flip the registered research's UI visibility live: the game's research tabs (Available + Completed)
        /// filter on <c>!ResearchDef.HideInUI</c>, so setting <c>HideInUI</c> hides/shows the project without
        /// touching the research tree or save state. No-op when the def is not registered (the perk swap was
        /// never enabled this session). NEVER removes the def — it may be referenced by a save; hiding is the
        /// safe, reversible "make it not exist in the UI" for a mid-campaign toggle-off. Fully guarded.
        /// </summary>
        public static void SetVisible(DefRepository repo, bool visible)
        {
            try
            {
                if (repo == null)
                {
                    return;
                }
                if (repo.GetDef(ResearchId) is ResearchDef def
                    && (UnityEngine.Object)(object)def != (UnityEngine.Object)null)
                {
                    def.HideInUI = !visible;
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwapResearch.SetVisible failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Research gate verdict for the swap entry point. Returns <see cref="PerkSwapVerdict.Allow"/> when
        /// the gate does not block (toggle off, or the soldier's faction has completed the research) and
        /// <see cref="PerkSwapVerdict.DenyResearchLocked"/> when the swap must be blocked pending research.
        /// Fail-safe: any error or a missing research path is treated as "completed" only when the toggle is
        /// off; with the toggle on, an unresolved completion check denies (keeping the feature gated).
        /// </summary>
        public static PerkSwapVerdict GateVerdict(bool requireResearch, GeoCharacter character)
        {
            if (!requireResearch)
            {
                return PerkSwapVerdict.Allow;
            }
            try
            {
                Research research = character?.Faction?.Research;
                if (research != null && research.HasCompleted(ResearchId))
                {
                    return PerkSwapVerdict.Allow;
                }
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwapResearch.GateVerdict failed: " + ex.Message);
            }
            return PerkSwapVerdict.DenyResearchLocked;
        }

        /// <summary>Find the Phoenix research DB by name; tolerant of it not existing.</summary>
        private static ResearchDbDef FindResearchDb(DefRepository repo)
        {
            foreach (ResearchDbDef db in repo.GetAllDefs<ResearchDbDef>())
            {
                if ((UnityEngine.Object)(object)db != (UnityEngine.Object)null && db.name == ResearchDbName)
                {
                    return db;
                }
            }
            return null;
        }

        /// <summary>
        /// Clear the reveal/unlock requirement containers so the project has no prerequisites. An empty
        /// requirement set is treated as satisfied by the research engine, so the project is immediately
        /// available. Guarded per container in case the cloned shape is unexpected.
        /// </summary>
        private static void ClearRequirements(ResearchDef research)
        {
            try
            {
                // RevealRequirements / UnlockRequirements are value-type structs (ReseachRequirementDefContainer),
                // so they are never null — just empty their Container arrays. An empty requirement set is
                // treated as satisfied, making the project immediately available.
                ReseachRequirementDefContainer reveal = research.RevealRequirements;
                reveal.Container = new ReseachRequirementDefOpContainer[0];
                research.RevealRequirements = reveal;

                ReseachRequirementDefContainer unlock = research.UnlockRequirements;
                unlock.Container = new ReseachRequirementDefOpContainer[0];
                research.UnlockRequirements = unlock;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwapResearch: clearing requirements failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Set EVERY entry of the cloned <see cref="ResearchDef.InitialStates"/> to
        /// <see cref="ResearchState.Unlocked"/> so the project is researchable from the start for whichever
        /// faction(s) the template carried. Loops all entries (cloned length is not assumed to be 1).
        /// </summary>
        private static void SetAllInitialStatesUnlocked(ResearchDef research)
        {
            ResearchDef.InitialResearchState[] states = research.InitialStates;
            if (states == null)
            {
                return;
            }
            for (int i = 0; i < states.Length; i++)
            {
                states[i].State = ResearchState.Unlocked;
            }
            research.InitialStates = states;
        }

        /// <summary>
        /// Clone the template's <see cref="ResearchViewElementDef"/> and route its localized text binds at
        /// our CSV keys. Returns null on failure (the research then keeps the template's own view def).
        /// </summary>
        private static ResearchViewElementDef CreateViewElement(DefRepository repo, ResearchDef template)
        {
            try
            {
                if (repo.GetDef(ViewElementGuid) is ResearchViewElementDef existing
                    && (UnityEngine.Object)(object)existing != (UnityEngine.Object)null)
                {
                    return existing;
                }

                ResearchViewElementDef templateVed = template.ViewElementDef;
                if ((UnityEngine.Object)(object)templateVed == (UnityEngine.Object)null)
                {
                    OracleLog.Debug("[Oracle] PerkSwapResearch: template has no ViewElementDef; using none.");
                    return null;
                }

                ResearchViewElementDef ved = repo.CreateDef<ResearchViewElementDef>(ViewElementGuid, templateVed);
                if ((UnityEngine.Object)(object)ved == (UnityEngine.Object)null)
                {
                    return null;
                }
                ved.name = ResearchId + "_ViewElementDef";

                // Point each localized text at our CSV terms. Guard each bind: cloned binds should be
                // non-null, but if any is null create a fresh one rather than NPE.
                // DisplayName1/Description are inherited ViewElementDef fields; Benefits/Complete/Reveal/
                // Unlock are public fields on ResearchViewElementDef — all settable by ref the same way.
                SetLocKey(ref ved.DisplayName1, "ORACLE_Research_Name");
                SetLocKey(ref ved.Description, "ORACLE_Research_Description");
                SetLocKey(ref ved.BenefitsText, "ORACLE_Research_Benefits");
                SetLocKey(ref ved.CompleteText, "ORACLE_Research_Complete");
                // Reveal/Unlock text: empty key (project has no reveal/unlock stage worth narrating).
                SetLocKey(ref ved.RevealText, string.Empty);
                SetLocKey(ref ved.UnlockText, string.Empty);

                ApplyCustomIcon(ved);
                return ved;
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwapResearch.CreateViewElement failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Set the loc key on a <see cref="LocalizedTextBind"/> field, creating it if null.</summary>
        private static void SetLocKey(ref LocalizedTextBind bind, string key)
        {
            if (bind == null)
            {
                bind = new LocalizedTextBind(key);
            }
            else
            {
                bind.LocalizationKey = key;
            }
        }

        /// <summary>
        /// ICON: this no longer touches the def's icon — it intentionally leaves the cloned template's
        /// Addressable <see cref="ResearchViewElementDef.ResearchIcon"/> as-is.
        ///
        /// The custom illustration is now LIVE, but applied at the DISPLAY site rather than on the def: the
        /// research icon field is an Addressable asset reference (<c>AssetReferenceSprite</c>), not a plain
        /// <see cref="Sprite"/>, so a runtime PNG → Sprite (ReadAllBytes → Texture2D → ImageConversion →
        /// Sprite.Create) cannot be assigned into it. Instead, <c>ResearchTooltipIconPatch</c> (a Harmony
        /// postfix on <c>ResearchTooltip.Init</c>) swaps the displayed <c>ResearchTooltip.Icon</c> Image to
        /// the cached <see cref="IconSprite"/> (loaded from <see cref="CustomIconRelativePath"/> via
        /// <see cref="TryLoadCustomIconSprite"/>) whenever our research is shown. If the PNG is missing,
        /// <see cref="IconSprite"/> stays null and the patch no-ops, leaving the stock icon.
        /// </summary>
        private static void ApplyCustomIcon(ResearchViewElementDef ved)
        {
            // No-op by design: the custom icon is applied at the display site (ResearchTooltipIconPatch),
            // not on the def's Addressable ResearchIcon. See the method summary.
            _ = ved;
        }

        /// <summary>
        /// Load the optional custom icon PNG from the mod install dir into a runtime <see cref="Sprite"/>.
        /// Used (once, lazily) by <see cref="IconSprite"/>, which <c>ResearchTooltipIconPatch</c> consumes to
        /// swap the displayed research icon. Returns null if the file is absent or decoding fails.
        /// </summary>
        private static Sprite TryLoadCustomIconSprite()
        {
            try
            {
                OracleMain inst = OracleMain.Instance;
                if (inst == null)
                {
                    return null;
                }
                // OracleMain.Instance is the static shadow; the framework ModInstance (with the install
                // dir) is the base ModMain.Instance, reached by casting past the shadowing property.
                string dir = ((PhoenixPoint.Modding.ModMain)inst).Instance.Entry.Directory;
                string path = Path.Combine(dir, CustomIconRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, bytes))
                {
                    return null;
                }
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch (Exception ex)
            {
                OracleLog.Debug("[Oracle] PerkSwapResearch.TryLoadCustomIconSprite failed: " + ex.Message);
                return null;
            }
        }
    }
}
