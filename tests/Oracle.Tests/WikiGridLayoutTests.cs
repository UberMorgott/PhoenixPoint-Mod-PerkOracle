using Morgott.Oracle;
using Xunit;

namespace Morgott.Oracle.Tests
{
    /// <summary>
    /// Unit tests for the pure wiki grid sizing core: the preferred (vanilla-sized) shape must be left
    /// alone, a growing candidate count must widen and then shrink to stay inside the screen, and every
    /// degenerate input (no cells, no area) must resolve to something sane. No Unity types involved.
    /// </summary>
    public class WikiGridLayoutTests
    {
        // The panel's own numbers: preferred 6 columns of a measured ~110px native cell, 8px spacing.
        private const float Cell = 110f;
        private const float Spacing = 8f;
        private const int PreferredColumns = 6;

        private static WikiGridPlan Plan(int count, float width, float height)
            => WikiGridLayout.Plan(count, Cell, Spacing, PreferredColumns, width, height);

        [Fact]
        public void NoCells_IsAnEmptyPlan()
        {
            WikiGridPlan plan = Plan(0, 1700f, 800f);
            Assert.Equal(0, plan.Columns);
            Assert.Equal(0, plan.Rows);
            Assert.Equal(0f, plan.CellSize);
        }

        [Fact]
        public void SingleCell_IsOneByOneAtNativeSize()
        {
            WikiGridPlan plan = Plan(1, 1700f, 800f);
            Assert.Equal(1, plan.Columns);
            Assert.Equal(1, plan.Rows);
            Assert.Equal(Cell, plan.CellSize);
            Assert.Equal(Cell, plan.Width);
            Assert.Equal(Cell, plan.Height);
        }

        [Fact]
        public void VanillaSizedPool_KeepsThePreferredSixColumnShape()
        {
            // 12 candidates in a roomy 1920x1080-ish canvas: exactly the pre-adaptive layout.
            WikiGridPlan plan = Plan(12, 1700f, 800f);
            Assert.Equal(PreferredColumns, plan.Columns);
            Assert.Equal(2, plan.Rows);
            Assert.Equal(Cell, plan.CellSize);
            Assert.Equal(6 * Cell + 5 * Spacing, plan.Width);
        }

        [Fact]
        public void CountFittingExactly_DoesNotShrink()
        {
            // 24 cells => 4 rows of 6; the area is exactly 4 rows tall.
            float exactHeight = 4 * Cell + 3 * Spacing;
            WikiGridPlan plan = Plan(24, 1700f, exactHeight);
            Assert.Equal(PreferredColumns, plan.Columns);
            Assert.Equal(4, plan.Rows);
            Assert.Equal(Cell, plan.CellSize);
            Assert.Equal(exactHeight, plan.Height);
        }

        [Fact]
        public void TooTallForSixColumns_WidensBeforeShrinking()
        {
            // 30 cells would be 5 rows at 6 columns (too tall for 3 rows of room), but the width holds
            // 14 columns -> 3 rows at the untouched native cell size.
            WikiGridPlan plan = Plan(30, 1700f, 3 * Cell + 2 * Spacing);
            Assert.True(plan.Columns > PreferredColumns);
            Assert.Equal(Cell, plan.CellSize);
            Assert.True(plan.Rows <= 3);
            Assert.True(plan.Width <= 1700f);
        }

        [Fact]
        public void NarrowArea_NeverExceedsTheAvailableWidth()
        {
            // Room for 3 columns only (3*110 + 2*8 = 346).
            WikiGridPlan plan = Plan(12, 350f, 800f);
            Assert.Equal(3, plan.Columns);
            Assert.True(plan.Width <= 350f);
        }

        [Fact]
        public void LargeCount_ShrinksTheCellToFitAndStaysInsideTheArea()
        {
            WikiGridPlan plan = Plan(120, 1700f, 700f);
            Assert.True(plan.CellSize < Cell);
            Assert.True(plan.Width <= 1700f);
            Assert.True(plan.Height <= 700f);
            Assert.True(plan.Columns * plan.Rows >= 120); // every cell has a place
        }

        [Fact]
        public void UnboundedCount_FloorsAtTheMinimumCellAndLetsTheCallerScroll()
        {
            // No cell size fits 5000 cells on one screen: the plan stops at the readable minimum and the
            // panel's ScrollRect takes over. It must still be a valid, width-respecting shape.
            WikiGridPlan plan = Plan(5000, 1700f, 700f);
            Assert.Equal(Cell * WikiGridLayout.MinCellFraction, plan.CellSize, 3);
            Assert.True(plan.Width <= 1700f);
            Assert.True(plan.Rows * plan.Columns >= 5000);
        }

        [Fact]
        public void ZeroArea_FallsBackToThePreferredShape()
        {
            WikiGridPlan plan = Plan(12, 0f, 0f);
            Assert.Equal(PreferredColumns, plan.Columns);
            Assert.Equal(2, plan.Rows);
            Assert.Equal(Cell, plan.CellSize);
        }

        [Fact]
        public void DegenerateInputs_AreClampedNotCrashed()
        {
            WikiGridPlan negativeSpacing = WikiGridLayout.Plan(4, Cell, -50f, PreferredColumns, 1700f, 800f);
            Assert.Equal(4, negativeSpacing.Columns);
            Assert.Equal(4 * Cell, negativeSpacing.Width);

            WikiGridPlan noColumns = WikiGridLayout.Plan(4, Cell, Spacing, 0, 1700f, 800f);
            Assert.Equal(1, noColumns.Columns);
            Assert.Equal(4, noColumns.Rows);

            Assert.Equal(0, WikiGridLayout.Plan(4, 0f, Spacing, PreferredColumns, 1700f, 800f).Columns);
        }
    }
}
